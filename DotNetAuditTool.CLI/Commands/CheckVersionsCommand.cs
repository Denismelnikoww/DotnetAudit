using DotNetAuditTool.CLI.Services;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder;
using DotNetAuditTool.VersionChecker;
using System.CommandLine;

namespace DotNetAuditTool.CLI.Commands;

public static class CheckVersionsCommand
{
    public static Command Create()
    {
        var command = new Command("check-versions", "Check for outdated packages and version compatibility");

        var pathArg = new Argument<string>("path", "Path to .csproj, .sln file or directory");
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

        command.SetHandler(async (string path) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Environment.CurrentDirectory;
            }

            var console = new ConsoleOutputService();
            console.WriteHeader($"Checking versions for: {path}");

            try
            {
                var analyzer = new DependencyAnalyzer();
                var graph = await analyzer.AnalyzeAsync(path);

                var versionChecker = new VersionCompatibilityChecker();
                var projectCompatibilityChecker = new ProjectReferenceCompatibilityChecker();
                var projects = GetProjectsFromGraph(graph);

                var allIssues = new List<VersionIssue>();
                foreach (var project in projects)
                {
                    var issues = await versionChecker.CheckPackagesAsync(project);
                    allIssues.AddRange(issues);
                }

                var projectCompatibilityIssues = projectCompatibilityChecker.CheckProjectReferences(projects);
                console.WriteVersionIssuesTable(allIssues);
                console.WriteHeader("PROJECT COMPATIBILITY");
                console.WriteProjectCompatibilityTable(projectCompatibilityIssues);

                var projectCompatibilityCount = projectCompatibilityIssues.Count;
                var outdated = allIssues.Count(i => i.IsOutdated);
                var majorUpdates = allIssues.Count(i => i.Difference == VersionDifference.Major);
                var minorUpdates = allIssues.Count(i => i.Difference == VersionDifference.Minor);
                var patchUpdates = allIssues.Count(i => i.Difference == VersionDifference.Patch);

                console.WriteInfo($"Summary: {outdated} outdated packages, {projectCompatibilityCount} project compatibility issues");
                console.WriteInfo($"  - Major updates: {majorUpdates}");
                console.WriteInfo($"  - Minor updates: {minorUpdates}");
                console.WriteInfo($"  - Patch updates: {patchUpdates}");

                if (outdated == 0)
                {
                    console.WriteSuccess("All packages are up to date!");
                }
                else
                {
                    Environment.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Version check failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, pathArg);

        return command;
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