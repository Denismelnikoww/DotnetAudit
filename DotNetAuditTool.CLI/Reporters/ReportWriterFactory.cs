using System.Text.Json;

namespace DotNetAuditTool.CLI.Reporters;

public static class ReportWriterFactory
{
    public static IReportWriter<T> CreateJson<T>(JsonSerializerOptions? options = null)
    {
        return new JsonReportWriter<T>(options);
    }
}
