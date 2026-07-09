using Jellyfin.Plugin.StreamingSources.Cache;
using Jellyfin.Plugin.StreamingSources.Debrid;
using Jellyfin.Plugin.StreamingSources.ExternalApi;
using Jellyfin.Plugin.StreamingSources.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<StreamingSourcesController> _logger;

    public StreamingSourcesController(
        IExternalSourceClient externalSourceClient,
        IDebridProvider debridProvider,
        ISourceCache sourceCache,
        ILogger<StreamingSourcesController> logger)
    {
        _externalSourceClient = externalSourceClient;
        _debridProvider = debridProvider;
        _sourceCache = sourceCache;
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

        if (string.IsNullOrWhiteSpace(request.Source.Magnet))
        {
            return BadRequest("Source magnet is required.");
        }

        var cached = await _sourceCache.GetAsync(request.JellyfinItemId, cancellationToken).ConfigureAwait(false);
        if (cached?.StreamingUrl is not null && !request.ForceRefresh)
        {
            return Ok(new ResolvedSourceResponse(cached.StreamingUrl, cached.Provider, cached.TorrentHash, true));
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
        return Ok(new ResolvedSourceResponse(streamingUrl, _debridProvider.Name, request.Source.Hash, false));
    }

    [HttpDelete("Cache/{jellyfinItemId}")]
    public async Task<IActionResult> ClearCacheAsync(string jellyfinItemId, CancellationToken cancellationToken)
    {
        await _sourceCache.RemoveAsync(jellyfinItemId, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
