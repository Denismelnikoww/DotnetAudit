using DotNetAuditTool.Core.Models;
using DotNetAuditTool.DependencyGraphBuilder.Parsers;

namespace DotNetAuditTool.DependencyGraphBuilder;

public class GraphBuilder
{
    private readonly CsProjParser _csProjParser;
    private readonly Dictionary<string, ProjectInfo> _projectCache;

    public GraphBuilder()
    {
        _csProjParser = new CsProjParser();
        _projectCache = new Dictionary<string, ProjectInfo>();
    }

    public async Task<DependencyGraph> BuildFromProjectAsync(string csprojPath)
    {
        var project = await _csProjParser.ParseAsync(csprojPath);
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
        var csprojFiles = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
        var projects = new List<ProjectInfo>();

        foreach (var csproj in csprojFiles)
        {
            try
            {
                var project = await _csProjParser.ParseAsync(csproj);
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
            var projectNode = nodeDict.Values.First(n => n.Name == project.Name);

            foreach (var package in project.Packages)
            {
                var packageNode = GetOrCreatePackageNode(nodeDict, package);
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
                if (_projectCache.TryGetValue(projectRef.Path, out var referencedProject))
                {
                    var refNode = nodeDict.Values.First(n => n.Name == referencedProject.Name);

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