using System.Text;
using System.Text.Json;
using DotNetAuditTool.Core.Models;
using DotNetAuditTool.Security.Models.Oss;

namespace DotNetAuditTool.Security.Clients;

public class OSSIndexClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://ossindex.sonatype.org/api/v3/";

    public OSSIndexClient()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "DotNetAuditTool/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<Vulnerability>> ScanPackagesAsync(List<PackageReference> packages)
    {
        var vulnerabilities = new List<Vulnerability>();

        var batches = packages.Chunk(128);

        foreach (var batch in batches)
        {
            var batchVulns = await ScanBatchAsync(batch);
            vulnerabilities.AddRange(batchVulns);
            await Task.Delay(100);
        }

        return vulnerabilities;
    }

    private async Task<List<Vulnerability>> ScanBatchAsync(PackageReference[] packages)
    {
        var coordinates = packages.Select(p => $"pkg:nuget/{p.Name}@{p.Version}").ToList();
        var request = new { coordinates = coordinates };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.PostAsync("component-report", content);

            if (!response.IsSuccessStatusCode)
                return new List<Vulnerability>();

            var json = await response.Content.ReadAsStringAsync();
            var results = JsonSerializer.Deserialize<List<OssIndexResponse>>(json);

            var vulnerabilities = new List<Vulnerability>();

            foreach (var result in results ?? new List<OssIndexResponse>())
            {
                if (result.Vulnerabilities != null)
                {
                    foreach (var vuln in result.Vulnerabilities)
                    {
                        vulnerabilities.Add(MapToVulnerability(vuln, result.Coordinates));
                    }
                }
            }

            return vulnerabilities;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OSS Index scan failed: {ex.Message}");
            return new List<Vulnerability>();
        }
    }

    private Vulnerability MapToVulnerability(OssVulnerability vuln, string coordinates)
    {
        var packageMatch = System.Text.RegularExpressions.Regex.Match(coordinates, @"pkg:nuget/(.+)@(.+)");
        var packageName = packageMatch.Success ? packageMatch.Groups[1].Value : "Unknown";
        var version = packageMatch.Success ? packageMatch.Groups[2].Value : "Unknown";

        return new Vulnerability
        {
            Id = vuln.Id,
            PackageName = packageName,
            InstalledVersion = version,
            PatchedVersion = GetPatchedVersion(vuln),
            Severity = MapSeverity(vuln.CvssScore ?? 0),
            CvssScore = vuln.CvssScore ?? 0,
            Description = vuln.Description ?? string.Empty,
            PublishedDate = vuln.PublishedDate ?? DateTime.UtcNow,
            References = vuln.References ?? new List<string>(),
        };
    }

    private SeverityLevel MapSeverity(double score)
    {
        return score switch
        {
            >= 9.0 => SeverityLevel.Critical,
            >= 7.0 => SeverityLevel.High,
            >= 4.0 => SeverityLevel.Medium,
            > 0 => SeverityLevel.Low,
            _ => SeverityLevel.None
        };
    }

    private string? GetPatchedVersion(OssVulnerability vuln)
    {
        if (vuln.Title?.Contains("fixed in") == true)
        {
            var match = System.Text.RegularExpressions.Regex.Match(vuln.Title, @"fixed in (\d+\.\d+\.\d+)");
            if (match.Success)
                return match.Groups[1].Value;
        }
        return null;
    }
}
