using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.VersionChecker.Models;

public class PackageUpdateReport
{
    public string ProjectName { get; set; } = string.Empty;
    public DateTime ScanTime { get; set; }
    public int TotalPackages { get; set; }
    public int OutdatedPackages { get; set; }
    public int MajorUpdates { get; set; }
    public int MinorUpdates { get; set; }
    public int PatchUpdates { get; set; }
    public UpdatePriority UpdatePriority { get; set; }
    public List<VersionIssue> Issues { get; set; } = new();
}
