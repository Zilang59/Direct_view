namespace Jellyfin.Plugin.StreamingSources.Models;

/// <summary>
/// Represents one torrent source returned by the external API.
/// </summary>
public sealed class StreamingSource
{
    public string Name { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int Seeders { get; set; }

    public string Language { get; set; } = string.Empty;

    public string Quality { get; set; } = string.Empty;

    public string Codec { get; set; } = string.Empty;

    public bool IsHdr { get; set; }

    public bool IsDolbyVision { get; set; }

    public string Hash { get; set; } = string.Empty;

    public string Magnet { get; set; } = string.Empty;

    public string DirectUrl { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsWebReady { get; set; } = true;
}
