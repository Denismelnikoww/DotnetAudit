
using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.Core.Utils;

public static class VersionHelper
{
    public static VersionDifference CompareVersions(string current, string? target)
    {
        if (string.IsNullOrEmpty(target))
            return VersionDifference.Same;

        var currentParts = ParseVersion(current);
        var targetParts = ParseVersion(target);

        if (currentParts.Major != targetParts.Major)
            return VersionDifference.Major;
        if (currentParts.Minor != targetParts.Minor)
            return VersionDifference.Minor;
        if (currentParts.Patch != targetParts.Patch)
            return VersionDifference.Patch;
        if (currentParts.Prerelease != targetParts.Prerelease)
            return VersionDifference.Prerelease;

        return VersionDifference.Same;
    }

    private static (int Major, int Minor, int Patch, string Prerelease) ParseVersion(string version)
    {
        var prereleaseSplit = version.Split('-');
        var versionParts = prereleaseSplit[0].Split('.');

        return (
            Major: int.Parse(versionParts[0]),
            Minor: versionParts.Length > 1 ? int.Parse(versionParts[1]) : 0,
            Patch: versionParts.Length > 2 ? int.Parse(versionParts[2]) : 0,
            Prerelease: prereleaseSplit.Length > 1 ? prereleaseSplit[1] : string.Empty
        );
    }
}