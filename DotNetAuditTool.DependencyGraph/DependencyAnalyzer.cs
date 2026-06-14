using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.DependencyGraphBuilder;

public class DependencyAnalyzer
{
    private readonly GraphBuilder _graphBuilder;

    public DependencyAnalyzer()
    {
        _graphBuilder = new GraphBuilder();
    }

    public async Task<DependencyGraph> AnalyzeAsync(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var extension = Path.GetExtension(targetPath).ToLower();
            if (extension == ".csproj")
            {
                return await _graphBuilder.BuildFromProjectAsync(targetPath);
            }
            else if (extension.StartsWith(".sln"))
            {
                return await _graphBuilder.BuildFromSolutionAsync(targetPath);
            }
        }
        else if (Directory.Exists(targetPath))
        {
            return await _graphBuilder.BuildFromDirectoryAsync(targetPath);
        }

        throw new ArgumentException($"Invalid target path: {targetPath}");
    }

    public List<string> FindCircularDependencies(DependencyGraph graph)
    {
        var cycles = new List<string>();
        var visited = new HashSet<string>();
        var path = new Stack<string>();

        foreach (var node in graph.Nodes)
        {
            if (!visited.Contains(node.Id))
            {
                DetectCycles(node.Id, graph, visited, path, cycles);
            }
        }

        return cycles;
    }

    private void DetectCycles(string nodeId, DependencyGraph graph, HashSet<string> visited,
                              Stack<string> path, List<string> cycles)
    {
        if (path.Contains(nodeId))
        {
            var cycle = string.Join(" -> ", path.SkipWhile(p => p != nodeId).Concat(new[] { nodeId }));
            cycles.Add(cycle);
            return;
        }

        if (visited.Contains(nodeId))
            return;

        visited.Add(nodeId);
        path.Push(nodeId);

        if (graph.AdjacencyList.TryGetValue(nodeId, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                DetectCycles(neighbor, graph, visited, path, cycles);
            }
        }

        path.Pop();
    }

    public Dictionary<string, int> GetDependencyDepth(DependencyGraph graph)
    {
        var depths = new Dictionary<string, int>();
        var roots = graph.GetRootNodes();

        foreach (var root in roots)
        {
            CalculateDepth(root.Id, graph, depths, 0);
        }

        return depths;
    }

    private void CalculateDepth(string nodeId, DependencyGraph graph, Dictionary<string, int> depths, int currentDepth)
    {
        if (!depths.ContainsKey(nodeId) || depths[nodeId] < currentDepth)
        {
            depths[nodeId] = currentDepth;
        }

        if (graph.AdjacencyList.TryGetValue(nodeId, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                CalculateDepth(neighbor, graph, depths, currentDepth + 1);
            }
        }
    }
}