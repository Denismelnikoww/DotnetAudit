using DotNetAuditTool.Core.Models;
using DotNetAuditTool.Core.Utils;
using NuGet.Versioning;

namespace DotNetAuditTool.VersionChecker;

public class VersionCompatibilityChecker
{
    private readonly NuGetVersionResolver _versionResolver = new();
    private readonly FrameworkCompatibility _frameworkCompatibility = new();


    public async Task<List<VersionIssue>> CheckPackagesAsync(ProjectInfo project)
    {
        var issues = new List<VersionIssue>();
        var tasks = project.Packages.Select(p => CheckPackageAsync(p, project)).ToArray();
        var results = await Task.WhenAll(tasks);

        issues.AddRange(results.OfType<VersionIssue>());
        return issues;
    }

    private async Task<VersionIssue?> CheckPackageAsync(PackageReference package, ProjectInfo project)
    {
        var issue = await CheckPackageVersionAsync(package);
        var compatibilityIssue = await _frameworkCompatibility.CheckFrameworkCompatibilityAsync(project, package);

        if (issue == null)
        {
            if (!compatibilityIssue.IsCompatible)
            {
                issue = new VersionIssue
                {
                    PackageName = package.Name,
                    CurrentVersion = package.Version,
                    LatestVersion = package.Version,
                    LatestStableVersion = package.Version,
                    Difference = VersionDifference.Same,
                    IsOutdated = false,
                    HasBreakingChanges = false,
                    IsCompatible = false,
                    Suggestion = compatibilityIssue.Suggestion
                };
            }

            return issue;
        }

        issue.IsCompatible = compatibilityIssue.IsCompatible;
        if (!compatibilityIssue.IsCompatible)
        {
            issue.Suggestion = compatibilityIssue.Suggestion;
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
}
