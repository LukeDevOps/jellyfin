using Jellyfin.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RealDebrid;

[ApiController]
[Authorize]
[Route("RealDebrid")]
public class RealDebridController : BaseJellyfinApiController
{
    private readonly SearchService _searchService;
    private readonly IHttpClientFactory _httpClientFactory;

    public RealDebridController(SearchService searchService, IHttpClientFactory httpClientFactory)
    {
        _searchService = searchService;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Search for media via Prowlarr and return RD-cached results.
    /// </summary>
    /// <param name="query">Search term.</param>
    /// <param name="category">Prowlarr category (2000 = Movies, 5000 = TV). Defaults to Movies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<DebridSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DebridSearchResult>>> Search(
        [FromQuery] string query,
        [FromQuery] int category = ProwlarrClient.CategoryMovies,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query is required.");
        }

        var results = await _searchService.SearchAsync(query, category, cancellationToken).ConfigureAwait(false);
        return Ok(results);
    }

    /// <summary>
    /// Add a magnet to Real-Debrid and return a direct stream URL.
    /// </summary>
    [HttpPost("stream")]
    [ProducesResponseType(typeof(StreamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<StreamResponse>> GetStreamUrl(
        [FromBody] StreamRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.MagnetUrl))
        {
            return BadRequest("MagnetUrl is required.");
        }

        var apiKey = Plugin.Instance?.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Real-Debrid is not configured.");
        }

        var rd = new RealDebridClient(_httpClientFactory.CreateClient(), apiKey);

        var torrentId = await rd.AddMagnetAsync(request.MagnetUrl, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(torrentId))
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Failed to add magnet to Real-Debrid.");
        }

        var links = await rd.SelectFilesAndGetLinksAsync(torrentId, cancellationToken).ConfigureAwait(false);
        if (links.Count == 0)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Real-Debrid returned no links for this torrent.");
        }

        // Unrestrict the first (largest) link to get a direct stream URL
        var directUrl = await rd.UnrestrictLinkAsync(links[0], cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(directUrl))
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Failed to unrestrict link via Real-Debrid.");
        }

        return Ok(new StreamResponse(directUrl));
    }
}

public record StreamRequest(string MagnetUrl);
public record StreamResponse(string Url);
