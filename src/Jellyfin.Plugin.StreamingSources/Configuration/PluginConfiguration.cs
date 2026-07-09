using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.StreamingSources.Configuration;

/// <summary>
/// Stores Streaming Sources plugin settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the external torrent search API base URL.
    /// </summary>
    public string ExternalApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external torrent search API key.
    /// </summary>
    public string ExternalApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Debrid provider name.
    /// </summary>
    public string DebridProvider { get; set; } = "AllDebrid";

    /// <summary>
    /// Gets or sets the AllDebrid API key.
    /// </summary>
    public string AllDebridApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum result size in gigabytes. Zero disables this filter.
    /// </summary>
    public int MaxSizeGb { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results to display.
    /// </summary>
    public int MaxResults { get; set; } = 25;

    /// <summary>
    /// Gets or sets the default sort key.
    /// </summary>
    public string DefaultSort { get; set; } = "quality";
}
