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
        _projectCache = new Dictionary<string, ProjectInfo>();
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
        var nodeDict = new Dictionary<string, DependencyNode>();

        foreach (var project in projects)
        {
            _projectCache[project.FilePath] = project;
        }

        foreach (var project in projects)
        {
            var node = CreateProjectNode(project);
            nodeDict[node.Id] = node;
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
                if (_projectCache.TryGetValue(projectRef.Path, out var referencedProject) &&
                    nodeDict.TryGetValue(referencedProject.FilePath, out var refNode))
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

    private DependencyNode CreateProjectNode(ProjectInfo project)
    {
        return new DependencyNode
        {
            Id = project.FilePath,
            Name = project.Name,
            Version = project.TargetFramework,
            Type = DependencyType.ProjectReference,
            Metadata = new Dictionary<string, object>
            {
                ["Path"] = project.FilePath,
                ["Packages"] = project.Packages.Count,
                ["TargetFramework"] = project.TargetFramework
            }
        };
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
            }
        };

        nodeDict[nodeId] = newNode;
        return newNode;
    }
}