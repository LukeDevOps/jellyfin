using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.StreamHub.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;

    public string ProwlarrUrl { get; set; } = string.Empty;

    public string ProwlarrApiKey { get; set; } = string.Empty;

    public string TraktClientId { get; set; } = string.Empty;

    public string TraktClientSecret { get; set; } = string.Empty;

    public string TraktAccessToken { get; set; } = string.Empty;

    public string TraktRefreshToken { get; set; } = string.Empty;
}
