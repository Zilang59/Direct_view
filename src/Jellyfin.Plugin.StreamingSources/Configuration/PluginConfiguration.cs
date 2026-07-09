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
    /// Gets or sets newline-separated Stremio/Lumio manifest URLs.
    /// </summary>
    public string StremioManifestUrls { get; set; } = string.Empty;

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
    public int MaxResults { get; set; }

    /// <summary>
    /// Gets or sets the default sort key.
    /// </summary>
    public string DefaultSort { get; set; } = "quality";

    /// <summary>
    /// Gets or sets a value indicating whether the external API should be queried.
    /// </summary>
    public bool EnableExternalApi { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Stremio-compatible stream addons should be queried.
    /// </summary>
    public bool EnableStremioAddons { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the plugin should register a Jellyfin Web transformation.
    /// Disabled by default because a broken web transformation can make Jellyfin Web unavailable.
    /// </summary>
    public bool EnableWebButtonInjection { get; set; }
}
