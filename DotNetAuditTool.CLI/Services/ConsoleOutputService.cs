using Spectre.Console;
using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.CLI.Services;

public class ConsoleOutputService
{
    public void WriteHeader(string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[yellow]{title}[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("yellow")
        });
        AnsiConsole.WriteLine();
    }

    public void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {message}");
    }

    public void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {message}");
    }

    public void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {message}");
    }

    public void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {message}");
    }

    public void WriteTable(string title, IEnumerable<(string Label, string Value)> rows)
    {
        var table = new Table();
        table.AddColumn("[cyan]Property[/]");
        table.AddColumn("[cyan]Value[/]");
        table.Border = TableBorder.Rounded;

        foreach (var row in rows)
        {
            table.AddRow(row.Label, row.Value);
        }

        AnsiConsole.Write(table);
    }

    public void WriteVulnerabilityTable(List<Vulnerability> vulnerabilities)
    {
        if (!vulnerabilities.Any())
        {
            WriteSuccess("No vulnerabilities found!");
            return;
        }

        var table = new Table();
        table.AddColumn("[red]Severity[/]");
        table.AddColumn("[yellow]Package[/]");
        table.AddColumn("[cyan]Current Version[/]");
        table.AddColumn("[green]Patched Version[/]");
        table.AddColumn("[blue]CVE ID[/]");

        foreach (var vuln in vulnerabilities)
        {
            var severityColor = vuln.Severity switch
            {
                SeverityLevel.Critical => "red",
                SeverityLevel.High => "orange3",
                SeverityLevel.Medium => "yellow",
                _ => "blue"
            };

            table.AddRow(
                $"[{severityColor}]{vuln.Severity}[/]",
                vuln.PackageName,
                vuln.InstalledVersion,
                vuln.PatchedVersion ?? "N/A",
                vuln.Id
            );
        }

        AnsiConsole.Write(table);
    }

    public void WriteVersionIssuesTable(List<VersionIssue> issues)
    {
        if (!issues.Any())
        {
            WriteSuccess("All packages are up to date!");
            return;
        }

        var table = new Table();
        table.AddColumn("[yellow]Package[/]");
        table.AddColumn("[cyan]Current[/]");
        table.AddColumn("[green]Latest[/]");
        table.AddColumn("[blue]Difference[/]");
        table.AddColumn("[white]Suggestion[/]");

        foreach (var issue in issues.Where(i => i.IsOutdated))
        {
            var diffColor = issue.Difference switch
            {
                VersionDifference.Major => "red",
                VersionDifference.Minor => "yellow",
                _ => "blue"
            };

            table.AddRow(
                issue.PackageName,
                issue.CurrentVersion,
                issue.LatestVersion ?? "N/A",
                $"[{diffColor}]{issue.Difference}[/]",
                issue.Suggestion ?? ""
            );
        }

        AnsiConsole.Write(table);
    }

    public void WriteSecretsTable(List<SecretMatch> secrets)
    {
        if (!secrets.Any())
        {
            WriteSuccess("No secrets found in code!");
            return;
        }

        var table = new Table();
        table.AddColumn("[red]Type[/]");
        table.AddColumn("[yellow]File[/]");
        table.AddColumn("[cyan]Line[/]");
        table.AddColumn("[blue]Matched Value[/]");
        table.AddColumn("[white]Entropy[/]");

        foreach (var secret in secrets.Take(50)) // Limit output
        {
            table.AddRow(
                secret.Type.ToString(),
                Path.GetFileName(secret.FilePath),
                secret.LineNumber.ToString(),
                secret.MatchedValue,
                secret.Entropy.ToString("F2")
            );
        }

        if (secrets.Count > 50)
        {
            AnsiConsole.MarkupLine($"[yellow]... and {secrets.Count - 50} more secrets[/]");
        }

        AnsiConsole.Write(table);
    }

    public void WriteProgress<T>(IEnumerable<T> items, Func<T, Task> action, string message)
    {
        AnsiConsole.Progress()
            .Start(ctx =>
            {
                var task = ctx.AddTask(message);

                var itemsList = items.ToList();
                var total = itemsList.Count;

                foreach (var item in itemsList)
                {
                    action(item).Wait();
                    task.Increment(100.0 / total);
                }
            });
    }

    public void WriteSummary(AuditReport report)
    {
        WriteHeader("AUDIT SUMMARY");

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow("[bold]Total Projects:[/]", report.Summary.TotalProjects.ToString());
        grid.AddRow("[bold]Total Packages:[/]", report.Summary.TotalPackages.ToString());
        grid.AddRow("[bold]Vulnerabilities:[/]", $"[red]{report.Summary.TotalVulnerabilities}[/]");
        grid.AddRow("[bold]Secrets Found:[/]", $"[yellow]{report.Summary.TotalSecrets}[/]");
        grid.AddRow("[bold]Outdated Packages:[/]", $"[cyan]{report.Summary.OutdatedPackages}[/]");
        grid.AddRow("[bold]Criticality Score:[/]", GetScoreColor(report.Summary.CriticalityScore));

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        if (report.Summary.VulnerabilitiesBySeverity.Any())
        {
            AnsiConsole.MarkupLine("[bold]Vulnerabilities by severity:[/]");
            foreach (var kvp in report.Summary.VulnerabilitiesBySeverity)
            {
                var color = kvp.Key switch
                {
                    SeverityLevel.Critical => "red",
                    SeverityLevel.High => "orange3",
                    SeverityLevel.Medium => "yellow",
                    _ => "blue"
                };
                AnsiConsole.MarkupLine($"  [{color}]{kvp.Key}:[/] {kvp.Value}");
            }
        }
    }

    private string GetScoreColor(double score)
    {
        if (score >= 70) return $"[red]{score:F1}%[/]";
        if (score >= 40) return $"[yellow]{score:F1}%[/]";
        if (score > 0) return $"[green]{score:F1}%[/]";
        return "[green]0%[/]";
    }

    public void WriteBanner()
    {
        var banner = @"
    ____        __             __  ___             ___ __ 
   / __ \____  / /_____  ___  / /_/   | __  ______/ (_) /_
  / / / / __ \/ __/ __ \/ _ \/ __/ /| |/ / / / __  / / __/
 / /_/ / /_/ / /_/ / / /  __/ /_/ ___ / /_/ / /_/ / / /_  
/_____/\____/\__/_/ /_/\___/\__/_/  |_\__,_/\__,_/_/\__/  
";
        AnsiConsole.Write(new Panel(banner)
        {
            Border = BoxBorder.Double,
            BorderStyle = Style.Parse("blue")
        });
    }
}