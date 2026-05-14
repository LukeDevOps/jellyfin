using Jellyfin.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamHub;

[ApiController]
[Authorize]
[Route("StreamHub")]
public class StreamHubController : BaseJellyfinApiController
{
    private readonly SearchService _searchService;
    private readonly TraktService _traktService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StreamHubController> _logger;

    public StreamHubController(SearchService searchService, TraktService traktService, IHttpClientFactory httpClientFactory, ILogger<StreamHubController> logger)
    {
        _searchService = searchService;
        _traktService = traktService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

        var apiKey = Environment.GetEnvironmentVariable("JELLYFIN_RD_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Real-Debrid is not configured. Set JELLYFIN_RD_API_KEY.");
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

    /// <summary>
    /// Starts the Trakt device auth flow. Returns the code the user enters at trakt.tv/activate.
    /// </summary>
    [HttpPost("trakt/auth/start")]
    [ProducesResponseType(typeof(TraktAuthStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TraktAuthStartResponse>> StartTraktAuth(CancellationToken cancellationToken)
    {
        var result = await _traktService.StartDeviceAuthAsync(cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Failed to start Trakt auth. Check JELLYFIN_TRAKT_CLIENT_ID is set.");
        }

        return Ok(new TraktAuthStartResponse(result.UserCode, result.VerificationUrl, result.DeviceCode, result.Interval));
    }

    /// <summary>
    /// Polls once to check if the user has completed Trakt activation.
    /// </summary>
    [HttpPost("trakt/auth/poll")]
    [ProducesResponseType(typeof(TraktAuthPollResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TraktAuthPollResponse>> PollTraktAuth(
        [FromBody] TraktAuthPollRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Trakt poll: DeviceCode={DeviceCode}", string.IsNullOrEmpty(request.DeviceCode) ? "(empty)" : request.DeviceCode[..8] + "…");
        var authenticated = await _traktService.PollForTokenAsync(request.DeviceCode, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Trakt poll result: authenticated={Authenticated}", authenticated);
        return Ok(new TraktAuthPollResponse(authenticated));
    }

    /// <summary>
    /// Returns the user's recent Trakt watch history.
    /// </summary>
    [HttpGet("trakt/history")]
    [ProducesResponseType(typeof(IReadOnlyList<TraktHistoryItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TraktHistoryItem>>> GetTraktHistory(
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var history = await _traktService.GetHistoryAsync(limit, cancellationToken).ConfigureAwait(false);
        return Ok(history);
    }

    /// <summary>
    /// Returns whether the user is authenticated with Trakt.
    /// </summary>
    [HttpGet("trakt/status")]
    [ProducesResponseType(typeof(TraktAuthPollResponse), StatusCodes.Status200OK)]
    public ActionResult<TraktAuthPollResponse> GetTraktStatus()
        => Ok(new TraktAuthPollResponse(_traktService.IsAuthenticated));
}

public record StreamRequest([property: System.Text.Json.Serialization.JsonPropertyName("magnetUrl")] string MagnetUrl);
public record StreamResponse(string Url);
public record TraktAuthStartResponse(string UserCode, string VerificationUrl, string DeviceCode, int Interval);
public record TraktAuthPollRequest([property: System.Text.Json.Serialization.JsonPropertyName("deviceCode")] string DeviceCode);
public record TraktAuthPollResponse(bool Authenticated);
