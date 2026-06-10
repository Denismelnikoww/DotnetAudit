namespace DotNetAuditTool.Security.Models.GitHub;

public class SecurityAdvisories
{
    public List<GitHubAdvisory> Nodes { get; set; } = new();
}