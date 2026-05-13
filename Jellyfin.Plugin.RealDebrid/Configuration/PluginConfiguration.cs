using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RealDebrid.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
}
