using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamHub;

public class TraktService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TraktService> _logger;

    public TraktService(IHttpClientFactory httpClientFactory, ILogger<TraktService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(Plugin.Instance?.Configuration.TraktAccessToken);

    /// <summary>
    /// Kicks off the device code flow. Returns the code the user enters at trakt.tv/activate.
    /// </summary>
    public async Task<DeviceCodeResponse?> StartDeviceAuthAsync(CancellationToken cancellationToken)
    {
        var clientId = Plugin.Instance?.GetTraktClientId();
        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("Trakt client ID is not configured. Set JELLYFIN_TRAKT_CLIENT_ID.");
            return null;
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);
        return await client.StartDeviceAuthAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Polls Trakt once for a token using the device code. Returns true if authenticated, false if still pending.
    /// </summary>
    public async Task<bool> PollForTokenAsync(string deviceCode, CancellationToken cancellationToken)
    {
        var clientId = Plugin.Instance?.GetTraktClientId();
        var clientSecret = Plugin.Instance?.GetTraktClientSecret();

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogWarning("Trakt client ID or secret is not configured.");
            return false;
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);

        try
        {
            var token = await client.PollForTokenAsync(deviceCode, clientSecret, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                return false;
            }

            Plugin.Instance!.SaveTraktTokens(token.AccessToken, token.RefreshToken);
            _logger.LogInformation("Trakt authentication successful.");
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Trakt device auth polling failed.");
            return false;
        }
    }

    /// <summary>
    /// Returns the user's recent watch history from Trakt.
    /// </summary>
    public async Task<IReadOnlyList<TraktHistoryItem>> GetHistoryAsync(int limit, CancellationToken cancellationToken)
    {
        var accessToken = Plugin.Instance?.Configuration.TraktAccessToken;
        var clientId = Plugin.Instance?.GetTraktClientId();

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(clientId))
        {
            return [];
        }

        var client = new TraktClient(_httpClientFactory.CreateClient(), clientId);
        return await client.GetHistoryAsync(accessToken, limit, cancellationToken).ConfigureAwait(false);
    }
}
