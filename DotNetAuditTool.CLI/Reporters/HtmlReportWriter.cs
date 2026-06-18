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
        sb.Append("</title>");

        sb.Append("""
                  <style>
                      body{font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; color:#111; background:#fff; margin:0; padding:20px; line-height:1.5}
                      main{max-width:1100px;margin:0 auto}
                      h1{font-size:1.8rem;margin:0 0 0.5rem;color:#0b3d91}
                      h2{font-size:1rem;margin:0;color:#1f4e79;display:inline-block}
                      section{padding:10px 0}
                      p,li{font-size:0.95rem;color:#222}
                      ul{margin:0 0 0 1.2rem}
                      code,pre{font-family: Consolas, 'Courier New', monospace; background:#f6f8fa; padding:2px 6px;border-radius:4px}
                      table{border-collapse:collapse;width:100%;margin:8px 0}
                      th,td{border:1px solid #e6e6e6;padding:6px 8px;text-align:left}
                      .small{color:#666;font-size:0.85rem}
                          .card{border:1px solid #e8eef8;border-radius:8px;padding:10px;margin:10px 0;background:#ffffff;box-shadow:0 1px 2px rgba(16,24,40,0.03)}
                          .card.even{background:#fbfcff}
                          .card.odd{background:#ffffff}
                      .card-header{display:flex;align-items:center;gap:8px;margin-bottom:8px}
                      .toggle{background:transparent;border:none;font-size:1rem;cursor:pointer;color:#1f4e79}
                      .card-body{padding-left:6px}
                      </style>
                  """);

        sb.Append("""
                  <script>
                      function toggle(btn){
                        const header = btn.parentElement;
                        const body = header.nextElementSibling;
                        if(!body) return;
                        const hidden = body.style.display === 'none';
                        body.style.display = hidden ? 'block' : 'none';
                        btn.textContent = hidden ? '▾' : '▸';
                      }
                      </script>
                  """);

        sb.Append("</head><body><main>");
        sb.Append("<h1>");
        sb.Append(Escape(title));
        sb.Append("</h1>");
        AppendElement(sb, root, 0);
        sb.Append("</main></body></html>");
        return sb.ToString();
    }

    private void AppendElement(StringBuilder sb, JsonElement element, int depth = 0)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append("<div>");
                int idx = 0;
                foreach (var property in element.EnumerateObject())
                {
                    var parityClass = depth == 0 ? (idx % 2 == 0 ? " even" : " odd") : string.Empty;
                    sb.Append($"<article class=\"card{parityClass}\">");
                    sb.Append("<header class=\"card-header\">");
                    var expanded = depth == 0;
                    sb.Append($"<button class=\"toggle\" onclick=\"toggle(this)\">{(expanded ? '▾' : '▸')}</button>");
                    sb.Append("<h2>");
                    sb.Append(Escape(property.Name));
                    sb.Append("</h2>");
                    sb.Append("</header>");
                    var bodyStyle = expanded ? "display:block" : "display:none";
                    sb.Append($"<div class=\"card-body\" style=\"{bodyStyle}\">");
                    AppendElement(sb, property.Value, depth + 1);
                    sb.Append("</div>");
                    sb.Append("</article>");
                    idx++;
                }

                sb.Append("</div>");
                break;
            case JsonValueKind.Array:
                sb.Append("<div>");
                int i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    sb.Append("<article class=\"card\">");
                    sb.Append("<header class=\"card-header\">");
                    sb.Append($"<button class=\"toggle\" onclick=\"toggle(this)\">▸</button>");
                    sb.Append($"<h2>Item {i + 1}</h2>");
                    sb.Append("</header>");
                    sb.Append("<div class=\"card-body\" style=\"display:none\">");
                    AppendElement(sb, item, depth + 1);
                    sb.Append("</div>");
                    sb.Append("</article>");
                    i++;
                }

                sb.Append("</div>");
                break;
            case JsonValueKind.String:
                sb.Append("<p>");
                sb.Append(Escape(element.GetString()));
                sb.Append("</p>");
                break;
            case JsonValueKind.Number:
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