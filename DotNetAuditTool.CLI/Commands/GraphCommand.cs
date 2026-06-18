using DotNetAuditTool.CLI.Reporters;
using DotNetAuditTool.CLI.Services;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder;
using Spectre.Console;
using System.CommandLine;
using System.IO;

namespace DotNetAuditTool.CLI.Commands;

public static class GraphCommand
{
    public static Command Create()
    {
        var command = new Command("graph", "Build and visualize dependency graph");

        var pathArg = new Argument<string>("path", () => ".",
            "Path to .csproj, .sln file or directory (defaults to current directory)");

        var formatOption = new Option<string>(["--format", "-f"], () => "console",
            "Output format: console, mermaid, json");
        var outputOption = new Option<string>(["--output", "-o"], "Output file path");

        command.AddArgument(pathArg);
        command.AddOption(formatOption);
        command.AddOption(outputOption);

        command.SetHandler(async (string path, string format, string? output) =>
        {
            var console = new ConsoleOutputService();

            var fullPath = Path.GetFullPath(path);

            console.WriteHeader($"Building dependency graph for: {fullPath}");

            try
            {
                var analyzer = new DependencyAnalyzer();
                var graph = await analyzer.AnalyzeAsync(fullPath);

                console.WriteSuccess($"Built graph with {graph.Nodes.Count} nodes and {graph.Edges.Count} edges");

                var cycles = analyzer.FindCircularDependencies(graph);
                if (cycles.Any())
                {
                    console.WriteWarning($"Found {cycles.Count} circular dependencies:");
                    foreach (var cycle in cycles.Take(5))
                    {
                        console.WriteInfo($"  {cycle}");
                    }
                }

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
                    IReportWriter<DependencyGraph> reportWriter;
                    if (!string.IsNullOrEmpty(output))
                    {
                        reportWriter = ReportWriterFactory.CreateByExtension<DependencyGraph>(output);
                    }
                    else
                    {
                        reportWriter = ReportWriterFactory.CreateJson<DependencyGraph>();
                    }

                    var serializedGraph = reportWriter.Serialize(graph);

                    if (!string.IsNullOrEmpty(output))
                    {
                        await reportWriter.WriteAsync(graph, output);
                        console.WriteSuccess($"Graph saved to {output}");
                    }
                    else
                    {
                        Console.WriteLine(serializedGraph);
                    }
                }
                else
                {
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
                            node.Version ?? "N/A",
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
                var sourceLabel = SanitizeMermaidLabel(sourceNode.Name);
                var targetLabel = SanitizeMermaidLabel(targetNode.Name);
                mermaid += $"    {sourceLabel}[{EscapeMermaidText(sourceNode.Name)}] --> {targetLabel}[{EscapeMermaidText(targetNode.Name)}]\n";
            }
        }

        return mermaid;
    }

    private static string SanitizeMermaidLabel(string name)
    {
        return name
            .Replace(' ', '_')
            .Replace('.', '_')
            .Replace('-', '_')
            .Replace('/', '_')
            .Replace('\\', '_');
    }

    private static string EscapeMermaidText(string text)
    {
        return text
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }
}