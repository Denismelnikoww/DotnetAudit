namespace DotNetAuditTool.Security.Models.GitHub;

public class VulnerabilitiesList
{
    public List<AdvisoryVulnerability> Nodes { get; set; } = new();
}
