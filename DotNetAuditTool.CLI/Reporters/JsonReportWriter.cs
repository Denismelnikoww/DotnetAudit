using System.IO;
using System.Text.Json;

namespace DotNetAuditTool.CLI.Reporters;

public class JsonReportWriter<T> : IReportWriter<T>
{
    private readonly JsonSerializerOptions _options;

    public JsonReportWriter(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public string Serialize(T report)
    {
        return JsonSerializer.Serialize(report, _options);
    }

    public async Task WriteAsync(T report, string outputPath)
    {
        var json = Serialize(report);
        await File.WriteAllTextAsync(outputPath, json);
    }
}
