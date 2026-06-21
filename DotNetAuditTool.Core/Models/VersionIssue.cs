namespace DotNetAuditTool.Core.Models;

public class VersionIssue
{
    public string PackageName { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
    public string? LatestStableVersion { get; set; }
    public VersionDifference Difference { get; set; }
    public bool IsOutdated { get; set; }
    public bool HasBreakingChanges { get; set; }
    public bool IsCompatible { get; set; } = true;
    public string? Suggestion { get; set; }
}
