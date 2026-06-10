namespace DotNetAuditTool.Core.Models;

public class DependencyGraph
{
    public List<DependencyNode> Nodes { get; set; } = new();
    public List<DependencyEdge> Edges { get; set; } = new();
    public Dictionary<string, List<string>> AdjacencyList { get; set; } = new();

    public List<DependencyNode> GetRootNodes()
    {
        var allChildren = Edges.Select(e => e.Target).ToHashSet();
        return Nodes.Where(n => !allChildren.Contains(n.Id)).ToList();
    }
}
