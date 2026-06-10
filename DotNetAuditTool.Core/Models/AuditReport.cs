namespace DotNetAuditTool.Core.Models;

public class AuditReport
{
    public DateTime ScanTime { get; set; } = DateTime.UtcNow;
    public string TargetPath { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty; // .csproj, .sln, folder
    public AuditSummary Summary { get; set; } = new();
    public DependencyGraph? Graph { get; set; }
    public List<VersionIssue> VersionIssues { get; set; } = new();
    public List<Vulnerability> Vulnerabilities { get; set; } = new();
    public List<SecretMatch> Secrets { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}
