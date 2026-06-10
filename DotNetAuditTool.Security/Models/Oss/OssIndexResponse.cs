namespace DotNetAuditTool.Security.Models.Oss;

public class OssIndexResponse
{
    public string Coordinates { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<OssVulnerability>? Vulnerabilities { get; set; }
}
