namespace Jellyfin.Plugin.StreamingSources.Models;

public sealed record ResolvedSourceResponse(
    string StreamingUrl,
    string Provider,
    string TorrentHash,
    bool FromCache);
