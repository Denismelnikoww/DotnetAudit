using DotNetAuditTool.CLI.Services;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder;
using Spectre.Console;
using System.CommandLine;

namespace DotNetAuditTool.CLI.Commands;

public static class GraphCommand
{
    public static Command Create()
    {
        var command = new Command("graph", "Build and visualize dependency graph");

        var pathArg = new Argument<string>("path", "Path to .csproj, .sln file or directory");
        var formatOption = new Option<string>(["--format", "-f"], () => "console",
            "Output format: console, mermaid, json");
        var outputOption = new Option<string>(["--output", "-o"], "Output file path");

        command.AddArgument(pathArg);
        command.AddOption(formatOption);
        command.AddOption(outputOption);

        command.SetHandler(async (string path, string format, string? output) =>
        {
            var console = new ConsoleOutputService();
            console.WriteHeader($"Building dependency graph for: {path}");

            try
            {
                var analyzer = new DependencyAnalyzer();
                var graph = await analyzer.AnalyzeAsync(path);

                console.WriteSuccess($"Built graph with {graph.Nodes.Count} nodes and {graph.Edges.Count} edges");

                // Find circular dependencies
                var cycles = analyzer.FindCircularDependencies(graph);
                if (cycles.Any())
                {
                    console.WriteWarning($"Found {cycles.Count} circular dependencies:");
                    foreach (var cycle in cycles.Take(5))
                    {
                        console.WriteInfo($"  {cycle}");
                    }
                }

                // Output in requested format
                if (format == "mermaid")
                {
                    var mermaid = GenerateMermaidGraph(graph);
                    if (!string.IsNullOrEmpty(output))
                    {
                        await File.WriteAllTextAsync(output, mermaid);
                        console.WriteSuccess($"Mermaid graph saved to {output}");
                    }
                    else
                    {
                        Console.WriteLine(mermaid);
                    }
                }
                else if (format == "json")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(graph, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    if (!string.IsNullOrEmpty(output))
                    {
                        await File.WriteAllTextAsync(output, json);
                        console.WriteSuccess($"JSON graph saved to {output}");
                    }
                    else
                    {
                        Console.WriteLine(json);
                    }
                }
                else
                {
                    // Console output
                    var rootNodes = graph.GetRootNodes();
                    console.WriteInfo($"Root nodes: {string.Join(", ", rootNodes.Select(n => n.Name))}");

                    var table = new Table();
                    table.AddColumn("[cyan]Node[/]");
                    table.AddColumn("[yellow]Type[/]");
                    table.AddColumn("[green]Version/Framework[/]");
                    table.AddColumn("[blue]Dependencies[/]");

                    foreach (var node in graph.Nodes.Take(50))
                    {
                        var deps = graph.AdjacencyList.GetValueOrDefault(node.Id, new List<string>());
                        table.AddRow(
                            node.Name,
                            node.Type.ToString(),
                            node.Version,
                            deps.Count.ToString()
                        );
                    }

                    AnsiConsole.Write(table);

                    if (graph.Nodes.Count > 50)
                    {
                        console.WriteInfo($"... and {graph.Nodes.Count - 50} more nodes");
                    }
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Failed to build graph: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, pathArg, formatOption, outputOption);

        return command;
    }

    private static string GenerateMermaidGraph(DependencyGraph graph)
    {
        var mermaid = "graph TD\n";

        foreach (var edge in graph.Edges.Take(100)) // Limit for readability
        {
            var sourceNode = graph.Nodes.FirstOrDefault(n => n.Id == edge.Source);
            var targetNode = graph.Nodes.FirstOrDefault(n => n.Id == edge.Target);

            if (sourceNode != null && targetNode != null)
            {
                var sourceLabel = sourceNode.Name.Replace(" ", "_").Replace(".", "_");
                var targetLabel = targetNode.Name.Replace(" ", "_").Replace(".", "_");
                mermaid += $"    {sourceLabel}[{sourceNode.Name}] --> {targetLabel}[{targetNode.Name}]\n";
            }
        }

        return mermaid;
    }
}