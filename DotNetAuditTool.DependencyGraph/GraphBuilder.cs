using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder.Parsers;

namespace DotNetAuditTool.DependencyGraphBuilder;

public class GraphBuilder
{
    private readonly ProjectFileParser _projectFileParser;
    private readonly Dictionary<string, ProjectInfo> _projectCache;

    public GraphBuilder()
    {
        _projectFileParser = new ProjectFileParser();
        _projectCache = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<DependencyGraph> BuildFromProjectAsync(string csprojPath)
    {
        var project = await _projectFileParser.ParseAsync(csprojPath);
        return await BuildGraphAsync(new List<ProjectInfo> { project });
    }

    public async Task<DependencyGraph> BuildFromSolutionAsync(string solutionPath)
    {
        var solutionParser = new SolutionParser();
        var projects = await solutionParser.ParseSolutionAsync(solutionPath);
        return await BuildGraphAsync(projects);
    }

    public async Task<DependencyGraph> BuildFromDirectoryAsync(string directoryPath)
    {
        var projectFiles = new List<string>();
        projectFiles.AddRange(Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories));
        projectFiles.AddRange(Directory.GetFiles(directoryPath, "*.vbproj", SearchOption.AllDirectories));
        projectFiles.AddRange(Directory.GetFiles(directoryPath, "*.fsproj", SearchOption.AllDirectories));

        var projects = new List<ProjectInfo>();

        foreach (var csproj in projectFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var project = await _projectFileParser.ParseAsync(csproj);
                projects.Add(project);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse {csproj}: {ex.Message}");
            }
        }

        return await BuildGraphAsync(projects);
    }

    private async Task<DependencyGraph> BuildGraphAsync(List<ProjectInfo> projects)
    {
        var graph = new DependencyGraph();
        var nodeDict = new Dictionary<string, DependencyNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            _projectCache[project.FilePath] = project;
        }

        foreach (var project in projects)
        {
            var node = CreateProjectNode(project, nodeDict);
            nodeDict[project.FilePath] = node;
            graph.Nodes.Add(node);
        }

        foreach (var project in projects)
        {
            if (!nodeDict.TryGetValue(project.FilePath, out var projectNode))
                continue;

            foreach (var package in project.Packages)
            {
                var packageNode = GetOrCreatePackageNode(nodeDict, package);
                if (!graph.Nodes.Contains(packageNode))
                    graph.Nodes.Add(packageNode);

                projectNode.Dependencies.Add(packageNode);
                projectNode.DependencyIds.Add(packageNode.Id);

                var edge = new DependencyEdge
                {
                    Source = projectNode.Id,
                    Target = packageNode.Id,
                    VersionRange = package.Version
                };
                graph.Edges.Add(edge);

                if (!graph.AdjacencyList.ContainsKey(projectNode.Id))
                    graph.AdjacencyList[projectNode.Id] = new List<string>();
                graph.AdjacencyList[projectNode.Id].Add(packageNode.Id);
            }

            foreach (var projectRef in project.ProjectReferences)
            {
                var normalizedRefPath = Path.GetFullPath(projectRef.Path).ToLowerInvariant();
                string? targetKey = null;
                DependencyNode? refNode = null;

                foreach (var kvp in nodeDict)
                {
                    if (kvp.Key != null && Path.GetFullPath(kvp.Key).ToLowerInvariant() == normalizedRefPath)
                    {
                        targetKey = kvp.Key;
                        refNode = kvp.Value;
                        break;
                    }
                }

                if (refNode != null)
                {
                    projectNode.Dependencies.Add(refNode);
                    projectNode.DependencyIds.Add(refNode.Id);

                    var edge = new DependencyEdge
                    {
                        Source = projectNode.Id,
                        Target = refNode.Id
                    };
                    graph.Edges.Add(edge);

                    if (!graph.AdjacencyList.ContainsKey(projectNode.Id))
                        graph.AdjacencyList[projectNode.Id] = new List<string>();
                    graph.AdjacencyList[projectNode.Id].Add(refNode.Id);
                }
            }
        }

        graph.Nodes = graph.Nodes.DistinctBy(n => n.Id).ToList();

        return graph;
    }

    private DependencyNode CreateProjectNode(ProjectInfo project, Dictionary<string, DependencyNode> nodeDict)
    {
        var node = new DependencyNode
        {
            Id = Guid.NewGuid().ToString(),
            Name = project.Name,
            Type = DependencyType.ProjectReference,
            Version = project.TargetFramework,
            Metadata = new Dictionary<string, object>
            {
                ["Path"] = project.FilePath,
                ["TargetFramework"] = project.TargetFramework,
                ["TargetFrameworks"] = project.TargetFrameworks,
                ["Packages"] = project.Packages,
                ["ProjectReferences"] = project.ProjectReferences
            },
            Dependencies = new List<DependencyNode>(),
            DependencyIds = new List<string>()
        };

        nodeDict[project.FilePath] = node;
        return node;
    }

    private DependencyNode GetOrCreatePackageNode(Dictionary<string, DependencyNode> nodeDict, PackageReference package)
    {
        var nodeId = $"{package.Name}@{package.Version}";

        if (nodeDict.TryGetValue(nodeId, out var existingNode))
            return existingNode;

        var newNode = new DependencyNode
        {
            Id = nodeId,
            Name = package.Name,
            Version = package.Version,
            Type = DependencyType.NuGet,
            Metadata = new Dictionary<string, object>
            {
                ["IsDevelopmentDependency"] = package.IsDevelopmentDependency,
                ["IsPrivateAssets"] = package.IsPrivateAssets
            },
            Dependencies = new List<DependencyNode>(),
            DependencyIds = new List<string>()
        };

        nodeDict[nodeId] = newNode;
        return newNode;
    }

    private static List<ProjectInfo> GetProjectsFromGraph(DependencyGraph graph)
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