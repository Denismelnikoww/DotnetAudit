using DotNetAuditTool.CLI.Reporters;
using System.CommandLine;
using DotNetAuditTool.Secrets;
using DotNetAuditTool.CLI.Services;

namespace DotNetAuditTool.CLI.Commands;

public static class ScanSecretsCommand
{
    public static Command Create()
    {
        var command = new Command("scan-secrets", "Search for secrets and sensitive data in code");

        var pathArg = new Argument<string>("path", "Path to file or directory to scan");
        pathArg.AddValidator(result =>
        {
            var pathVal = result.GetValueOrDefault<string>();
            var path = !string.IsNullOrWhiteSpace(pathVal) ? pathVal : Environment.CurrentDirectory;
            if (string.IsNullOrWhiteSpace(path))
            {
                result.ErrorMessage = "Path cannot be empty.";
            }
        });
        var outputOption = new Option<string>(["--output", "-o"], "Output file for results");

        command.AddArgument(pathArg);
        command.AddOption(outputOption);

        command.SetHandler(ExecuteScanSecretsCommand, pathArg, outputOption);

        return command;
    }

    private static async Task<int> ExecuteScanSecretsCommand(string path, string? output)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Environment.CurrentDirectory;
        }

        var console = new ConsoleOutputService();
        console.WriteHeader($"Scanning for secrets in: {path}");

        try
        {
            var configurationService = new ConfigurationService();
            var settings = configurationService.Load();

            var detector = new SecretDetector(settings.EntropyThreshold);
            var reportFullPath = string.IsNullOrEmpty(output) ? null : Path.GetFullPath(output);
            var result = await detector.ScanAsync(path, reportFullPath is null ? null : new[] { reportFullPath });

            console.WriteSecretsTable(result.FoundSecrets);

            console.WriteInfo($"Scanned {result.TotalFilesScanned} files");
            console.WriteInfo($"Found {result.FoundSecrets.Count} potential secrets in {result.FilesWithSecrets.Count} files");

            var riskColor = result.RiskLevel switch
            {
                SecretRiskLevel.Critical => "red",
                _ => "green"
            };
            console.WriteInfo($"Risk level: [{riskColor}]{result.RiskLevel}[/]\n");

            console.WriteInfo(result.Summary);

            if (!string.IsNullOrEmpty(output))
            {
                var limitedResult = new SecretScanResult
                {
                    ScanTime = result.ScanTime,
                    TargetPath = result.TargetPath,
                    TotalFilesScanned = result.TotalFilesScanned,
                    FoundSecrets = result.FoundSecrets.Take(1000).ToList(),
                    SecretsByType = result.SecretsByType,
                    FilesWithSecrets = result.FilesWithSecrets,
                    RiskLevel = result.RiskLevel,
                    Summary = result.Summary
                };

                IReportWriter<SecretScanResult> reportWriter = ReportWriterFactory.CreateByExtension<SecretScanResult>(output);
                await reportWriter.WriteAsync(limitedResult, output);
                console.WriteSuccess($"Results saved to {output}");
                if (result.FoundSecrets.Count > 1000)
                {
                    console.WriteWarning($"Note: report limited to 1000 of {result.FoundSecrets.Count} secrets to prevent memory issues");
                }
            }

            if (result.RiskLevel != SecretRiskLevel.None)
            {
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            console.WriteError($"Secret scan failed: {ex.Message}");
            return 1;
        }
    }
}