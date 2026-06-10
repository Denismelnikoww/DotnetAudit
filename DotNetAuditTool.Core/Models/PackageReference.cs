namespace DotNetAuditTool.Core.Models;

public class PackageReference
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsPrivateAssets { get; set; }
    public bool IsDevelopmentDependency { get; set; }
}
