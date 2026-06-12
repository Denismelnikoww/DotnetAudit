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
            // Optionally, validate if the path exists here if desired
        });
        var fixOption = new Option<bool>(["--fix", "-f"], "Generate update scripts");

        command.AddArgument(pathArg);
        command.AddOption(fixOption);

        command.SetHandler(async (string path, bool fix) =>
        {
            // Use current directory if path is empty or whitespace
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
                var projects = GetProjectsFromGraph(graph);

                var allIssues = new List<VersionIssue>();
                foreach (var project in projects)
                {
                    var issues = await versionChecker.CheckPackagesAsync(project);
                    allIssues.AddRange(issues);
                }

                console.WriteVersionIssuesTable(allIssues);

                // Summary
                var outdated = allIssues.Count(i => i.IsOutdated);
                var majorUpdates = allIssues.Count(i => i.Difference == VersionDifference.Major);
                var minorUpdates = allIssues.Count(i => i.Difference == VersionDifference.Minor);
                var patchUpdates = allIssues.Count(i => i.Difference == VersionDifference.Patch);

                console.WriteInfo($"Summary: {outdated} outdated packages");
                console.WriteInfo($"  - Major updates: {majorUpdates}");
                console.WriteInfo($"  - Minor updates: {minorUpdates}");
                console.WriteInfo($"  - Patch updates: {patchUpdates}");

                if (fix && outdated > 0)
                {
                    var script = GenerateUpdateScript(allIssues);
                    await File.WriteAllTextAsync("update-packages.ps1", script);
                    console.WriteSuccess("Update script generated: update-packages.ps1");
                }

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
        }, pathArg, fixOption);

        return command;
    }

    private static List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
    {
        // Simplified - same as in AnalyzeCommand
        return new List<ProjectInfo>();
    }

    private static string GenerateUpdateScript(List<VersionIssue> issues)
    {
        var script = "# Package Update Script\n";
        script += "# Run with: powershell -File update-packages.ps1\n\n";

        foreach (var issue in issues.Where(i => i.IsOutdated && i.LatestVersion != null))
        {
            script += $"Write-Host \"Updating {issue.PackageName} from {issue.CurrentVersion} to {issue.LatestVersion}\" -ForegroundColor Yellow\n";
            script += $"dotnet add package {issue.PackageName} --version {issue.LatestVersion}\n";
        }

        script += "\nWrite-Host \"Update complete!\" -ForegroundColor Green\n";

        return script;
    }
}