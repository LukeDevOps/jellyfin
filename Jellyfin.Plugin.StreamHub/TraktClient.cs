using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.StreamHub;

public class TraktClient
{
    private const string BaseUrl = "https://api.trakt.tv";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _clientId;

    public TraktClient(HttpClient httpClient, string clientId)
    {
        _httpClient = httpClient;
        _clientId = clientId;
        _httpClient.DefaultRequestHeaders.Add("trakt-api-key", clientId);
        _httpClient.DefaultRequestHeaders.Add("trakt-api-version", "2");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-StreamHub/1.0");
    }

    /// <summary>
    /// Starts the device auth flow. Returns the code the user must enter at trakt.tv/activate.
    /// </summary>
    public async Task<DeviceCodeResponse?> StartDeviceAuthAsync(CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new { client_id = _clientId });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{BaseUrl}/oauth/device/code", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Trakt device/code returned {(int)response.StatusCode}: {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<DeviceCodeResponse>(json, JsonOptions);
    }

    /// <summary>
    /// Polls for a token after the user has activated their device code.
    /// Returns null if still pending, throws on expiry.
    /// </summary>
    public async Task<TokenResponse?> PollForTokenAsync(string deviceCode, string clientSecret, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            code = deviceCode,
            client_id = _clientId,
            client_secret = clientSecret
        });

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{BaseUrl}/oauth/device/token", content, cancellationToken).ConfigureAwait(false);

        var pollBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // 400 = still pending, 410 = expired
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException($"Trakt /oauth/token 400 (still pending): {pollBody}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Trakt /oauth/token {(int)response.StatusCode}: {pollBody}");
        }

        return JsonSerializer.Deserialize<TokenResponse>(pollBody, JsonOptions);
    }

    /// <summary>
    /// Gets the user's watch history, mixing movies and episodes, ordered by watched_at desc.
    /// </summary>
    public async Task<IReadOnlyList<TraktHistoryItem>> GetHistoryAsync(string accessToken, int limit, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/users/me/history?limit={limit}&extended=full");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<TraktHistoryItem>>(json, JsonOptions) ?? [];
    }
}

public class DeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = string.Empty;

    [JsonPropertyName("verification_url")]
    public string VerificationUrl { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; }
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}

public class TraktHistoryItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("watched_at")]
    public DateTime WatchedAt { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("movie")]
    public TraktMovie? Movie { get; set; }

    [JsonPropertyName("show")]
    public TraktShow? Show { get; set; }

    [JsonPropertyName("episode")]
    public TraktEpisode? Episode { get; set; }
}

public class TraktMovie
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("ids")]
    public TraktIds Ids { get; set; } = new();
}

public class TraktShow
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("ids")]
    public TraktIds Ids { get; set; } = new();
}

public class TraktEpisode
{
    [JsonPropertyName("season")]
    public int Season { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public class TraktIds
{
    [JsonPropertyName("trakt")]
    public int Trakt { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tmdb")]
    public int? Tmdb { get; set; }
}
