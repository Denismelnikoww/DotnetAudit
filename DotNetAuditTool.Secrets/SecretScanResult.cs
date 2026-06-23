using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.Secrets;

public class SecretScanResult
{
    public DateTime ScanTime { get; set; }
    public string TargetPath { get; set; } = string.Empty;
    public int TotalFilesScanned { get; set; }
    public List<SecretMatch> FoundSecrets { get; set; } = new();
    public Dictionary<SecretType, int> SecretsByType { get; set; } = new();
    public List<string> FilesWithSecrets { get; set; } = new();
    public SecretRiskLevel RiskLevel { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public enum SecretRiskLevel
{
    None,
    Critical
}