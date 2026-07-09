using Jellyfin.Plugin.StreamingSources.Cache;
using Jellyfin.Plugin.StreamingSources.Debrid;
using Jellyfin.Plugin.StreamingSources.ExternalApi;
using Jellyfin.Plugin.StreamingSources.Models;
using Jellyfin.Plugin.StreamingSources.Playback;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Jellyfin.Plugin.StreamingSources.Controllers;

/// <summary>
/// Internal API used by the Jellyfin web UI extension.
/// </summary>
[ApiController]
[Authorize]
[Route("StreamingSources")]
public sealed class StreamingSourcesController : ControllerBase
{
    private readonly IExternalSourceClient _externalSourceClient;
    private readonly IDebridProvider _debridProvider;
    private readonly ISourceCache _sourceCache;
    private readonly StreamingSourcesMediaSourceProvider _mediaSourceProvider;
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<StreamingSourcesController> _logger;

    public StreamingSourcesController(
        IExternalSourceClient externalSourceClient,
        IDebridProvider debridProvider,
        ISourceCache sourceCache,
        StreamingSourcesMediaSourceProvider mediaSourceProvider,
        IMediaSourceManager mediaSourceManager,
        ILibraryManager libraryManager,
        IUserManager userManager,
        ILogger<StreamingSourcesController> logger)
    {
        _externalSourceClient = externalSourceClient;
        _debridProvider = debridProvider;
        _sourceCache = sourceCache;
        _mediaSourceProvider = mediaSourceProvider;
        _mediaSourceManager = mediaSourceManager;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("Search")]
    public async Task<ActionResult<IReadOnlyList<StreamingSource>>> SearchAsync(
        [FromBody] MediaLookupRequest request,
        CancellationToken cancellationToken)
    {
        var sources = await _externalSourceClient.SearchAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(sources);
    }

