using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamingSources.Debrid;

/// <summary>
/// Minimal AllDebrid provider implementation.
/// </summary>
public sealed class AllDebridProvider : IDebridProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AllDebridProvider> _logger;

    public AllDebridProvider(HttpClient httpClient, ILogger<AllDebridProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Name => "AllDebrid";

    public async Task<DebridMagnetResult> AddMagnetAsync(string magnet, CancellationToken cancellationToken)
    {
        ConfigureClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["magnets[]"] = magnet
        });

        using var response = await _httpClient.PostAsync("magnet/upload", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AllDebridUploadResponse>(cancellationToken).ConfigureAwait(false);
        var magnetResult = payload?.Data?.Magnets?.FirstOrDefault();

        if (magnetResult is null || string.IsNullOrWhiteSpace(magnetResult.Id))
        {
            _logger.LogWarning("AllDebrid did not return a magnet id.");
            throw new InvalidOperationException("AllDebrid did not return a magnet id.");
        }

        return new DebridMagnetResult(magnetResult.Id, magnetResult.Hash ?? string.Empty);
    }

    public async Task<IReadOnlyList<DebridFile>> GetFilesAsync(string magnetId, CancellationToken cancellationToken)
    {
        ConfigureClient();

        using var response = await _httpClient.GetAsync($"magnet/status?id={Uri.EscapeDataString(magnetId)}", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AllDebridStatusResponse>(cancellationToken).ConfigureAwait(false);
        var links = payload?.Data?.Magnets?.Links ?? Array.Empty<AllDebridLink>();

        return links
            .Where(link => !string.IsNullOrWhiteSpace(link.Link))
            .Select(link => new DebridFile(link.Link!, link.Filename ?? "Unknown file", link.Size, link.Link))
            .ToArray();
    }

    public async Task<string> GetStreamingUrlAsync(string fileId, CancellationToken cancellationToken)
    {
        ConfigureClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["link"] = fileId
        });

        using var response = await _httpClient.PostAsync("link/unlock", content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AllDebridUnlockResponse>(cancellationToken).ConfigureAwait(false);
        var link = payload?.Data?.Link;

        if (string.IsNullOrWhiteSpace(link))
        {
            throw new InvalidOperationException("AllDebrid did not return a streaming URL.");
        }

        return link;
    }

    public Task<bool> IsCachedAsync(string hash, CancellationToken cancellationToken)
    {
        // AllDebrid cache checks vary by account/API behavior. Keep this explicit until wired to a verified endpoint.
        return Task.FromResult(false);
    }

    private void ConfigureClient()
    {
        var configuration = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("Plugin configuration is not available.");

        if (string.IsNullOrWhiteSpace(configuration.AllDebridApiKey))
        {
            throw new InvalidOperationException("AllDebrid API key is missing.");
        }

        _httpClient.BaseAddress = new Uri("https://api.alldebrid.com/v4/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, configuration.TimeoutSeconds));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.AllDebridApiKey);
    }

    private sealed class AllDebridUploadResponse
    {
        [JsonPropertyName("data")]
        public AllDebridUploadData? Data { get; set; }
    }

    private sealed class AllDebridUploadData
    {
        [JsonPropertyName("magnets")]
        public AllDebridUploadedMagnet[]? Magnets { get; set; }
    }

    private sealed class AllDebridUploadedMagnet
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
    }

    private sealed class AllDebridStatusResponse
    {
        [JsonPropertyName("data")]
        public AllDebridStatusData? Data { get; set; }
    }

    private sealed class AllDebridStatusData
    {
        [JsonPropertyName("magnets")]
        public AllDebridMagnetStatus? Magnets { get; set; }
    }

    private sealed class AllDebridMagnetStatus
    {
        [JsonPropertyName("links")]
        public AllDebridLink[]? Links { get; set; }
    }

    private sealed class AllDebridLink
    {
        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("link")]
        public string? Link { get; set; }
    }

    private sealed class AllDebridUnlockResponse
    {
        [JsonPropertyName("data")]
        public AllDebridUnlockData? Data { get; set; }
    }

    private sealed class AllDebridUnlockData
    {
        [JsonPropertyName("link")]
        public string? Link { get; set; }
    }
}
