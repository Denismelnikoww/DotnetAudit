using DotNetAuditTool.Core.Models;

namespace DotNetAuditTool.VersionChecker;

public class ProjectReferenceCompatibilityChecker
{
    private readonly FrameworkCompatibility _frameworkCompatibility = new();

    public List<ProjectCompatibilityIssue> CheckProjectReferences(List<ProjectInfo> projects)
    {
        var issues = new List<ProjectCompatibilityIssue>();
        var projectByPath = projects
            .Where(p => !string.IsNullOrWhiteSpace(p.FilePath))
            .ToDictionary(p => p.FilePath, StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            var projectFramework = GetEffectiveFramework(project);
            if (string.IsNullOrWhiteSpace(projectFramework))
                continue;

            foreach (var projectReference in project.ProjectReferences)
            {
                if (string.IsNullOrWhiteSpace(projectReference.Path))
                    continue;

                if (!projectByPath.TryGetValue(projectReference.Path, out var referencedProject))
                {
                    issues.Add(new ProjectCompatibilityIssue
                    {
                        ProjectName = project.Name,
                        ProjectPath = project.FilePath,
                        ProjectTargetFramework = projectFramework,
                        ReferencedProjectName = projectReference.Name,
                        ReferencedProjectPath = projectReference.Path,
                        ReferencedTargetFramework = "Unknown",
                        IsCompatible = false,
                        Suggestion = $"Referenced project '{projectReference.Name}' not found in the dependency graph. Check the project reference path."
                    });
                    continue;
                }

                var referencedFramework = GetEffectiveFramework(referencedProject);
                if (string.IsNullOrWhiteSpace(referencedFramework))
                {
                    issues.Add(new ProjectCompatibilityIssue
                    {
                        ProjectName = project.Name,
                        ProjectPath = project.FilePath,
                        ProjectTargetFramework = projectFramework,
                        ReferencedProjectName = referencedProject.Name,
                        ReferencedProjectPath = referencedProject.FilePath,
                        ReferencedTargetFramework = "Unknown",
                        IsCompatible = false,
                        Suggestion = $"Referenced project '{referencedProject.Name}' has no target framework specified."
                    });
                    continue;
                }

                var normalizedReferencedFramework = NormalizeFramework(referencedFramework);
                var compatibleFrameworks = _frameworkCompatibility.GetCompatibleChain(projectFramework)
                    .Select(NormalizeFramework)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var isCompatible = compatibleFrameworks.Contains(normalizedReferencedFramework);

                issues.Add(new ProjectCompatibilityIssue
                {
                    ProjectName = project.Name,
                    ProjectPath = project.FilePath,
                    ProjectTargetFramework = projectFramework,
                    ReferencedProjectName = referencedProject.Name,
                    ReferencedProjectPath = referencedProject.FilePath,
                    ReferencedTargetFramework = referencedFramework,
                    IsCompatible = isCompatible,
                    Suggestion = isCompatible
                        ? $"✓ Compatible: {projectFramework} can reference {referencedFramework}"
                        : $"✗ Incompatible: Project {project.Name} ({projectFramework}) cannot reference project {referencedProject.Name} ({referencedFramework}). " +
                          "Use a compatible target framework or multi-target the referenced project."
                });
            }
        }
        return issues;
    }

    private static string GetEffectiveFramework(ProjectInfo project)
    {
        if (!string.IsNullOrWhiteSpace(project.TargetFramework))
            return project.TargetFramework;

        return project.TargetFrameworks.FirstOrDefault() ?? string.Empty;
    }

    private static string NormalizeFramework(string framework)
    {
        return framework?.ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("v", string.Empty)
            .Trim() ?? string.Empty;
    }
}