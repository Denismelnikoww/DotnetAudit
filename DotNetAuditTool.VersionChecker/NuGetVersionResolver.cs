namespace DotNetAuditTool.VersionChecker;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;


public class NuGetVersionResolver
{
    private readonly SourceRepository _repository;
    private readonly ILogger _logger;

    public ILogger Logger => _logger;

    public NuGetVersionResolver(string sourceUrl = "https://api.nuget.org/v3/index.json")
    {
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());

        _repository = new SourceRepository(new PackageSource(sourceUrl), providers);
        _logger = NullLogger.Instance;
    }

    public async Task<NuGetVersion?> GetLatestVersionAsync(string packageName, bool includePrerelease = false)
    {
        try
        {
            var metadataResource = await _repository.GetResourceAsync<FindPackageByIdResource>();
            var versions = await metadataResource.GetAllVersionsAsync(
                packageName,
                new SourceCacheContext(),
                _logger,
                CancellationToken.None);

            if (!versions.Any())
                return null;

            var filteredVersions = includePrerelease
                ? versions
                : versions.Where(v => !v.IsPrerelease);

            return filteredVersions.Max();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching versions for {packageName}: {ex.Message}");
            return null;
        }
    }

    public async Task<NuGetVersion?> GetLatestStableVersionAsync(string packageName)
    {
        return await GetLatestVersionAsync(packageName, false);
    }

    public async Task<PackageMetadataResource> GetPackageMetadataResourceAsync()
    {
        return await _repository.GetResourceAsync<PackageMetadataResource>();
    }
}