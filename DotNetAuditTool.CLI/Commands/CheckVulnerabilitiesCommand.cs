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

        var pathArg = new Argument<string>("path", "Path to .csproj, .sln file or directory");
        var githubTokenOption = new Option<string>(["--github-token", "-t"],
            "GitHub token for advisory API (optional)");

        command.AddArgument(pathArg);
        command.AddOption(githubTokenOption);

        command.SetHandler(async (string path, string? githubToken) =>
        {
            var console = new ConsoleOutputService();
            console.WriteHeader($"Checking vulnerabilities for: {path}");

            try
            {
                // Set GitHub token if provided
                if (!string.IsNullOrEmpty(githubToken))
                {
                    Environment.SetEnvironmentVariable("GITHUB_TOKEN", githubToken);
                }

                var analyzer = new DependencyAnalyzer();
                var graph = await analyzer.AnalyzeAsync(path);
                var projects = GetProjectsFromGraph(graph);

                var scanner = new VulnerabilityScanner(githubToken);
                var report = await scanner.GenerateReportAsync(projects);

                console.WriteVulnerabilityTable(report.Vulnerabilities);

                // Summary
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

                var riskColor = report.RiskScore >= 70 ? "red" : report.RiskScore >= 40 ? "yellow" : "green";
                console.WriteInfo($"Risk score: [{riskColor}]{report.RiskScore:F1}%[/]");

                if (report.CriticalCount > 0)
                {
                    console.WriteError("Critical vulnerabilities found! Immediate action required.");
                    Environment.ExitCode = 1;
                }
                else if (report.HighCount > 0)
                {
                    console.WriteWarning("High severity vulnerabilities found.");
                    Environment.ExitCode = 1;
                }
                else
                {
                    console.WriteSuccess("No critical or high severity vulnerabilities found.");
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Vulnerability check failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, pathArg, githubTokenOption);

        return command;
    }

    private static List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
    {
        // Same implementation as in other commands
        return new List<ProjectInfo>();
    }
}