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

    public List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
    {
        // Сначала создаем уникальные ProjectInfo на основе узлов проектов
        var uniqueProjects = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase); // Key: FilePath
        var nodeById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase); // Для быстрого поиска зависимостей по ID

        foreach (var node in graph.Nodes.Where(n => n.Type == DependencyType.ProjectReference))
        {
            if (node.Metadata.TryGetValue("Path", out var pathObj) && pathObj != null)
            {
                var filePath = pathObj.ToString();
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    // Создаем ProjectInfo только если его еще нет
                    if (!uniqueProjects.ContainsKey(filePath))
                    {
                        var project = new ProjectInfo
                        {
                            Name = node.Name,
                            FilePath = filePath,
                            TargetFramework = node.Metadata.TryGetValue("TargetFramework", out var tf)
                                ? tf.ToString() ?? node.Version
                                : node.Version,
                            Packages = new List<PackageReference>(),      // Инициализируем списки
                            ProjectReferences = new List<ProjectReference>() // Инициализируем списки
                        };
                        uniqueProjects[filePath] = project;
                    }
                }
            }
        }

        // Теперь обходим все узлы проектов снова, чтобы добавить их зависимости к соответствующему уникальному ProjectInfo
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
                                // Проверяем, нет ли уже такой зависимости (во избежание дублей если в графе были)
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
                                    // Проверяем, нет ли уже такой зависимости (во избежание дублей если в графе были)
                                    var projRef = new ProjectReference
                                    {
                                        Name = depNode.Name,
                                        Path = refPath,
                                        RelativePath = refPath // Можно вычислить относительный путь, если нужно
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

        return uniqueProjects.Values.ToList(); // Возвращаем список уникальных ProjectInfo
    }

}