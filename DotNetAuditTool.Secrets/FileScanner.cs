using DotNetAuditTool.Core.Models;
using System.Text.RegularExpressions;

namespace DotNetAuditTool.Secrets;

public class FileScanner
{
    private readonly List<string> _ignorePatterns;
    private readonly HashSet<string> _ignoreFileNames;
    private readonly List<string> _allowedExtensions;
    private readonly List<string> _ignoreDirectories;
    private readonly double _entropyThreshold;

    public FileScanner(IEnumerable<string>? ignoreFileNames = null, double entropyThreshold = 4.5)
    {
        _entropyThreshold = entropyThreshold;
        _ignorePatterns = new List<string>
        {
            "*.exe", "*.dll", "*.pdb", "*.bin", "*.obj",
            "*.jpg", "*.png", "*.gif", "*.ico", "*.svg",
            "*.mp3", "*.mp4", "*.wav", "*.avi",
            "*.zip", "*.rar", "*.7z", "*.tar", "*.gz",
            "*.pdf", "*.doc", "*.docx", "*.xls", "*.xlsx"
        };

        _ignoreFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ignoreFileNames != null)
        {
            foreach (var f in ignoreFileNames)
            {
                if (!string.IsNullOrWhiteSpace(f))
                    _ignoreFileNames.Add(Path.GetFileName(f));
            }
        }

        _allowedExtensions = new List<string>
        {
            ".cs", ".vb", ".fs", ".json", ".xml", ".yaml", ".yml",
            ".config", ".txt", ".md", ".ps1", ".sh", ".bat", ".cmd",
            ".js", ".ts", ".jsx", ".tsx", ".html", ".css", ".scss",
            ".env", ".ini", ".cfg", ".conf", ".properties"
        };

        _ignoreDirectories = new List<string>
        {
            "bin", "obj", "node_modules", ".git", ".vs", ".idea",
            "packages", "TestResults", "coverage", ".github"
        };
    }

    public async Task<List<SecretMatch>> ScanDirectoryAsync(string directoryPath)
    {
        var allSecrets = new List<SecretMatch>();
        var files = GetFilesToScan(directoryPath);

        Console.WriteLine($"Scanning {files.Count} files for secrets...");

        var semaphore = new SemaphoreSlim(10);
        var tasks = files.Select(async file =>
        {
            await semaphore.WaitAsync();
            try
            {
                var secrets = await ScanFileAsync(file);
                return secrets;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        allSecrets.AddRange(results.SelectMany(r => r));

        return allSecrets;
    }

    public async Task<List<SecretMatch>> ScanFileAsync(string filePath)
    {
        var secrets = new List<SecretMatch>();

        if (!ShouldScanFile(filePath))
            return secrets;

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineSecrets = DetectSecretsInLine(line, filePath, i + 1);
                secrets.AddRange(lineSecrets);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning file {filePath}: {ex.Message}");
        }

        return secrets;
    }

    private List<string> GetFilesToScan(string directoryPath)
    {
        var files = new List<string>();

        foreach (var extension in _allowedExtensions)
        {
            var pattern = $"*{extension}";
            var matchedFiles = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

            foreach (var file in matchedFiles)
            {
                if (!ShouldIgnoreFile(file))
                    files.Add(file);
            }
        }

        return files;
    }

    private bool ShouldScanFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return _allowedExtensions.Contains(extension) && !ShouldIgnoreFile(filePath);
    }

    private bool ShouldIgnoreFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        if (_ignoreFileNames != null && _ignoreFileNames.Contains(fileName))
            return true;

        foreach (var pattern in _ignorePatterns)
        {
            if (fileName.EndsWith(pattern.TrimStart('*')))
                return true;
        }

        var directoryPath = Path.GetDirectoryName(filePath) ?? string.Empty;
        var directories = directoryPath.Split(Path.DirectorySeparatorChar);

        foreach (var dir in directories)
        {
            if (_ignoreDirectories.Contains(dir, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private List<SecretMatch> DetectSecretsInLine(string line, string filePath, int lineNumber)
    {
        var secrets = new List<SecretMatch>();

        foreach (var (pattern, secretType, ruleName) in RegexPatterns.All)
        {
            var matches = pattern.Matches(line);

            foreach (Match match in matches)
            {
                string? secretValue = match.Groups.Count > 2 ? match.Groups[2].Value : match.Value;

                if (string.IsNullOrEmpty(secretValue))
                    secretValue = match.Value;

                var entropy = EntropyAnalyzer.CalculateShannonEntropy(secretValue);
                var isSuspicious = EntropyAnalyzer.IsSuspiciousSecret(secretValue, secretType, _entropyThreshold);
                var hasRepeatPatterns = EntropyAnalyzer.HasRepeatPatterns(secretValue);

                if (IsFalsePositive(secretValue, line, secretType))
                    continue;

                if (entropy < _entropyThreshold && hasRepeatPatterns)
                    continue;

                secrets.Add(new SecretMatch
                {
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    LineContent = line.Trim(),
                    Type = secretType,
                    MatchedValue = MaskSecret(secretValue),
                    Entropy = entropy,
                    Rule = ruleName
                });
            }
        }

        return secrets;
    }

    private bool IsFalsePositive(string value, string line, SecretType type)
    {
        if (line.Contains("test") || line.Contains("example") || line.Contains("sample"))
            return true;

        if (line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("#") || line.TrimStart().StartsWith("/*"))
            return true;

        if (line.Contains("Log") && type != SecretType.Password)
            return true;

        var placeholders = new[] { "your-api-key", "your-key", "changeme", "TODO" };
        foreach (var placeholder in placeholders)
        {
            if (value.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return new string('*', secret.Length);

        var start = secret.Substring(0, 4);
        var end = secret.Substring(secret.Length - 4, 4);
        var middleLength = Math.Min(secret.Length - 8, 10);

        return $"{start}{new string('*', middleLength)}{end}";
    }
}