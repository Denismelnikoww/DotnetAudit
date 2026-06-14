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
            // Optionally, validate if the path exists here if desired
        });
        var entropyThresholdOption = new Option<double>(["--entropy-threshold", "-e"],
            () => 4.5, "Entropy threshold for detection (0-8)");
        var outputOption = new Option<string>(["--output", "-o"], "Output file for results");

        command.AddArgument(pathArg);
        command.AddOption(entropyThresholdOption);
        command.AddOption(outputOption);

        command.SetHandler(async (string path, double entropyThreshold, string? output) =>
        {
            // Use current directory if path is empty or whitespace
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Environment.CurrentDirectory;
            }

            var console = new ConsoleOutputService();
            console.WriteHeader($"Scanning for secrets in: {path}");

            try
            {
                var detector = new SecretDetector();
                var reportFullPath = string.IsNullOrEmpty(output) ? null : Path.GetFullPath(output);
                var result = await detector.ScanAsync(path, reportFullPath is null ? null : new[] { reportFullPath });

                console.WriteSecretsTable(result.FoundSecrets);

                // Summary
                console.WriteInfo($"Scanned {result.TotalFilesScanned} files");
                console.WriteInfo($"Found {result.FoundSecrets.Count} potential secrets in {result.FilesWithSecrets.Count} files");

                var riskColor = result.RiskLevel switch
                {
                    SecretRiskLevel.Critical => "red",
                    SecretRiskLevel.High => "orange3",
                    SecretRiskLevel.Medium => "yellow",
                    _ => "green"
                };
                console.WriteInfo($"Risk level: [{riskColor}]{result.RiskLevel}[/]\n");

                console.WriteInfo(result.Summary);

                if (!string.IsNullOrEmpty(output))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(output, json);
                    console.WriteSuccess($"Results saved to {output}");
                }

                if (result.RiskLevel >= SecretRiskLevel.High)
                {
                    Environment.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                console.WriteError($"Secret scan failed: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }, pathArg, entropyThresholdOption, outputOption);

        return command;
    }
}