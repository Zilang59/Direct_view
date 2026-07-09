using System.Reflection;
using System.Text.Json;

namespace Jellyfin.Plugin.StreamingSources.Web;

/// <summary>
/// File Transformation callback for injecting the Streaming Sources client script.
/// </summary>
public static class WebIndexTransformer
{
    private const string Marker = "streaming-sources-client-loader";
    private const string ScriptTag = "<script id=\"streaming-sources-client-loader\" src=\"/StreamingSources/Web/streamingSources.js\" defer></script>";

    public static object InjectClientScript(object payload)
    {
        string contents = ExtractContents(payload);
        if (string.IsNullOrWhiteSpace(contents) || contents.Contains(Marker, StringComparison.OrdinalIgnoreCase))
        {
            return contents;
        }

        if (!contents.Contains("<html", StringComparison.OrdinalIgnoreCase) &&
            !contents.Contains("</body>", StringComparison.OrdinalIgnoreCase))
        {
            return contents;
        }

        string updated = contents.Contains("</body>", StringComparison.OrdinalIgnoreCase)
            ? ReplaceLastIgnoreCase(contents, "</body>", ScriptTag + "</body>")
            : contents + ScriptTag;

        return updated;
    }

    private static string ExtractContents(object payload)
    {
        PropertyInfo? contentsProperty = payload.GetType().GetProperty("contents")
            ?? payload.GetType().GetProperty("Contents");

        if (contentsProperty?.GetValue(payload) is string directContents)
        {
            return directContents;
        }

        string raw = payload.ToString() ?? string.Empty;
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            if (document.RootElement.TryGetProperty("contents", out JsonElement contentsElement))
            {
                return contentsElement.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static string ReplaceLastIgnoreCase(string value, string search, string replacement)
    {
        int index = value.LastIndexOf(search, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? value
            : value[..index] + replacement + value[(index + search.Length)..];
    }
}
