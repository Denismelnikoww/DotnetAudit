using DotNetAuditTool.Core.Models;
using DotNetAuditTool.VersionChecker.Models;
using NuGet.Protocol.Core.Types;

namespace DotNetAuditTool.VersionChecker;

public class FrameworkCompatibility
{
    private static readonly Dictionary<string, string[]> _frameworkMappings = new()
    {
        ["net10.0"] = new[] { "net10.0", "net9.0", "net8.0", "net7.0", "net6.0", "net5.0", "netcoreapp3.1", "netstandard2.1" },
        ["net9.0"] = new[] { "net9.0", "net8.0", "net7.0", "net6.0", "net5.0", "netcoreapp3.1", "netstandard2.1" },
        ["net8.0"] = new[] { "net8.0", "net7.0", "net6.0", "net5.0", "netcoreapp3.1", "netstandard2.1" },
        ["net7.0"] = new[] { "net7.0", "net6.0", "net5.0", "netcoreapp3.1", "netstandard2.1" },
        ["net6.0"] = new[] { "net6.0", "net5.0", "netcoreapp3.1", "netstandard2.1", "netstandard2.0" },
        ["net5.0"] = new[] { "net5.0", "netcoreapp3.1", "netcoreapp3.0", "netstandard2.1", "netstandard2.0" },
        ["netcoreapp3.1"] = new[] { "netcoreapp3.1", "netcoreapp3.0", "netstandard2.1", "netstandard2.0" },
        ["netcoreapp3.0"] = new[] { "netcoreapp3.0", "netstandard2.1", "netstandard2.0" },
        ["netstandard2.1"] = new[] { "netstandard2.1", "netstandard2.0" },
        ["netstandard2.0"] = new[] { "netstandard2.0" }
    };

    private readonly NuGetVersionResolver _versionResolver;

    public FrameworkCompatibility()
    {
        _versionResolver = new NuGetVersionResolver();
    }

    public List<string> GetCompatibleChain(string framework)
    {
        framework = NormalizeFramework(framework);

        if (_frameworkMappings.TryGetValue(framework, out var chain))
            return chain.ToList();

        return new List<string> { framework };
    }

    private string NormalizeFramework(string framework)
    {
        return framework?.ToLower()
            .Replace(" ", "")
            .Replace("v", "")
            .Trim() ?? string.Empty;
    }

    public async Task<CompatibilityIssue> CheckFrameworkCompatibilityAsync(ProjectInfo project, PackageReference package)
    {
        var issue = new CompatibilityIssue
        {
            PackageName = package.Name,
            ProjectFramework = project.TargetFramework,
            PackageVersion = package.Version
        };

        if (string.IsNullOrWhiteSpace(project.TargetFramework))
        {
            issue.IsCompatible = true;
            return issue;
        }

        var projectFramework = NormalizeFramework(project.TargetFramework);
        var compatibleChain = GetCompatibleChain(projectFramework);
        var supportedFrameworks = await GetPackageSupportedFrameworksAsync(package);
        var normalizedSupportedFrameworks = supportedFrameworks.Select(NormalizeFramework).ToList();

        if (normalizedSupportedFrameworks.Contains("any") || normalizedSupportedFrameworks.Any(compatibleChain.Contains))
        {
            issue.IsCompatible = true;
            return issue;
        }

        if (!supportedFrameworks.Any())
        {
            if (projectFramework.Contains("netcoreapp") && package.Version.StartsWith("1."))
            {
                issue.IsCompatible = false;
                issue.IssueType = "LegacyPackage";
                issue.Suggestion = $"Package {package.Name} version {package.Version} may have compatibility issues with .NET Core. Consider upgrading.";
                return issue;
            }
        }

        issue.IsCompatible = false;
        issue.IssueType = "FrameworkMismatch";
        issue.Suggestion = $"Package {package.Name} version {package.Version} does not declare compatibility with target framework {project.TargetFramework}. " +
                           $"Compatible frameworks include: {string.Join(", ", compatibleChain)}. " +
                           $"Package supports: {string.Join(", ", supportedFrameworks)}.";

        return issue;
    }

    private async Task<List<string>> GetPackageSupportedFrameworksAsync(PackageReference package)
    {
        if (!NuGet.Versioning.NuGetVersion.TryParse(package.Version, out var packageVersion))
            return new List<string>();

        try
        {
            var metadataResource = await _versionResolver.GetPackageMetadataResourceAsync();
            var metadata = await metadataResource.GetMetadataAsync(
                package.Name,
                includePrerelease: true,
                includeUnlisted: false,
                new SourceCacheContext(),
                _versionResolver.Logger,
                CancellationToken.None);
            var packageMetadata = metadata.FirstOrDefault(m => m.Identity.Version == packageVersion);
            if (packageMetadata == null)
                return new List<string>();

            var frameworks = packageMetadata.DependencySets
                .Select(ds => ds.TargetFramework)
                .Where(tf => tf != null)
                .Select(tf => tf.IsAny ? "any" : tf.GetShortFolderName())
                .Where(tf => !string.IsNullOrWhiteSpace(tf))
                .Distinct()
                .ToList();

            return frameworks;
        }
        catch
        {
            return new List<string>();
        }
    }
}
