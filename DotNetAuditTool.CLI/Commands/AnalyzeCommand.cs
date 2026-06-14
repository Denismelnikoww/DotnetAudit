using DotNetAuditTool.CLI.Reporters;
using DotNetAuditTool.CLI.Services;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.Secrets;
using DotNetAuditTool.Security;
using DotNetAuditTool.VersionChecker;
using DotNetAuditTool.DependencyGraphBuilder;
using Spectre.Console;
using System.CommandLine;

namespace DotNetAuditTool.CLI.Commands;

public static class AnalyzeCommand
{
    public static Command Create()
    {
        var command = new Command("analyze", "Perform complete audit of .NET project");

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

        var outputOption = new Option<string>(["--output", "-o"], () => "audit-report.json", "Output file path for JSON report");
        var verboseOption = new Option<bool>(["--verbose", "-v"], "Enable verbose output");

        command.AddArgument(pathArg);
        command.AddOption(outputOption);
        command.AddOption(verboseOption);

        command.SetHandler(async (string path, string output, bool verbose) =>
        {
            // Use current directory if path is empty or whitespace
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Environment.CurrentDirectory;
            }

            var console = new ConsoleOutputService();
            console.WriteBanner();
            console.WriteHeader($"Auditing: {path}");

            try
            {
                // 1. Build dependency graph
                console.WriteInfo("Building dependency graph...");
                var graphBuilder = new DependencyAnalyzer();
                var graph = await graphBuilder.AnalyzeAsync(path);
                console.WriteSuccess($"Found {graph.Nodes.Count} dependency nodes");

                // 2. Get all projects
                var projects = GetProjectsFromGraph(graph);
                console.WriteInfo($"Found {projects.Count} projects to analyze");

                // 3. Check version compatibility
                console.WriteInfo("Checking version compatibility...");
                var versionChecker = new VersionCompatibilityChecker();
                var versionIssues = new List<VersionIssue>();

                await AnsiConsole.Progress()
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Checking packages versions");
                        foreach (var project in projects)
                        {
                            var issues = await versionChecker.CheckPackagesAsync(project);
                            versionIssues.AddRange(issues);
                            task.Increment(100.0 / projects.Count);
                        }
                    });

                console.WriteSuccess($"Found {versionIssues.Count(i => i.IsOutdated)} outdated packages");

                // 4. Scan for vulnerabilities
                console.WriteInfo("Scanning for vulnerabilities...");
                var vulnScanner = new VulnerabilityScanner();
                var vulnerabilities = await vulnScanner.ScanPackagesAsync(
                    projects.SelectMany(p => p.Packages).DistinctBy(p => p.Name).ToList()
                );
                console.WriteSuccess($"Found {vulnerabilities.Count} vulnerabilities");

                // 5. Scan for secrets
                console.WriteInfo("Scanning for secrets in code...");
                var secretDetector = new SecretDetector();
                var scanTarget = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
                // compute full path to output file so scanner can ignore it
                var reportFullPath = Path.GetFullPath(output ?? "audit-report.json");
                var secretResult = await secretDetector.ScanAsync(scanTarget, new[] { reportFullPath });
                console.WriteSuccess($"Found {secretResult.FoundSecrets.Count} potential secrets");

                // 6. Generate report
                var report = new AuditReport
                {
                    TargetPath = path,
                    Graph = graph,
                    VersionIssues = versionIssues,
                    Vulnerabilities = vulnerabilities,
                    Secrets = secretResult.FoundSecrets,
                    Summary = new AuditSummary
                    {
                        TotalProjects = projects.Count,
                        TotalPackages = projects.SelectMany(p => p.Packages).Count(),
                        TotalVulnerabilities = vulnerabilities.Count,
                        TotalSecrets = secretResult.FoundSecrets.Count,
                        OutdatedPackages = versionIssues.Count(i => i.IsOutdated),
                        VulnerabilitiesBySeverity = vulnerabilities
                            .GroupBy(v => v.Severity)
                            .ToDictionary(g => g.Key, g => g.Count()),
                        SecretsByType = secretResult.FoundSecrets
                            .GroupBy(s => s.Type)
                            .ToDictionary(g => g.Key, g => g.Count()),
                        CriticalityScore = CalculateCriticalityScore(vulnerabilities, secretResult.FoundSecrets)
                    }
                };

                // 7. Display summary
                console.WriteSummary(report);

                // 8. Show detailed tables if verbose
                if (verbose)
                {
                    if (vulnerabilities.Any())
                    {
                        console.WriteHeader("VULNERABILITIES");
                        console.WriteVulnerabilityTable(vulnerabilities);
                    }

                    if (versionIssues.Any(i => i.IsOutdated))
                    {
                        console.WriteHeader("OUTDATED PACKAGES");
                        console.WriteVersionIssuesTable(versionIssues);
                    }

                    if (secretResult.FoundSecrets.Any())
                    {
                        console.WriteHeader("POTENTIAL SECRETS");
                        console.WriteSecretsTable(secretResult.FoundSecrets);
                    }
                }

                // 9. Save JSON report
                if (!string.IsNullOrEmpty(output))
                {
                    IReportWriter<AuditReport> reportWriter = ReportWriterFactory.CreateJson<AuditReport>();
                    await reportWriter.WriteAsync(report, output);
                    console.WriteSuccess($"Report saved to {output}");
                }

                // 10. Exit with error code if critical issues found
                if (vulnerabilities.Any(v => v.Severity == SeverityLevel.Critical) ||
                    secretResult.RiskLevel == SecretRiskLevel.Critical)
                {
                    Environment.ExitCode = 1;
                    console.WriteError("Critical issues found! Audit failed.");
                }
                else
                {
                    console.WriteSuccess("Audit completed successfully!");
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Audit failed: {ex.Message}");
                if (verbose)
                    console.WriteError(ex.StackTrace ?? "");
                Environment.ExitCode = 1;
            }
        }, pathArg, outputOption, verboseOption);

        return command;
    }

    private static List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
    {
        var projects = new List<ProjectInfo>();

        foreach (var node in graph.Nodes)
        {
            if (node.Type == DependencyType.ProjectReference &&
                node.Metadata.TryGetValue("Path", out var path))
            {
                var project = new ProjectInfo
                {
                    Name = node.Name,
                    FilePath = path.ToString() ?? string.Empty,
                    TargetFramework = node.Version
                };

                // Parse packages from metadata
                if (node.Metadata.TryGetValue("Packages", out var packageCount))
                {
                    // This is simplified - in real implementation you'd store full package info
                }

                projects.Add(project);
            }
        }

        return projects;
    }

    private static double CalculateCriticalityScore(List<Vulnerability> vulnerabilities, List<SecretMatch> secrets)
    {
        double score = 0;

        // Vulnerabilities contribute up to 70 points
        var vulnScore = vulnerabilities.Sum(v => v.CvssScore);
        score += Math.Min(70, vulnScore);

        // Secrets contribute up to 30 points
        var secretScore = Math.Min(30, secrets.Count * 3);
        score += secretScore;

        return score;
    }
}