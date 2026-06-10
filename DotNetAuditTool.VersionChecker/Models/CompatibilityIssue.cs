namespace DotNetAuditTool.VersionChecker.Models;

public class CompatibilityIssue
{
    public string PackageName { get; set; } = string.Empty;
    public string ProjectFramework { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public bool IsCompatible { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
}