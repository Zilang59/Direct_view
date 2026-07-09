namespace Jellyfin.Plugin.StreamingSources.Debrid;

public sealed record DebridMagnetResult(string MagnetId, string Hash);

public sealed record DebridFile(string Id, string Name, long SizeBytes, string? Link);
