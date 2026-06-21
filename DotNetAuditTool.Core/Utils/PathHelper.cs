using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DotNetAuditTool.Core.Utils;

public static class PathHelper
{
    public static bool IsSolutionFile(string path) =>
        Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase);

    public static bool IsProjectFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".vbproj", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".fsproj", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSourceFile(string path) =>
        Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<string> FindAllProjects(string rootPath)
    {
        var results = new List<string>();
        results.AddRange(Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories));
        results.AddRange(Directory.GetFiles(rootPath, "*.vbproj", SearchOption.AllDirectories));
        results.AddRange(Directory.GetFiles(rootPath, "*.fsproj", SearchOption.AllDirectories));
        return results.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> FindAllSolutions(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*.sln", SearchOption.AllDirectories);
    }
}
