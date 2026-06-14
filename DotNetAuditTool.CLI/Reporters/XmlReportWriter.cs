using System.IO;
using System.Xml.Serialization;

namespace DotNetAuditTool.CLI.Reporters;

public class XmlReportWriter<T> : IReportWriter<T>
{
    private readonly XmlSerializer _serializer;

    public XmlReportWriter()
    {
        _serializer = new XmlSerializer(typeof(T));
    }

    public string Serialize(T report)
    {
        using var stringWriter = new StringWriter();
        _serializer.Serialize(stringWriter, report);
        return stringWriter.ToString();
    }

    public async Task WriteAsync(T report, string outputPath)
    {
        var xml = Serialize(report);
        await File.WriteAllTextAsync(outputPath, xml);
    }
}
