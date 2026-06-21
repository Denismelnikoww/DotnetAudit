using System.Xml;
using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.DependencyGraphBuilder.Parsers;

public class ProjectFileParser
{
    public async Task<ProjectInfo> ParseAsync(string projectPath)
    {
        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var content = await File.ReadAllTextAsync(projectPath);
        return Parse(content, projectPath);
    }

    public ProjectInfo Parse(string content, string filePath)
    {
        var projectInfo = new ProjectInfo
        {
            FilePath = filePath,
            DirectoryPath = Path.GetDirectoryName(filePath) ?? string.Empty,
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        var doc = new XmlDocument();
        doc.LoadXml(content);

        var targetFrameworkNode = doc.SelectSingleNode("//Project/PropertyGroup/TargetFramework");
        if (targetFrameworkNode != null)
        {
            projectInfo.TargetFramework = targetFrameworkNode.InnerText;
        }

        var targetFrameworksNode = doc.SelectSingleNode("//Project/PropertyGroup/TargetFrameworks");
        if (targetFrameworksNode != null)
        {
            var frameworks = targetFrameworksNode.InnerText.Split(';');
            projectInfo.TargetFrameworks.AddRange(frameworks);
            if (string.IsNullOrWhiteSpace(projectInfo.TargetFramework) && projectInfo.TargetFrameworks.Any())
            {
                projectInfo.TargetFramework = projectInfo.TargetFrameworks.First();
            }
        }

        var packageNodes = doc.SelectNodes("//Project/ItemGroup/PackageReference");
        if (packageNodes != null)
        {
            foreach (XmlNode packageNode in packageNodes)
            {
                var package = new PackageReference
                {
                    Name = packageNode.Attributes?["Include"]?.Value ?? string.Empty,
                    Version = packageNode.Attributes?["Version"]?.Value ??
                             packageNode.SelectSingleNode("Version")?.InnerText ?? string.Empty,
                    IsPrivateAssets = packageNode.SelectSingleNode("PrivateAssets")?.InnerText == "true",
                    IsDevelopmentDependency = packageNode.SelectSingleNode("DevelopmentDependency")?.InnerText == "true"
                };

                if (!string.IsNullOrEmpty(package.Name))
                {
                    projectInfo.Packages.Add(package);
                }
            }
        }

        var projectRefNodes = doc.SelectNodes("//Project/ItemGroup/ProjectReference");
        if (projectRefNodes != null)
        {
            foreach (XmlNode refNode in projectRefNodes)
            {
                var include = refNode.Attributes?["Include"]?.Value;
                if (!string.IsNullOrEmpty(include))
                {
                    var projectRef = new ProjectReference
                    {
                        Name = Path.GetFileNameWithoutExtension(include),
                        Path = include,
                        RelativePath = include
                    };

                    var fullPath = Path.GetFullPath(Path.Combine(projectInfo.DirectoryPath, include));
                    if (File.Exists(fullPath))
                    {
                        projectRef.Path = fullPath;
                    }

                    projectInfo.ProjectReferences.Add(projectRef);
                }
            }
        }

        var propertyNodes = doc.SelectNodes("//Project/PropertyGroup/*");
        if (propertyNodes != null)
        {
            foreach (XmlNode propNode in propertyNodes)
            {
                if (!string.IsNullOrEmpty(propNode.Name) && !projectInfo.Properties.ContainsKey(propNode.Name))
                {
                    projectInfo.Properties[propNode.Name] = propNode.InnerText;
                }
            }
        }

        return projectInfo;
    }
}
