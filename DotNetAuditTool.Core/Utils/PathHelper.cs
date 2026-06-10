namespace DotNetAuditTool.Core.Utils;

public static class PathHelper
{
    public static bool IsSolutionFile(string path) =>
        Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase);

    public static bool IsProjectFile(string path) =>
        Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase);

    public static bool IsSourceFile(string path) =>
        Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<string> FindAllProjects(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
    }

    public static IEnumerable<string> FindAllSolutions(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*.sln", SearchOption.AllDirectories);
    }
}
