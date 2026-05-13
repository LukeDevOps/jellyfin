using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamHub;

public class RealDebridMediaSourceProvider : IMediaSourceProvider
{
    private readonly ILogger<RealDebridMediaSourceProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public RealDebridMediaSourceProvider(ILogger<RealDebridMediaSourceProvider> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
    {
        var apiKey = Plugin.Instance?.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Real-Debrid API key is not configured. Set the JELLYFIN_RD_API_KEY environment variable.");
            return [];
        }

        if (string.IsNullOrEmpty(item.Path))
        {
            return [];
        }

        var client = new RealDebridClient(_httpClientFactory.CreateClient(), apiKey);
        var directUrl = await client.UnrestrictLinkAsync(item.Path, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(directUrl))
        {
            _logger.LogDebug("Real-Debrid did not return a download URL for {Path}", item.Path);
            return [];
        }

        return
        [
            new MediaSourceInfo
            {
                Id = "realdebrid",
                Name = "Real-Debrid",
                Path = directUrl,
                Protocol = MediaProtocol.Http,
                IsRemote = true,
                SupportsDirectPlay = true,
                SupportsDirectStream = true
            }
        ];
    }

    public Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
