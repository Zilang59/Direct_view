namespace Jellyfin.Plugin.StreamingSources.Models;

public sealed class ResolveSourceRequest
{
    public string JellyfinItemId { get; set; } = string.Empty;

    public StreamingSource Source { get; set; } = new();

    public bool ForceRefresh { get; set; }
}
