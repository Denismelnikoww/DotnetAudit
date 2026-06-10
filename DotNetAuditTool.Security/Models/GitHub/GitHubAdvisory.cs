namespace DotNetAuditTool.Security.Models.GitHub;

public class GitHubAdvisory
{
    public string? GhsaId { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public DateTime? PublishedAt { get; set; }
    public CvssInfo? Cvss { get; set; }
    public References? References { get; set; }
    public VulnerabilitiesList? Vulnerabilities { get; set; }
}
