using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.StreamHub;

public class RealDebridClient
{
    private const string BaseUrl = "https://api.real-debrid.com/rest/1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public RealDebridClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string?> UnrestrictLinkAsync(string link, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("link", link)]);
        var response = await _httpClient.PostAsync($"{BaseUrl}/unrestrict/link", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<UnrestrictResponse>(json, JsonOptions);
        return result?.Download;
    }

    /// <summary>
    /// Checks which of the supplied hashes are instantly available (already cached) on Real-Debrid.
    /// Returns the subset of hashes that are cached.
    /// </summary>
    public async Task<IReadOnlySet<string>> GetInstantAvailabilityAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        var hashList = string.Join("/", hashes.Select(h => h.ToUpperInvariant()));
        if (string.IsNullOrEmpty(hashList))
        {
            return new HashSet<string>();
        }

        var response = await _httpClient.GetAsync($"{BaseUrl}/torrents/instantAvailability/{hashList}", cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return new HashSet<string>();
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Response is a dict keyed by uppercase hash — if a hash has cached variants its value is non-empty
        using var doc = JsonDocument.Parse(json);
        var cached = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            // Each value is an object with a "rd" array — non-empty means it's cached
            if (prop.Value.TryGetProperty("rd", out var rd) && rd.GetArrayLength() > 0)
            {
                cached.Add(prop.Name);
            }
        }

        return cached;
    }

    /// <summary>
    /// Adds a magnet link to Real-Debrid and returns the torrent ID.
    /// </summary>
    public async Task<string?> AddMagnetAsync(string magnetUrl, CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("magnet", magnetUrl)]);
        var response = await _httpClient.PostAsync($"{BaseUrl}/torrents/addMagnet", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<AddMagnetResponse>(json, JsonOptions);
        return result?.Id;
    }

    /// <summary>
    /// Selects all files in a torrent so RD starts making them available, then returns the download links.
    /// </summary>
    public async Task<IReadOnlyList<string>> SelectFilesAndGetLinksAsync(string torrentId, CancellationToken cancellationToken)
    {
        // Select all files
        var selectContent = new FormUrlEncodedContent([new KeyValuePair<string, string>("files", "all")]);
        await _httpClient.PostAsync($"{BaseUrl}/torrents/selectFiles/{torrentId}", selectContent, cancellationToken).ConfigureAwait(false);

        // Fetch torrent info to get the links
        var infoResponse = await _httpClient.GetAsync($"{BaseUrl}/torrents/info/{torrentId}", cancellationToken).ConfigureAwait(false);
        if (!infoResponse.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await infoResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var info = JsonSerializer.Deserialize<TorrentInfo>(json, JsonOptions);
        return info?.Links ?? [];
    }

    private sealed class UnrestrictResponse
    {
        [JsonPropertyName("download")]
        public string? Download { get; set; }
    }

    private sealed class AddMagnetResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private sealed class TorrentInfo
    {
        [JsonPropertyName("links")]
        public List<string>? Links { get; set; }
    }
}
