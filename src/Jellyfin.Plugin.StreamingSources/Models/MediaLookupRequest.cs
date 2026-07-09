namespace Jellyfin.Plugin.StreamingSources.Models;

/// <summary>
/// Describes the media requested from the external source API.
/// </summary>
public sealed class MediaLookupRequest
{
    public string? JellyfinItemId { get; set; }

    public string? Title { get; set; }

    public int? Year { get; set; }

    public string? ImdbId { get; set; }

    public string? TmdbId { get; set; }

    public string? TvdbId { get; set; }

    public int? SeasonNumber { get; set; }

    public int? EpisodeNumber { get; set; }
}
