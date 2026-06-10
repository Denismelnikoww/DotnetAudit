namespace DotNetAuditTool.Core.Models;

public class DependencyEdge
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? VersionRange { get; set; }
}