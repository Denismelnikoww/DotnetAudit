using System.Text.Json;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.Security.Models.GitHub;

namespace DotNetAuditTool.Security.Clients;

public class GitHubAdvisoryClient
{
    private readonly HttpClient _httpClient;
    private const string GraphQlUrl = "https://api.github.com/graphql";
    private readonly string? _githubToken;

    public GitHubAdvisoryClient(string? githubToken = null)
    {
        _githubToken = githubToken ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DotNetAuditTool/1.0");

        if (!string.IsNullOrEmpty(_githubToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_githubToken}");
        }
    }

    public async Task<List<Vulnerability>> GetAdvisoriesForPackageAsync(string packageName)
    {
        var query = @"
        {
          securityAdvisories(first: 10, ecosystem: NUGET) {
            nodes {
              ghsaId
              summary
              description
              severity
              publishedAt
              references {
                url
              }
              vulnerabilities(first: 5) {
                nodes {
                  package {
                    name
                  }
                  vulnerableVersionRange
                  firstPatchedVersion {
                    identifier
                  }
                }
              }
              cvss {
                score
                vectorString
              }
            }
          }
        }";

        // Фильтруем по пакету на клиенте, так как GitHub API не поддерживает фильтрацию по пакету напрямую
        var request = new { query };
        var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(GraphQlUrl, content);

            if (!response.IsSuccessStatusCode)
                return new List<Vulnerability>();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GitHubAdvisoryResponse>(json);

            var vulnerabilities = new List<Vulnerability>();

            foreach (var advisory in result?.Data?.SecurityAdvisories?.Nodes ?? new List<GitHubAdvisory>())
            {
                foreach (var vuln in advisory.Vulnerabilities?.Nodes ?? new List<AdvisoryVulnerability>())
                {
                    if (vuln.Package?.Name?.Equals(packageName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        vulnerabilities.Add(MapToVulnerability(advisory, vuln));
                    }
                }
            }

            return vulnerabilities;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GitHub Advisory scan failed: {ex.Message}");
            return new List<Vulnerability>();
        }
    }

    private Vulnerability MapToVulnerability(GitHubAdvisory advisory, AdvisoryVulnerability vuln)
    {
        var severity = advisory.Severity?.ToLower() switch
        {
            "critical" => SeverityLevel.Critical,
            "high" => SeverityLevel.High,
            "medium" => SeverityLevel.Medium,
            "low" => SeverityLevel.Low,
            _ => SeverityLevel.None
        };

        return new Vulnerability
        {
            Id = advisory.GhsaId ?? string.Empty,
            PackageName = vuln.Package?.Name ?? string.Empty,
            InstalledVersion = string.Empty,
            PatchedVersion = vuln.FirstPatchedVersion?.Identifier,
            Severity = severity,
            CvssScore = advisory.Cvss?.Score ?? 0,
            Description = advisory.Description ?? advisory.Summary ?? string.Empty,
            PublishedDate = advisory.PublishedAt ?? DateTime.UtcNow,
            References = advisory.References?.Nodes?.Select(r => r.Url).ToList() ?? [],
            AffectedVersions = new List<string> { vuln.VulnerableVersionRange ?? string.Empty }
        };
    }
}
