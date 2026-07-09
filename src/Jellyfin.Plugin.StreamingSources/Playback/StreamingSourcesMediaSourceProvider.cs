using Jellyfin.Plugin.StreamingSources.Cache;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.StreamingSources.Playback;

public sealed class StreamingSourcesMediaSourceProvider : IMediaSourceProvider
{
    private const string SourcePrefix = "streaming-sources:";
    private readonly ISourceCache _sourceCache;

    public StreamingSourcesMediaSourceProvider(ISourceCache sourceCache)
    {
        _sourceCache = sourceCache;
    }

    public async Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken)
    {
        var cached = await _sourceCache.GetAsync(item.Id.ToString("N"), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(cached?.StreamingUrl))
        {
            return Array.Empty<MediaSourceInfo>();
        }

        return new[]
        {
            BuildMediaSource(item, cached.StreamingUrl, cached.Provider)
        };
    }

    public Task<ILiveStream> OpenMediaSource(
        string openToken,
        List<ILiveStream> currentLiveStreams,
        CancellationToken cancellationToken)
    {
        return Task.FromException<ILiveStream>(
            new NotSupportedException("Streaming Sources media sources are direct HTTP sources and do not require opening."));
    }

    private static MediaSourceInfo BuildMediaSource(BaseItem item, string streamingUrl, string provider)
    {
        return new MediaSourceInfo
        {
            Id = SourcePrefix + item.Id.ToString("N"),
            Name = string.IsNullOrWhiteSpace(provider) ? "Streaming Sources" : $"Streaming Sources - {provider}",
            Path = streamingUrl,
            Protocol = MediaProtocol.Http,
            IsRemote = true,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            RequiresOpening = false,
            RequiresClosing = false,
            RunTimeTicks = item.RunTimeTicks
        };
    }
}
