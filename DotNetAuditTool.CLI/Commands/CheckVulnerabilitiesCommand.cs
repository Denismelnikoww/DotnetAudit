using DotNetAuditTool.CLI.Services;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder;
using DotNetAuditTool.Security;
using System.CommandLine;

namespace DotNetAuditTool.CLI.Commands;

public static class CheckVulnerabilitiesCommand
{
    public static Command Create()
    {
        var command = new Command("check-vulns", "Check for security vulnerabilities in packages");

        var pathArg = new Argument<string>("path", "Path to .csproj, .vbproj, .fsproj, .sln file or directory");
        pathArg.AddValidator(result =>
        {
            var pathVal = result.GetValueOrDefault<string>();
            var path = !string.IsNullOrWhiteSpace(pathVal) ? pathVal : Environment.CurrentDirectory;
            if (string.IsNullOrWhiteSpace(path))
            {
                result.ErrorMessage = "Path cannot be empty.";
            }
        });

        command.AddArgument(pathArg);

        command.SetHandler(ExecuteCheckVulnerabilitiesCommand, pathArg);

        return command;
    }

    private static async Task<int> ExecuteCheckVulnerabilitiesCommand(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.CurrentDirectory;
        }

        var console = new ConsoleOutputService();
        console.WriteHeader($"Checking vulnerabilities for: {path}");

        try
        {
            var analyzer = new DependencyAnalyzer();
            var graph = await analyzer.AnalyzeAsync(path);
            var projects = GetProjectsFromGraph(graph);

            var scanner = new VulnerabilityScanner();
            var report = await scanner.GenerateReportAsync(projects);

            console.WriteVulnerabilityTable(report.Vulnerabilities);

            console.WriteInfo($"Scanned {report.TotalPackages} packages across {report.TotalProjects} projects");
            console.WriteInfo($"Found {report.Vulnerabilities.Count} vulnerable packages");

            if (report.CriticalCount > 0)
                console.WriteError($"  - Critical: {report.CriticalCount}");
            if (report.HighCount > 0)
                console.WriteWarning($"  - High: {report.HighCount}");
            if (report.MediumCount > 0)
                console.WriteInfo($"  - Medium: {report.MediumCount}");
            if (report.LowCount > 0)
                console.WriteInfo($"  - Low: {report.LowCount}");

            if (report.CriticalCount > 0)
            {
                console.WriteError("Critical vulnerabilities found! Immediate action required.");
                return 1;
            }
            else if (report.HighCount > 0)
            {
                console.WriteWarning("High severity vulnerabilities found.");
                return 1;
            }
            else
            {
                console.WriteSuccess("No critical or high severity vulnerabilities found.");
                return 0;
            }
        }
        catch (Exception ex)
        {
            console.WriteError($"Vulnerability check failed: {ex.Message}");
            return 1;
        }
    }

    private static List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
    {
        var projects = new List<ProjectInfo>();

        foreach (var node in graph.Nodes.Where(n => n.Type == DependencyType.ProjectReference))
        {
            if (node.Metadata.TryGetValue("Path", out var path))
            {
                var project = new ProjectInfo
                {
                    Name = node.Name,
                    FilePath = path.ToString() ?? string.Empty,
                    TargetFramework = node.Metadata.TryGetValue("TargetFramework", out var tf) ? tf.ToString() ?? node.Version : node.Version
                };

                foreach (var depId in node.DependencyIds)
                {
                    var depNode = graph.Nodes.FirstOrDefault(n => n.Id == depId);
                    if (depNode?.Type == DependencyType.NuGet)
                    {
                        project.Packages.Add(new PackageReference
                        {
                            Name = depNode.Name,
                            Version = depNode.Version,
                            IsDevelopmentDependency = depNode.Metadata.ContainsKey("IsDevelopmentDependency") && (bool)depNode.Metadata["IsDevelopmentDependency"],
                            IsPrivateAssets = depNode.Metadata.ContainsKey("IsPrivateAssets") && (bool)depNode.Metadata["IsPrivateAssets"]
                        });
                    }
                }

                projects.Add(project);
            }
        }

        return projects;
    }
}