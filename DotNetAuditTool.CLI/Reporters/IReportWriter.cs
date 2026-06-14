namespace DotNetAuditTool.CLI.Reporters;

public interface IReportWriter<T>
{
    string Serialize(T report);
    Task WriteAsync(T report, string outputPath);
}
