using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamHub;

public class TraktService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TraktService> _logger;
    private readonly string _tokenPath;

    private record TokenStore(string AccessToken, string RefreshToken);

    public TraktService(IHttpClientFactory httpClientFactory, IApplicationPaths appPaths, ILogger<TraktService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _tokenPath = Path.Combine(appPaths.DataPath, "streamhub-trakt.json");
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(LoadTokens()?.AccessToken);

    public async Task<DeviceCodeResponse?> StartDeviceAuthAsync(CancellationToken cancellationToken)
    {
        var clientId = Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_ID");
        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("Trakt client ID is not configured. Set JELLYFIN_TRAKT_CLIENT_ID.");
            return null;
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);
        try
        {
            return await client.StartDeviceAuthAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Trakt device/code endpoint.");
            return null;
        }
    }

    public async Task<bool> PollForTokenAsync(string deviceCode, CancellationToken cancellationToken)
    {
        var clientId = Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_ID");
        var clientSecret = Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_SECRET");

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogWarning("Trakt client ID or secret is not configured.");
            return false;
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);

        _logger.LogInformation("Trakt: polling /oauth/token with device_code={Code}, client_id={Id}", deviceCode, clientId[..8] + "…");

        try
        {
            var token = await client.PollForTokenAsync(deviceCode, clientSecret, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                return false;
            }

            SaveTokens(token.AccessToken, token.RefreshToken);
            _logger.LogInformation("Trakt authentication successful.");
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("still pending", StringComparison.Ordinal))
        {
            _logger.LogInformation("Trakt poll response: {Message}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trakt device auth polling failed.");
            return false;
        }
    }

    public async Task<IReadOnlyList<TraktHistoryItem>> GetHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        var clientId = Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_ID");
        var accessToken = LoadTokens()?.AccessToken;

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(clientId))
        {
            return [];
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);
        return await client.GetHistoryAsync(accessToken, limit, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TraktRecommendationItem>> GetRecommendationsAsync(string type, int limit, int page, CancellationToken cancellationToken)
    {
        var clientId = Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_ID");
        var accessToken = LoadTokens()?.AccessToken;

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(clientId))
        {
            return [];
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);
        return await client.GetRecommendationsAsync(type, accessToken, limit, page, cancellationToken).ConfigureAwait(false);
    }

    private TokenStore? LoadTokens()
    {
        try
        {
            if (!File.Exists(_tokenPath)) return null;
            var json = File.ReadAllText(_tokenPath);
            return JsonSerializer.Deserialize<TokenStore>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveTokens(string accessToken, string refreshToken)
    {
        var json = JsonSerializer.Serialize(new TokenStore(accessToken, refreshToken));
        File.WriteAllText(_tokenPath, json);
    }
}
