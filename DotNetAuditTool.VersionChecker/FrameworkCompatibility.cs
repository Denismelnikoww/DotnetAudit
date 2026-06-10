using DotNetAuditTool.Core.Models;
using DotNetAuditTool.VersionChecker.Models;

namespace DotNetAuditTool.VersionChecker;

public class FrameworkCompatibility
{
    private static readonly Dictionary<string, string[]> _frameworkMappings = new()
    {
        ["net8.0"] = new[] { "net8.0", "net7.0", "net6.0", "netcoreapp3.1", "netstandard2.1" },
        ["net7.0"] = new[] { "net7.0", "net6.0", "netcoreapp3.1", "netstandard2.1" },
        ["net6.0"] = new[] { "net6.0", "net5.0", "netcoreapp3.1", "netstandard2.1", "netstandard2.0" },
        ["net5.0"] = new[] { "net5.0", "netcoreapp3.1", "netcoreapp3.0", "netstandard2.1", "netstandard2.0" },
        ["netcoreapp3.1"] = new[] { "netcoreapp3.1", "netcoreapp3.0", "netstandard2.1", "netstandard2.0" },
        ["netcoreapp3.0"] = new[] { "netcoreapp3.0", "netstandard2.1", "netstandard2.0" },
        ["netstandard2.1"] = new[] { "netstandard2.1", "netstandard2.0" },
        ["netstandard2.0"] = new[] { "netstandard2.0" }
    };

    public bool IsCompatible(string targetFramework, string packageFramework)
    {
        if (string.IsNullOrEmpty(targetFramework) || string.IsNullOrEmpty(packageFramework))
            return false;

        targetFramework = NormalizeFramework(targetFramework);
        packageFramework = NormalizeFramework(packageFramework);

        if (_frameworkMappings.TryGetValue(targetFramework, out var compatibleFrameworks))
        {
            return compatibleFrameworks.Contains(packageFramework);
        }

        return targetFramework == packageFramework;
    }

    public string GetBestCompatibleFramework(string projectFramework, List<string> packageFrameworks)
    {
        foreach (var framework in GetCompatibleChain(projectFramework))
        {
            if (packageFrameworks.Contains(framework))
                return framework;
        }

        return packageFrameworks.FirstOrDefault() ?? "unknown";
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

    public CompatibilityIssue CheckFrameworkCompatibility(ProjectInfo project, PackageReference package)
    {
        var issue = new CompatibilityIssue
        {
            PackageName = package.Name,
            ProjectFramework = project.TargetFramework,
            PackageVersion = package.Version
        };

        if (project.TargetFramework.Contains("netcoreapp") && package.Version.StartsWith("1."))
        {
            issue.IsCompatible = false;
            issue.IssueType = "LegacyPackage";
            issue.Suggestion = $"Package {package.Name} version {package.Version} may have compatibility issues with .NET Core. Consider upgrading.";
        }
        else
        {
            issue.IsCompatible = true;
        }

        return issue;
    }
}
