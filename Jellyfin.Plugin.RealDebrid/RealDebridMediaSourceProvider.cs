using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RealDebrid;

public class RealDebridMediaSourceProvider : IMediaSourceProvider
{
    private readonly ILogger<RealDebridMediaSourceProvider> _logger;

    public RealDebridMediaSourceProvider(ILogger<RealDebridMediaSourceProvider> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
    {
        var apiKey = Plugin.Instance?.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Real-Debrid API key is not configured. Set the JELLYFIN_RD_API_KEY environment variable.");
            return Task.FromResult(Enumerable.Empty<MediaSourceInfo>());
        }

        // TODO: call Real-Debrid /unrestrict/link with the item path and return a direct URL
        return Task.FromResult(Enumerable.Empty<MediaSourceInfo>());
    }

    public Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
