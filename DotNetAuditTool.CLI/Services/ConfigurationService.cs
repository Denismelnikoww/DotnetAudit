using System.Text.Json;

namespace DotNetAuditTool.CLI.Services;

public class ConfigurationService
{
    private const string DefaultConfigFileName = ".dotnetaudittool.json";
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    public string ConfigFilePath { get; }

    public ConfigurationService(string? configFilePath = null)
    {
        ConfigFilePath = configFilePath ?? Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);
    }

    public ApplicationSettings Load()
    {
        if (!File.Exists(ConfigFilePath))
            return new ApplicationSettings();

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            var settings = JsonSerializer.Deserialize<ApplicationSettings>(json, JsonOptions);
            return settings ?? new ApplicationSettings();
        }
        catch
        {
            return new ApplicationSettings();
        }
    }

    public void Save(ApplicationSettings settings)
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }
}

public sealed class ApplicationSettings
{
    public double EntropyThreshold { get; set; } = 4.5;
}