    [HttpPost("Resolve")]
    public async Task<ActionResult<ResolvedSourceResponse>> ResolveAsync(
        [FromBody] ResolveSourceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JellyfinItemId))
        {
            return BadRequest("JellyfinItemId is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Source.DirectUrl))
        {
            var directNow = DateTimeOffset.UtcNow;
            await _sourceCache.SetAsync(
                new CachedSource(request.JellyfinItemId, request.Source.Provider, request.Source.Hash, string.Empty, request.Source.DirectUrl, directNow, directNow),
                cancellationToken).ConfigureAwait(false);

            return Ok(CreateResponse(request.JellyfinItemId, request.Source.DirectUrl, request.Source.Provider, request.Source.Hash, false));
        }

        if (string.IsNullOrWhiteSpace(request.Source.Magnet))
        {
            return BadRequest("Source magnet or direct URL is required.");
        }

        var cached = await _sourceCache.GetAsync(request.JellyfinItemId, cancellationToken).ConfigureAwait(false);
        if (cached?.StreamingUrl is not null && !request.ForceRefresh)
        {
            return Ok(CreateResponse(request.JellyfinItemId, cached.StreamingUrl, cached.Provider, cached.TorrentHash, true));
        }

        var magnet = await _debridProvider.AddMagnetAsync(request.Source.Magnet, cancellationToken).ConfigureAwait(false);
        var files = await _debridProvider.GetFilesAsync(magnet.MagnetId, cancellationToken).ConfigureAwait(false);
        var selectedFile = files
            .OrderByDescending(file => file.SizeBytes)
            .FirstOrDefault();

        if (selectedFile is null)
        {
            return StatusCode(502, "No streamable file was returned by the Debrid provider.");
        }

        var streamingUrl = await _debridProvider.GetStreamingUrlAsync(selectedFile.Link ?? selectedFile.Id, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        await _sourceCache.SetAsync(
            new CachedSource(request.JellyfinItemId, _debridProvider.Name, request.Source.Hash, magnet.MagnetId, streamingUrl, now, now),
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Resolved external source for Jellyfin item {JellyfinItemId} with provider {Provider}.", request.JellyfinItemId, _debridProvider.Name);
        return Ok(CreateResponse(request.JellyfinItemId, streamingUrl, _debridProvider.Name, request.Source.Hash, false));
    }

    private static ResolvedSourceResponse CreateResponse(
        string jellyfinItemId,
        string streamingUrl,
        string provider,
        string torrentHash,
        bool fromCache)
    {
        return new ResolvedSourceResponse(
            streamingUrl,
            provider,
            torrentHash,
            fromCache,
            StreamingSourcesMediaSourceProvider.GetMediaSourceId(jellyfinItemId));
    }

    [HttpDelete("Cache/{jellyfinItemId}")]
    public async Task<IActionResult> ClearCacheAsync(string jellyfinItemId, CancellationToken cancellationToken)
    {
        await _sourceCache.RemoveAsync(jellyfinItemId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("Debug/MediaSource/{jellyfinItemId}")]
    public async Task<IActionResult> DebugMediaSourceAsync(
        string jellyfinItemId,
        [FromQuery] string? userId,
        CancellationToken cancellationToken)
    {
        var cached = await _sourceCache.GetAsync(jellyfinItemId, cancellationToken).ConfigureAwait(false);
        var expectedMediaSourceId = StreamingSourcesMediaSourceProvider.GetMediaSourceId(jellyfinItemId);
        var playbackSources = new List<object>();
        var playbackSourceError = string.Empty;
        var playbackSourcesContainExpected = false;

        try
        {
            if (Guid.TryParse(jellyfinItemId, out var itemGuid))
            {
                var item = _libraryManager.GetItemById(itemGuid);
                var user = Guid.TryParse(userId, out var userGuid)
                    ? _userManager.GetUserById(userGuid)
                    : null;

                if (item is not null && user is not null)
                {
                    var sources = await _mediaSourceManager
                        .GetPlaybackMediaSources(item, user, true, true, cancellationToken)
                        .ConfigureAwait(false);

                    playbackSources.AddRange(sources.Select(source => new
                    {
                        source.Id,
                        source.Name,
                        source.Protocol,
                        source.IsRemote,
                        source.SupportsDirectPlay,
                        source.SupportsDirectStream,
                        source.SupportsTranscoding,
                        HasPath = !string.IsNullOrWhiteSpace(source.Path),
                        IsExpected = string.Equals(source.Id, expectedMediaSourceId, StringComparison.OrdinalIgnoreCase)
                    }));
                    playbackSourcesContainExpected = sources.Any(source =>
                        string.Equals(source.Id, expectedMediaSourceId, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    playbackSourceError = item is null ? "Item not found." : "User not found.";
                }
            }
            else
            {
                playbackSourceError = "Invalid Jellyfin item id.";
            }
        }
        catch (Exception ex)
        {
            playbackSourceError = ex.Message;
        }

        return Ok(new
        {
            ItemId = jellyfinItemId,
            ExpectedMediaSourceId = expectedMediaSourceId,
            HasCachedSource = !string.IsNullOrWhiteSpace(cached?.StreamingUrl),
            cached?.Provider,
            cached?.TorrentHash,
            HasStreamingUrl = !string.IsNullOrWhiteSpace(cached?.StreamingUrl),
            ProviderType = _mediaSourceProvider.GetType().FullName,
            PlaybackSourceError = playbackSourceError,
            PlaybackSources = playbackSources,
            PlaybackSourcesContainExpected = playbackSourcesContainExpected
        });
    }

    [HttpGet("Web/streamingSources.js")]
    [AllowAnonymous]
    public IActionResult GetClientScript()
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Jellyfin.Plugin.StreamingSources.Web.streamingSources.js";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return NotFound();
        }

        using var reader = new StreamReader(stream);
        return Content(reader.ReadToEnd(), "application/javascript");
    }
}
