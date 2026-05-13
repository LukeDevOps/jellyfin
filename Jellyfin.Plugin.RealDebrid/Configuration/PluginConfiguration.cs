using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RealDebrid.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;

    public string ProwlarrUrl { get; set; } = string.Empty;

    public string ProwlarrApiKey { get; set; } = string.Empty;
}
