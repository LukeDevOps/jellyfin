using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Jellyfin.Plugin.RealDebrid;

public class RealDebridClient
{
    private const string BaseUrl = "https://api.real-debrid.com/rest/1.0";
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
        var result = JsonSerializer.Deserialize<UnrestrictResponse>(json);
        return result?.Download;
    }

    private sealed class UnrestrictResponse
    {
        [JsonPropertyName("download")]
        public string? Download { get; set; }
    }
}
