using System.IO;
using System.Text;
using System.Text.Json;

namespace DotNetAuditTool.CLI.Reporters;

public class HtmlReportWriter<T> : IReportWriter<T>
{
    private readonly JsonSerializerOptions _options;

    public HtmlReportWriter(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public string Serialize(T report)
    {
        var element = JsonSerializer.SerializeToElement(report, _options);
        return BuildHtml(element, typeof(T).Name);
    }

    public async Task WriteAsync(T report, string outputPath)
    {
        var html = Serialize(report);
        await File.WriteAllTextAsync(outputPath, html);
    }

    private string BuildHtml(JsonElement root, string title)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>");
        sb.Append(Escape(title));
        sb.Append("</title></head><body>");
        sb.Append("<h1>");
        sb.Append(Escape(title));
        sb.Append("</h1>");
        AppendElement(sb, root);
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private void AppendElement(StringBuilder sb, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append("<div>");
                foreach (var property in element.EnumerateObject())
                {
                    sb.Append("<section>");
                    sb.Append("<h2>");
                    sb.Append(Escape(property.Name));
                    sb.Append("</h2>");
                    AppendElement(sb, property.Value);
                    sb.Append("</section>");
                }
                sb.Append("</div>");
                break;
            case JsonValueKind.Array:
                sb.Append("<ul>");
                foreach (var item in element.EnumerateArray())
                {
                    sb.Append("<li>");
                    AppendElement(sb, item);
                    sb.Append("</li>");
                }
                sb.Append("</ul>");
                break;
            case JsonValueKind.String:
                sb.Append("<p>");
                sb.Append(Escape(element.GetString()));
                sb.Append("</p>");
                break;
            case JsonValueKind.Number:
                sb.Append("<p>");
                sb.Append(Escape(element.GetRawText()));
                sb.Append("</p>");
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append("<p>");
                sb.Append(Escape(element.GetRawText()));
                sb.Append("</p>");
                break;
            case JsonValueKind.Null:
                sb.Append("<p>null</p>");
                break;
            default:
                sb.Append("<p>");
                sb.Append(Escape(element.GetRawText()));
                sb.Append("</p>");
                break;
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&#39;");
    }
}
