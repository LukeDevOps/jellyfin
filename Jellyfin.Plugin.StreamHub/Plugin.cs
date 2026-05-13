using Jellyfin.Plugin.StreamHub.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.StreamHub;

public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override Guid Id => new Guid("d4f5a3b2-1c6e-4d8f-9a2b-7e3c5f1d0e4a");

    public override string Name => "StreamHub";

    public override string Description => "Search, stream and track media via Real-Debrid, Prowlarr and Trakt.";

    public string? GetApiKey() =>
        Environment.GetEnvironmentVariable("JELLYFIN_RD_API_KEY")
        ?? (string.IsNullOrEmpty(Configuration.ApiKey) ? null : Configuration.ApiKey);

    public string? GetProwlarrUrl() =>
        Environment.GetEnvironmentVariable("JELLYFIN_PROWLARR_URL")
        ?? (string.IsNullOrEmpty(Configuration.ProwlarrUrl) ? null : Configuration.ProwlarrUrl);

    public string? GetProwlarrApiKey() =>
        Environment.GetEnvironmentVariable("JELLYFIN_PROWLARR_API_KEY")
        ?? (string.IsNullOrEmpty(Configuration.ProwlarrApiKey) ? null : Configuration.ProwlarrApiKey);

    public string? GetTraktClientId() =>
        Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_ID")
        ?? (string.IsNullOrEmpty(Configuration.TraktClientId) ? null : Configuration.TraktClientId);

    public string? GetTraktClientSecret() =>
        Environment.GetEnvironmentVariable("JELLYFIN_TRAKT_CLIENT_SECRET")
        ?? (string.IsNullOrEmpty(Configuration.TraktClientSecret) ? null : Configuration.TraktClientSecret);

    public void SaveTraktTokens(string accessToken, string refreshToken)
    {
        Configuration.TraktAccessToken = accessToken;
        Configuration.TraktRefreshToken = refreshToken;
        SaveConfiguration();
    }
}
