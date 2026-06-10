using DotNetAuditTool.Core.Models;
using DotNetAuditTool.Core.Utils;
using DotNetAuditTool.VersionChecker.Models;
using NuGet.Versioning;

namespace DotNetAuditTool.VersionChecker;

public class VersionCompatibilityChecker
{
    private readonly NuGetVersionResolver _versionResolver;
    private readonly FrameworkCompatibility _frameworkCompatibility;

    public VersionCompatibilityChecker()
    {
        _versionResolver = new NuGetVersionResolver();
        _frameworkCompatibility = new FrameworkCompatibility();
    }

    public async Task<List<VersionIssue>> CheckPackagesAsync(ProjectInfo project)
    {
        var issues = new List<VersionIssue>();
        var tasks = project.Packages.Select(p => CheckPackageAsync(p, project)).ToArray();
        var results = await Task.WhenAll(tasks);

        issues.AddRange(results.Where(r => r != null));
        return issues;
    }

    public async Task<List<VersionIssue>> CheckPackagesAsync(List<ProjectInfo> projects)
    {
        var allIssues = new List<VersionIssue>();
        var allPackages = projects.SelectMany(p => p.Packages).DistinctBy(p => p.Name).ToList();

        foreach (var package in allPackages)
        {
            var issue = await CheckPackageVersionAsync(package);
            if (issue != null)
                allIssues.Add(issue);
        }

        return allIssues;
    }

    private async Task<VersionIssue?> CheckPackageAsync(PackageReference package, ProjectInfo project)
    {
        var issue = await CheckPackageVersionAsync(package);

        if (issue != null)
        {
            var compatibilityIssue = _frameworkCompatibility.CheckFrameworkCompatibility(project, package);
            if (!compatibilityIssue.IsCompatible)
            {
                issue.Suggestion = compatibilityIssue.Suggestion;
            }
        }

        return issue;
    }

    private async Task<VersionIssue?> CheckPackageVersionAsync(PackageReference package)
    {
        if (!NuGetVersion.TryParse(package.Version, out var currentVersion))
            return null;

        var latestVersion = await _versionResolver.GetLatestStableVersionAsync(package.Name);

        if (latestVersion == null)
            return null;

        var difference = VersionHelper.CompareVersions(package.Version, latestVersion.ToString());

        var issue = new VersionIssue
        {
            PackageName = package.Name,
            CurrentVersion = package.Version,
            LatestVersion = latestVersion.ToString(),
            LatestStableVersion = latestVersion.ToString(),
            Difference = difference,
            IsOutdated = difference != VersionDifference.Same,
            HasBreakingChanges = difference == VersionDifference.Major
        };

        if (issue.IsOutdated)
        {
            issue.Suggestion = GenerateSuggestion(package, latestVersion, difference);
        }

        return issue;
    }

    private string GenerateSuggestion(PackageReference package, NuGetVersion latestVersion, VersionDifference difference)
    {
        return difference switch
        {
            VersionDifference.Major => $"Major update available! Please review breaking changes before updating {package.Name} from {package.Version} to {latestVersion}",
            VersionDifference.Minor => $"Minor update available for {package.Name}: {package.Version} → {latestVersion}",
            VersionDifference.Patch => $"Patch update available for {package.Name}: {package.Version} → {latestVersion}",
            _ => $"Update available: {package.Name} {package.Version} → {latestVersion}"
        };
    }

    public async Task<Dictionary<string, List<string>>> GetUpdateChainsAsync(List<PackageReference> packages)
    {
        var updateChains = new Dictionary<string, List<string>>();

        foreach (var package in packages)
        {
            var versions = await _versionResolver.GetVersionHistoryAsync(package.Name, 5);
            if (versions.Any())
            {
                updateChains[package.Name] = versions.Select(v => v.ToString()).ToList();
            }
        }

        return updateChains;
    }

    public async Task<PackageUpdateReport> GenerateUpdateReportAsync(ProjectInfo project)
    {
        var issues = await CheckPackagesAsync(project);

        var report = new PackageUpdateReport
        {
            ProjectName = project.Name,
            ScanTime = DateTime.UtcNow,
            TotalPackages = project.Packages.Count,
            OutdatedPackages = issues.Count(i => i.IsOutdated),
            MajorUpdates = issues.Count(i => i.Difference == VersionDifference.Major),
            MinorUpdates = issues.Count(i => i.Difference == VersionDifference.Minor),
            PatchUpdates = issues.Count(i => i.Difference == VersionDifference.Patch),
            Issues = issues
        };

        report.UpdatePriority = CalculatePriority(report);

        return report;
    }

    private UpdatePriority CalculatePriority(PackageUpdateReport report)
    {
        if (report.MajorUpdates > 0)
            return UpdatePriority.High;
        if (report.MinorUpdates > 0 && report.MinorUpdates > report.TotalPackages * 0.3)
            return UpdatePriority.Medium;
        if (report.PatchUpdates > 0)
            return UpdatePriority.Low;

        return UpdatePriority.None;
    }
}
