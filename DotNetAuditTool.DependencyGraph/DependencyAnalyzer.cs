using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.DependencyGraphBuilder;

public class DependencyAnalyzer
{
    private readonly GraphBuilder _graphBuilder = new();


    public async Task<DependencyGraph> AnalyzeAsync(string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var extension = Path.GetExtension(targetPath).ToLower();
            if (extension == ".csproj" || extension == ".vbproj" || extension == ".fsproj")
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

    public List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
    {
        var uniqueProjects = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
        var nodeById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes.Where(n => n.Type == DependencyType.ProjectReference))
        {
            if (node.Metadata.TryGetValue("Path", out var pathObj) && pathObj != null)
            {
                var filePath = pathObj.ToString();
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    if (!uniqueProjects.ContainsKey(filePath))
                    {
                        var project = new ProjectInfo
                        {
                            Name = node.Name,
                            FilePath = filePath,
                            TargetFramework = node.Metadata.TryGetValue("TargetFramework", out var tf)
                                ? tf.ToString() ?? node.Version
                                : node.Version,
                            Packages = new List<PackageReference>(),
                            ProjectReferences = new List<ProjectReference>()
                        };
                        uniqueProjects[filePath] = project;
                    }
                }
            }
        }

        foreach (var node in graph.Nodes.Where(n => n.Type == DependencyType.ProjectReference))
        {
            if (node.Metadata.TryGetValue("Path", out var pathObj) && pathObj != null)
            {
                var projectPath = pathObj.ToString();
                if (!string.IsNullOrWhiteSpace(projectPath) && uniqueProjects.TryGetValue(projectPath, out var project))
                {
                    foreach (var depId in node.DependencyIds)
                    {
                        if (nodeById.TryGetValue(depId, out var depNode))
                        {
                            if (depNode.Type == DependencyType.NuGet)
                            {
                                var packageRef = new PackageReference
                                {
                                    Name = depNode.Name,
                                    Version = depNode.Version,
                                    IsDevelopmentDependency = depNode.Metadata.ContainsKey("IsDevelopmentDependency") &&
                                                            (bool)depNode.Metadata["IsDevelopmentDependency"],
                                    IsPrivateAssets = depNode.Metadata.ContainsKey("IsPrivateAssets") &&
                                                     (bool)depNode.Metadata["IsPrivateAssets"]
                                };
                                if (!project.Packages.Any(p => p.Name.Equals(packageRef.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    project.Packages.Add(packageRef);
                                }
                            }
                            else if (depNode.Type == DependencyType.ProjectReference)
                            {
                                var refPath = depNode.Metadata.TryGetValue("Path", out var refPathObj) ? refPathObj?.ToString() : null;
                                if (!string.IsNullOrWhiteSpace(refPath))
                                {
                                    var projRef = new ProjectReference
                                    {
                                        Name = depNode.Name,
                                        Path = refPath,
                                        RelativePath = refPath
                                    };
                                    if (!project.ProjectReferences.Any(pr => pr.Path.Equals(projRef.Path, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        project.ProjectReferences.Add(projRef);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return uniqueProjects.Values.ToList();
    }

}