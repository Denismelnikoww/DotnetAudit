namespace DotNetAuditTool.Core.Models;

public class DependencyNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DependencyType Type { get; set; }
    public List<DependencyNode> Dependencies { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> DependencyIds { get; set; } = new();
}
