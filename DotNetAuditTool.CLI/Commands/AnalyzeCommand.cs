using DotNetAuditTool.CLI.Reporters;
using DotNetAuditTool.CLI.Services;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder;
using DotNetAuditTool.Secrets;
using DotNetAuditTool.Security;
using DotNetAuditTool.VersionChecker;
using Spectre.Console;
using System.CommandLine;

namespace DotNetAuditTool.CLI.Commands;

public static class AnalyzeCommand
{
    public static Command Create()
    {
        var command = new Command("analyze", "Perform complete audit of .NET project");

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

        var outputOption = new Option<string>(["--output", "-o"], () => "audit-report.json", "Output file path for JSON report");
        var verboseOption = new Option<bool>(["--verbose", "-v"], "Enable verbose output");

        command.AddArgument(pathArg);
        command.AddOption(outputOption);
        command.AddOption(verboseOption);

        command.SetHandler(ExecuteAnalyzeCommand, pathArg, outputOption, verboseOption);

        return command;
    }

    private static async Task<int> ExecuteAnalyzeCommand(string path, string output, bool verbose)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.CurrentDirectory;
        }

        var console = new ConsoleOutputService();
        console.WriteBanner();
        console.WriteHeader($"Auditing: {path}");

        try
        {
            console.WriteInfo("Building dependency graph...");

            var graphBuilder = new DependencyAnalyzer();
            var graph = await graphBuilder.AnalyzeAsync(path);

            console.WriteSuccess($"Found {graph.Nodes.Count} dependency nodes");

            var projects = graphBuilder.GetProjectsFromGraph(graph);
            console.WriteInfo($"Found {projects.Count} projects to analyze");

            console.WriteInfo("Checking version compatibility...");
            var versionChecker = new VersionCompatibilityChecker();
            var projectCompatibilityChecker = new ProjectReferenceCompatibilityChecker();
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

            var projectCompatibilityIssues = projectCompatibilityChecker.CheckProjectReferences(projects);
            var outdatedCount = versionIssues.Count(i => i.IsOutdated);
            var compatibilityIssueCount = versionIssues.Count(i => !i.IsCompatible) + projectCompatibilityIssues.Count;
            console.WriteSuccess($"Found {outdatedCount} outdated packages and {compatibilityIssueCount} compatibility issues");

            console.WriteInfo("Scanning for vulnerabilities...");
            var vulnScanner = new VulnerabilityScanner();
            var vulnerabilities = await vulnScanner.ScanPackagesAsync(
                projects.SelectMany(p => p.Packages).DistinctBy(p => p.Name).ToList()
            );
            console.WriteSuccess($"Found {vulnerabilities.Count} vulnerabilities");

            console.WriteInfo("Scanning for secrets in code...");
            var configurationService = new ConfigurationService();
            var settings = configurationService.Load();
            var secretDetector = new SecretDetector(settings.EntropyThreshold);
            var scanTarget = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
            var reportFullPath = Path.GetFullPath(output ?? "audit-report.json");
            var secretResult = await secretDetector.ScanAsync(scanTarget, new[] { reportFullPath });
            console.WriteSuccess($"Found {secretResult.FoundSecrets.Count} potential secrets");

            var report = new AuditReport
            {
                TargetPath = path,
                Graph = graph,
                VersionIssues = versionIssues,
                Vulnerabilities = vulnerabilities,
                Secrets = secretResult.FoundSecrets,
                ProjectCompatibilityIssues = projectCompatibilityIssues,
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
                }
            };

            console.WriteSummary(report);

            if (verbose)
            {
                if (vulnerabilities.Any())
                {
                    console.WriteHeader("VULNERABILITIES");
                    console.WriteVulnerabilityTable(vulnerabilities);
                }

                if (versionIssues.Any(i => i.IsOutdated || !i.IsCompatible || !string.IsNullOrWhiteSpace(i.Suggestion)))
                {
                    console.WriteHeader("VERSION ISSUES");
                    console.WriteVersionIssuesTable(versionIssues);
                }

                if (projectCompatibilityIssues.Any())
                {
                    console.WriteHeader("PROJECT COMPATIBILITY");
                    console.WriteProjectCompatibilityTable(projectCompatibilityIssues);
                }

                if (secretResult.FoundSecrets.Any())
                {
                    console.WriteHeader("POTENTIAL SECRETS");
                    console.WriteSecretsTable(secretResult.FoundSecrets);
                }
            }

            if (!string.IsNullOrEmpty(output))
            {
                IReportWriter<AuditReport> reportWriter = ReportWriterFactory.CreateByExtension<AuditReport>(output);
                await reportWriter.WriteAsync(report, output);
                console.WriteSuccess($"Report saved to {output}");
            }

            if (vulnerabilities.Any(v => v.Severity == SeverityLevel.Critical) ||
                secretResult.RiskLevel == SecretRiskLevel.Critical || projectCompatibilityIssues.Any(p => !p.IsCompatible))
            {
                console.WriteError("Critical issues found! Audit failed.");
                return 1;
            }
            else
            {
                console.WriteSuccess("Audit completed successfully!");
                return 0;
            }
        }
        catch (Exception ex)
        {
            console.WriteError($"Audit failed: {ex.Message}");
            if (verbose)
                console.WriteError(ex.StackTrace ?? "");
            return 1;
        }
    }
}