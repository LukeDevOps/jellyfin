using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.StreamHub;

public class ProwlarrClient
{
    // Prowlarr category IDs
    public const int CategoryMovies = 2000;
    public const int CategoryTV = 5000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ProwlarrClient(HttpClient httpClient, string baseUrl, string apiKey)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<IReadOnlyList<ProwlarrResult>> SearchAsync(string query, int category, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/api/v1/search?query={Uri.EscapeDataString(query)}&categories={category}&type=search";
        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<ProwlarrResult>>(json, JsonOptions) ?? [];
    }
}

public class ProwlarrResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("infoHash")]
    public string? InfoHash { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("seeders")]
    public int Seeders { get; set; }

    [JsonPropertyName("leechers")]
    public int Leechers { get; set; }

    [JsonPropertyName("indexer")]
    public string Indexer { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public List<ProwlarrCategory> Categories { get; set; } = [];
}

public class ProwlarrCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
