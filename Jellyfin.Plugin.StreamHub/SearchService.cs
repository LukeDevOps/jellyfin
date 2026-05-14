using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamHub;

public class SearchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IHttpClientFactory httpClientFactory, ILogger<SearchService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DebridSearchResult>> SearchAsync(string query, int category, CancellationToken cancellationToken)
    {
        var prowlarrUrl = Environment.GetEnvironmentVariable("JELLYFIN_PROWLARR_URL");
        var prowlarrKey = Environment.GetEnvironmentVariable("JELLYFIN_PROWLARR_API_KEY");
        var rdKey = Environment.GetEnvironmentVariable("JELLYFIN_RD_API_KEY");

        if (string.IsNullOrEmpty(prowlarrUrl) || string.IsNullOrEmpty(prowlarrKey))
        {
            _logger.LogWarning("Prowlarr is not configured. Set JELLYFIN_PROWLARR_URL and JELLYFIN_PROWLARR_API_KEY.");
            return [];
        }

        if (string.IsNullOrEmpty(rdKey))
        {
            _logger.LogWarning("Real-Debrid API key is not configured.");
            return [];
        }

        var prowlarr = new ProwlarrClient(_httpClientFactory.CreateClient(), prowlarrUrl, prowlarrKey);
        var rd = new RealDebridClient(_httpClientFactory.CreateClient(), rdKey);

        _logger.LogInformation("Searching Prowlarr for: {Query}", query);
        var prowlarrResults = await prowlarr.SearchAsync(query, category, cancellationToken).ConfigureAwait(false);

        var withHashes = prowlarrResults
            .Where(r => !string.IsNullOrEmpty(r.InfoHash))
            .ToList();

        if (withHashes.Count == 0)
        {
            _logger.LogDebug("No results with info hashes found for: {Query}", query);
            return [];
        }

        _logger.LogInformation("Checking RD instant availability for {Count} results", withHashes.Count);
        var cachedHashes = await rd.GetInstantAvailabilityAsync(
            withHashes.Select(r => r.InfoHash!),
            cancellationToken).ConfigureAwait(false);

        var cached = withHashes
            .Where(r => cachedHashes.Contains(r.InfoHash!))
            .OrderByDescending(r => r.Seeders)
            .Select(r => new DebridSearchResult(r.Title, r.InfoHash!, r.DownloadUrl ?? $"magnet:?xt=urn:btih:{r.InfoHash}", r.Size, r.Seeders, r.Indexer))
            .ToList();

        _logger.LogInformation("Found {Count} RD-cached results for: {Query}", cached.Count, query);
        return cached;
    }
}

public record DebridSearchResult(
    string Title,
    string InfoHash,
    string MagnetUrl,
    long Size,
    int Seeders,
    string Indexer);
