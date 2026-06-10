namespace DotNetAuditTool.Core.Models;

public class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string TargetFramework { get; set; } = string.Empty;
    public List<string> TargetFrameworks { get; set; } = new();
    public List<PackageReference> Packages { get; set; } = new();
    public List<ProjectReference> ProjectReferences { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}
