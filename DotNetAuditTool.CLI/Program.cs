using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using DotNetAuditTool.CLI.Commands;
using DotNetAuditTool.CLI.Services;
using Spectre.Console;

namespace DotNetAuditTool.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var rootCommand = new RootCommand("DotNetAuditTool - Comprehensive security and dependency auditing for .NET projects");

        rootCommand.AddCommand(AnalyzeCommand.Create());
        rootCommand.AddCommand(GraphCommand.Create());
        rootCommand.AddCommand(CheckVersionsCommand.Create());
        rootCommand.AddCommand(ScanSecretsCommand.Create());

        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseHelp()
            .UseVersionOption()
            .Build();

        try
        {
            return await parser.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            var console = new ConsoleOutputService();
            console.WriteError($"Unexpected error: {ex.Message}");

            if (ex.InnerException != null)
                console.WriteError($"Details: {ex.InnerException.Message}");

            return 1;
        }
    }

    private static void ShowHelp()
    {
        var console = new ConsoleOutputService();
        console.WriteBanner();

        var table = new Table();
        table.AddColumn("[cyan]Command[/]");
        table.AddColumn("[yellow]Description[/]");
        table.AddColumn("[green]Example[/]");

        table.AddRow("analyze", "Complete audit of .NET project", "dotnet-audit analyze ./src");
        table.AddRow("graph", "Build and visualize dependency graph", "dotnet-audit graph -f mermaid");
        table.AddRow("check-versions", "Check for outdated packages", "dotnet-audit check-versions --fix");
        table.AddRow("scan-secrets", "Search for secrets in code", "dotnet-audit scan-secrets --verbose");

        AnsiConsole.Write(table);

        console.WriteInfo("\nRun 'dotnet-audit <command> --help' for more information on a command.");
    }
}