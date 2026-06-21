namespace DotNetAuditTool.Core.Models;

public class ProjectCompatibilityIssue
{
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectTargetFramework { get; set; } = string.Empty;
    public string ReferencedProjectName { get; set; } = string.Empty;
    public string ReferencedProjectPath { get; set; } = string.Empty;
    public string ReferencedTargetFramework { get; set; } = string.Empty;
    public bool IsCompatible { get; set; } = true;
    public string? Suggestion { get; set; }
}
