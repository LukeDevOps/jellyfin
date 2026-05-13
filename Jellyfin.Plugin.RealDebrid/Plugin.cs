using Jellyfin.Plugin.RealDebrid.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RealDebrid;

public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override Guid Id => new Guid("d4f5a3b2-1c6e-4d8f-9a2b-7e3c5f1d0e4a");

    public override string Name => "Real-Debrid";

    public override string Description => "Stream media via Real-Debrid.";

    public string? GetApiKey() =>
        Environment.GetEnvironmentVariable("JELLYFIN_RD_API_KEY")
        ?? (string.IsNullOrEmpty(Configuration.ApiKey) ? null : Configuration.ApiKey);

    public string? GetProwlarrUrl() =>
        Environment.GetEnvironmentVariable("JELLYFIN_PROWLARR_URL")
        ?? (string.IsNullOrEmpty(Configuration.ProwlarrUrl) ? null : Configuration.ProwlarrUrl);

    public string? GetProwlarrApiKey() =>
        Environment.GetEnvironmentVariable("JELLYFIN_PROWLARR_API_KEY")
        ?? (string.IsNullOrEmpty(Configuration.ProwlarrApiKey) ? null : Configuration.ProwlarrApiKey);
}
