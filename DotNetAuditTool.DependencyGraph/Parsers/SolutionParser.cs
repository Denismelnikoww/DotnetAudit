using DotNetAuditTool.Core.Models;
using System.Text.RegularExpressions;

namespace DotNetAuditTool.DependencyGraphBuilder.Parsers;

public class SolutionParser
{
    private static readonly Regex ProjectRegex = new(
        @"Project\(""\{(?<typeGuid>[^}]+)}""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+)"",\s*""\{(?<guid>[^}]+)}""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public async Task<List<ProjectInfo>> ParseSolutionAsync(string solutionPath)
    {
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        var content = await File.ReadAllTextAsync(solutionPath);
        return ParseSolution(content, Path.GetDirectoryName(solutionPath) ?? string.Empty);
    }

    public List<ProjectInfo> ParseSolution(string content, string solutionDirectory)
    {
        var projects = new List<ProjectInfo>();
        var matches = ProjectRegex.Matches(content);

        var projectParser = new ProjectFileParser();

        foreach (Match match in matches)
        {
            var relativePath = match.Groups["path"].Value;

            if (!relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                && !relativePath.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)
                && !relativePath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
                continue;

            var fullPath = Path.GetFullPath(Path.Combine(solutionDirectory, relativePath));

            if (File.Exists(fullPath))
            {
                try
                {
                    var projectInfo = projectParser.ParseAsync(fullPath).Result;
                    projects.Add(projectInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to parse {fullPath}: {ex.Message}");
                }
            }
        }

        return projects;
    }
}