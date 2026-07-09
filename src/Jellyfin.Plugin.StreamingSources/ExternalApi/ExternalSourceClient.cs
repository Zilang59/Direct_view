using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
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

        var sources = new List<StreamingSource>();

        if (configuration.EnableExternalApi && !string.IsNullOrWhiteSpace(configuration.ExternalApiUrl))
        {
            sources.AddRange(await SearchExternalApiAsync(configuration, request, cancellationToken).ConfigureAwait(false));
        }

        if (configuration.EnableStremioAddons && !string.IsNullOrWhiteSpace(configuration.StremioManifestUrls))
        {
            sources.AddRange(await SearchStremioAddonsAsync(configuration, request, cancellationToken).ConfigureAwait(false));
        }

        var filteredSources = sources
            .Where(source => configuration.MaxSizeGb <= 0 || source.SizeBytes <= configuration.MaxSizeGb * 1024L * 1024L * 1024L)
            .Take(Math.Max(1, configuration.MaxResults))
            .ToArray();

        _logger.LogInformation("Source search returned {SourceCount} source(s).", filteredSources.Length);
        return filteredSources;
    }

    private async Task<IReadOnlyList<StreamingSource>> SearchExternalApiAsync(
        Configuration.PluginConfiguration configuration,
        MediaLookupRequest request,
        CancellationToken cancellationToken)
    {
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
        return sources ?? Array.Empty<StreamingSource>();
    }

    private async Task<IReadOnlyList<StreamingSource>> SearchStremioAddonsAsync(
        Configuration.PluginConfiguration configuration,
        MediaLookupRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<StreamingSource>();
        foreach (var manifestUrl in StremioManifestUrls(configuration.StremioManifestUrls))
        {
            results.AddRange(await SearchStremioAddonAsync(configuration, manifestUrl, request, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    private async Task<IReadOnlyList<StreamingSource>> SearchStremioAddonAsync(
        Configuration.PluginConfiguration configuration,
        Uri manifestUrl,
        MediaLookupRequest request,
        CancellationToken cancellationToken)
    {
        var streamUrls = BuildStremioStreamUrls(manifestUrl, request);
        var sources = new List<StreamingSource>();

        foreach (var streamUrl in streamUrls)
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, streamUrl);
            requestMessage.Headers.Accept.ParseAdd("application/json");

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stremio addon {ManifestUrl} returned status {StatusCode}.", manifestUrl, response.StatusCode);
                continue;
            }

            var payload = await response.Content.ReadFromJsonAsync<StremioStreamResponse>(cancellationToken).ConfigureAwait(false);
            foreach (var stream in payload?.Streams ?? Array.Empty<StremioStream>())
            {
                var source = ConvertStremioStream(manifestUrl, stream);
                if (source is not null)
                {
                    sources.Add(source);
                }
            }
        }

        return sources;
    }

    private static IEnumerable<Uri> StremioManifestUrls(string value)
    {
        return value
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null)
            .Where(uri => uri is not null)!
            .Cast<Uri>();
    }

    private static IEnumerable<Uri> BuildStremioStreamUrls(Uri manifestUrl, MediaLookupRequest request)
    {
        var baseUrl = manifestUrl.ToString();
        if (baseUrl.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl = baseUrl[..^"/manifest.json".Length];
        }
        baseUrl = baseUrl.TrimEnd('/');

        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.ImdbId))
        {
            ids.Add(request.ImdbId);
        }
        if (!string.IsNullOrWhiteSpace(request.TmdbId))
        {
            ids.Add("tmdb:" + request.TmdbId);
        }

        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (request.SeasonNumber is > 0 && request.EpisodeNumber is > 0)
            {
                yield return new Uri($"{baseUrl}/stream/series/{Uri.EscapeDataString(id + ":" + request.SeasonNumber + ":" + request.EpisodeNumber)}.json");
            }
            else
            {
                yield return new Uri($"{baseUrl}/stream/movie/{Uri.EscapeDataString(id)}.json");
            }
        }
    }

    private static StreamingSource? ConvertStremioStream(Uri manifestUrl, StremioStream stream)
    {
        var directUrl = FirstNonEmpty(stream.Url, stream.ExternalUrl);
        var hash = FirstNonEmpty(stream.InfoHash, HashFromMagnet(stream.ExternalUrl), HashFromMagnet(stream.Url));
        var magnet = !string.IsNullOrWhiteSpace(hash) ? "magnet:?xt=urn:btih:" + hash : string.Empty;

        if (string.IsNullOrWhiteSpace(directUrl) && string.IsNullOrWhiteSpace(magnet))
        {
            return null;
        }

        var description = FirstNonEmpty(stream.Description, stream.BehaviorHints?.Filename, stream.Title, stream.Name);
        var title = BuildStremioTitle(stream, description);
        var sizeBytes = stream.BehaviorHints?.VideoSize ?? GuessSizeBytes(description);
        var isWebReady = stream.BehaviorHints?.NotWebReady != true;

        return new StreamingSource
        {
            Name = title,
            SizeBytes = sizeBytes,
            Seeders = 0,
            Language = GuessLanguage(description),
            Quality = GuessQuality(description),
            Codec = GuessCodec(description),
            IsHdr = description.Contains("HDR", StringComparison.OrdinalIgnoreCase),
            IsDolbyVision = description.Contains("DV", StringComparison.OrdinalIgnoreCase) || description.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase),
            Hash = hash,
            Magnet = magnet,
            DirectUrl = directUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? string.Empty : directUrl,
            Provider = manifestUrl.Host,
            Description = description,
            IsWebReady = isWebReady
        };
    }

    private static string BuildStremioTitle(StremioStream stream, string description)
    {
        var parts = new[]
        {
            stream.Name,
            GuessQuality(description),
            GuessLanguage(description),
            GuessCodec(description),
            GuessReleaseType(description)
        };

        var title = string.Join(" - ", parts.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(title) ? FirstNonEmpty(stream.Title, stream.Name, "Stremio source") : title;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string HashFromMagnet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = System.Text.RegularExpressions.Regex.Match(value, "btih:([a-fA-F0-9]{40})");
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
    }

    private static string GuessQuality(string title)
    {
        if (title.Contains("2160p", StringComparison.OrdinalIgnoreCase) || title.Contains("4K", StringComparison.OrdinalIgnoreCase))
        {
            return "2160p";
        }
        if (title.Contains("1080p", StringComparison.OrdinalIgnoreCase))
        {
            return "1080p";
        }
        if (title.Contains("720p", StringComparison.OrdinalIgnoreCase))
        {
            return "720p";
        }
        return string.Empty;
    }

    private static string GuessLanguage(string title)
    {
        if (title.Contains("VOSTFR", StringComparison.OrdinalIgnoreCase))
        {
            return "VOSTFR";
        }
        if (title.Contains("VF", StringComparison.OrdinalIgnoreCase) || title.Contains("French", StringComparison.OrdinalIgnoreCase))
        {
            return "VF";
        }
        if (title.Contains("Multi", StringComparison.OrdinalIgnoreCase))
        {
            return "MULTI";
        }
        return string.Empty;
    }

    private static string GuessCodec(string title)
    {
        foreach (var codec in new[] { "x265", "x264", "HEVC", "AV1", "H264", "H265" })
        {
            if (title.Contains(codec, StringComparison.OrdinalIgnoreCase))
            {
                return codec;
            }
        }

        return string.Empty;
    }

    private static string GuessReleaseType(string title)
    {
        foreach (var type in new[] { "REMUX", "BluRay", "WEB-DL", "WEBRip", "HDTV" })
        {
            if (title.Contains(type, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return string.Empty;
    }

    private static long GuessSizeBytes(string title)
    {
        var match = Regex.Match(title, @"(\d+(?:[\.,]\d+)?)\s*(Go|GB|GiB|Mo|MB|MiB)", RegexOptions.IgnoreCase);
        if (!match.Success ||
            !double.TryParse(match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        var unit = match.Groups[2].Value.ToLowerInvariant();
        var multiplier = unit is "mo" or "mb" or "mib" ? 1024L * 1024L : 1024L * 1024L * 1024L;
        return (long)(value * multiplier);
    }

    private sealed class StremioStreamResponse
    {
        [JsonPropertyName("streams")]
        public StremioStream[]? Streams { get; set; }
    }

    private sealed class StremioStream
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("externalUrl")]
        public string? ExternalUrl { get; set; }

        [JsonPropertyName("infoHash")]
        public string? InfoHash { get; set; }

        [JsonPropertyName("behaviorHints")]
        public StremioBehaviorHints? BehaviorHints { get; set; }
    }

    private sealed class StremioBehaviorHints
    {
        [JsonPropertyName("videoSize")]
        public long? VideoSize { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("notWebReady")]
        public bool? NotWebReady { get; set; }
    }
}
