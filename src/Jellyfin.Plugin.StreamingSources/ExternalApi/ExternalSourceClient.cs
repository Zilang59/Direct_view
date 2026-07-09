using System.Net.Http.Json;
using Jellyfin.Plugin.StreamingSources.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.StreamingSources.ExternalApi;

public sealed class ExternalSourceClient : IExternalSourceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalSourceClient> _logger;

    public ExternalSourceClient(HttpClient httpClient, ILogger<ExternalSourceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StreamingSource>> SearchAsync(MediaLookupRequest request, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("Plugin configuration is not available.");

        if (!Uri.TryCreate(configuration.ExternalApiUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("External API URL is invalid.");
        }

        _httpClient.BaseAddress = baseUri;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, configuration.TimeoutSeconds));
        _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");

        if (!string.IsNullOrWhiteSpace(configuration.ExternalApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", configuration.ExternalApiKey);
        }

        using var response = await _httpClient.PostAsJsonAsync("sources/search", request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var sources = await response.Content.ReadFromJsonAsync<StreamingSource[]>(cancellationToken).ConfigureAwait(false);
        var filteredSources = (sources ?? Array.Empty<StreamingSource>())
            .Where(source => configuration.MaxSizeGb <= 0 || source.SizeBytes <= configuration.MaxSizeGb * 1024L * 1024L * 1024L)
            .Take(Math.Max(1, configuration.MaxResults))
            .ToArray();

        _logger.LogInformation("External source API returned {SourceCount} source(s).", filteredSources.Length);

        return filteredSources;
    }
}
