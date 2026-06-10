namespace DotNetAuditTool.Core.Models;

public class AuditSummary
{
    public int TotalProjects { get; set; }
    public int TotalPackages { get; set; }
    public int TotalVulnerabilities { get; set; }
    public int TotalSecrets { get; set; }
    public int OutdatedPackages { get; set; }
    public Dictionary<SeverityLevel, int> VulnerabilitiesBySeverity { get; set; } = new();
    public Dictionary<SecretType, int> SecretsByType { get; set; } = new();
    public double CriticalityScore { get; set; }
}