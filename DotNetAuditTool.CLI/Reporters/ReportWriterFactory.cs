using System.IO;
using System.Text.Json;

namespace DotNetAuditTool.CLI.Reporters;

public static class ReportWriterFactory
{
    public static IReportWriter<T> CreateJson<T>(JsonSerializerOptions? options = null)
    {
        return new JsonReportWriter<T>(options);
    }

    public static IReportWriter<T> CreateXml<T>()
    {
        return new XmlReportWriter<T>();
    }

    public static IReportWriter<T> CreateByExtension<T>(string outputPath)
    {
        var extension = Path.GetExtension(outputPath).ToLowerInvariant();

        return extension switch
        {
            ".xml" => CreateXml<T>(),
            ".html" or ".htm" => CreateHtml<T>(),
            _ => CreateJson<T>(),
        };
    }

    public static IReportWriter<T> CreateHtml<T>(JsonSerializerOptions? options = null)
    {
        return new HtmlReportWriter<T>(options);
    }
}
