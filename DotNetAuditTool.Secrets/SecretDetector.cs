using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.Secrets;

public class SecretDetector
{
    private readonly List<SecretMatch> _foundSecrets;

    public SecretDetector()
    {
        _foundSecrets = new List<SecretMatch>();
    }

    public async Task<SecretScanResult> ScanAsync(string targetPath, IEnumerable<string>? ignoreFilePaths = null)
    {
        Console.WriteLine($"Starting secret scan on: {targetPath}");

        var secrets = new List<SecretMatch>();

        var fileScanner = new FileScanner(ignoreFilePaths);

        if (File.Exists(targetPath))
        {
            var fileSecrets = await fileScanner.ScanFileAsync(targetPath);
            secrets.AddRange(fileSecrets);
        }
        else if (Directory.Exists(targetPath))
        {
            var dirSecrets = await fileScanner.ScanDirectoryAsync(targetPath);
            secrets.AddRange(dirSecrets);
        }
        else
        {
            throw new ArgumentException($"Path not found: {targetPath}");
        }

        var result = new SecretScanResult
        {
            ScanTime = DateTime.UtcNow,
            TargetPath = targetPath,
            TotalFilesScanned = GetFileCount(targetPath),
            FoundSecrets = secrets,
            SecretsByType = secrets.GroupBy(s => s.Type)
                .ToDictionary(g => g.Key, g => g.Count()),
            FilesWithSecrets = secrets.Select(s => s.FilePath).Distinct().ToList()
        };

        result.RiskLevel = CalculateRiskLevel(secrets);
        result.Summary = GenerateSummary(result);

        Console.WriteLine($"Secret scan complete. Found {secrets.Count} potential secrets " +
                         $"in {result.FilesWithSecrets.Count} files.");

        return result;
    }

    private int GetFileCount(string path)
    {
        if (File.Exists(path))
            return 1;

        if (Directory.Exists(path))
        {
            var extensions = new[] { ".cs", ".json", ".xml", ".yaml", ".yml", ".config", ".txt", ".env", ".js", ".ts" };
            var files = extensions.SelectMany(ext =>
                Directory.GetFiles(path, $"*{ext}", SearchOption.AllDirectories));
            return files.Count();
        }

        return 0;
    }

    private SecretRiskLevel CalculateRiskLevel(List<SecretMatch> secrets)
    {
        if (!secrets.Any())
            return SecretRiskLevel.None;

        var highRiskTypes = new[]
        {
            SecretType.PrivateKey, SecretType.AwsKey, SecretType.AccessToken,
            SecretType.Password, SecretType.JwtToken
        };

        var criticalSecrets = secrets.Count(s => highRiskTypes.Contains(s.Type) && s.Entropy > 5.5);

        if (criticalSecrets > 0)
            return SecretRiskLevel.Critical;

        if (secrets.Count(s => highRiskTypes.Contains(s.Type)) > 0)
            return SecretRiskLevel.High;

        if (secrets.Count > 5)
            return SecretRiskLevel.Medium;

        return SecretRiskLevel.Low;
    }

    private string GenerateSummary(SecretScanResult result)
    {
        if (result.FoundSecrets.Count == 0)
            return "No secrets found. Good job!";

        var summary = $"Found {result.FoundSecrets.Count} potential secrets:\n";

        foreach (var type in result.SecretsByType)
        {
            summary += $"  - {type.Key}: {type.Value}\n";
        }

        if (result.RiskLevel == SecretRiskLevel.Critical)
            summary += "\nCRITICAL: Immediate action required! Real credentials may be exposed.";
        else if (result.RiskLevel == SecretRiskLevel.High)
            summary += "\nHIGH: Potential security risk detected.";
        else
            summary += "\nReview these findings and remove any real secrets from code.";

        return summary;
    }
}
