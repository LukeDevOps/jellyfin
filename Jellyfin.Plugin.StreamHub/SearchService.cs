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

        var results = withHashes
            .OrderBy(r => ResolutionTier(r.Title))
            .ThenBy(r => SourceTier(r.Title))
            .ThenByDescending(r => r.Seeders)
            .Select(r => new DebridSearchResult(r.Title, r.InfoHash!, r.DownloadUrl ?? $"magnet:?xt=urn:btih:{r.InfoHash}", r.Size, r.Seeders, r.Indexer, ResolutionLabel(r.Title)))
            .ToList();

        _logger.LogInformation("Found {Count} results for: {Query}", results.Count, query);
        return results;
    }

    private static string ResolutionLabel(string title) => title.ToUpperInvariant() switch
    {
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(2160P|4K|UHD)\b") => "4K",
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b1080P\b") => "1080p",
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b720P\b") => "720p",
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b480P\b") => "480p",
        _ => "SD"
    };

    private static int ResolutionTier(string title) => title.ToUpperInvariant() switch
    {
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(2160P|4K|UHD)\b") => 1,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b1080P\b") => 2,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b720P\b") => 3,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\b480P\b") => 4,
        _ => 5
    };

    private static int SourceTier(string title) => title.ToUpperInvariant() switch
    {
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\bBLU[-]?RAY\b|\bBDRIP\b|\bBDMV\b") => 1,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\bWEB[-]?DL\b") => 2,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\bWEB[-]?RIP\b") => 3,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\bHDTV\b") => 4,
        var t when System.Text.RegularExpressions.Regex.IsMatch(t, @"\bDVDRIP\b|\bDVD\b") => 5,
        _ => 6
    };
}

public record DebridSearchResult(
    string Title,
    string InfoHash,
    string MagnetUrl,
    long Size,
    int Seeders,
    string Indexer,
    string Quality);
