using System.CommandLine;
using DotNetAuditTool.CLI.Commands;

namespace DotNetAuditTool.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("DotNetAuditTool - Comprehensive audit tool for .NET projects");

        rootCommand.AddCommand(AnalyzeCommand.Create());
        rootCommand.AddCommand(GraphCommand.Create());
        rootCommand.AddCommand(CheckVersionsCommand.Create());
        rootCommand.AddCommand(ScanSecretsCommand.Create());
        rootCommand.AddCommand(CheckVulnerabilitiesCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}