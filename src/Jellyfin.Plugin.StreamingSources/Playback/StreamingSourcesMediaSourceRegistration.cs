using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamingSources.Playback;

public sealed class StreamingSourcesMediaSourceRegistration : IHostedService
{
    private readonly IMediaSourceManager _mediaSourceManager;
    private readonly StreamingSourcesMediaSourceProvider _provider;
    private readonly ILogger<StreamingSourcesMediaSourceRegistration> _logger;

    public StreamingSourcesMediaSourceRegistration(
        IMediaSourceManager mediaSourceManager,
        StreamingSourcesMediaSourceProvider provider,
        ILogger<StreamingSourcesMediaSourceRegistration> logger)
    {
        _mediaSourceManager = mediaSourceManager;
        _provider = provider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _mediaSourceManager.AddParts(new[] { _provider });
        _logger.LogInformation("Streaming Sources media source provider registered.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
