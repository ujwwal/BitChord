using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BitChord.Core;

public record CanvasArtwork(string VideoUrl, string? Title, string? Artist, string? Album = null);

public sealed class CanvasService
{
    private const string TidalEmbedToken = "vNVdglQOjFJJGG2U";
    private const string TidalSearchEndpoint = "https://api.tidal.com/v1/search";
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ConcurrentDictionary<string, CanvasArtwork?> _cache = new();

    public async Task<CanvasArtwork?> GetCanvasAsync(string videoId, string title, string artist, string? album = null, CancellationToken cancellationToken = default)
    {
        string key = $"{videoId}|{title}|{artist}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var tidal = await SearchTidalCanvasAsync(title, artist, album, cancellationToken).ConfigureAwait(false);
            if (tidal is not null)
            {
                _cache[key] = tidal;
                return tidal;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Canvas lookup failed for '{title}': {ex.Message}");
        }

        _cache[key] = null;
        return null;
    }

    private static async Task<CanvasArtwork?> SearchTidalCanvasAsync(string title, string artist, string? album, CancellationToken cancellationToken)
    {
        string cleanTitle = CleanNoise(title);
        string cleanArtist = CleanNoise(artist);
        string query = Uri.EscapeDataString(string.IsNullOrWhiteSpace(album) ? $"{cleanArtist} {cleanTitle}" : $"{album} {cleanArtist} {cleanTitle}");

        string url = $"{TidalSearchEndpoint}?query={query}&limit=10&types=TRACKS&countryCode=US";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Tidal-Token", TidalEmbedToken);
        request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");

        using var response = await SharedHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonNode.Parse(json);
        var items = doc?["tracks"]?["items"]?.AsArray();
        if (items is null) return null;

        foreach (var item in items)
        {
            if (item is null) continue;
            string trackTitle = item["title"]?.GetValue<string>() ?? "";
            var albumObj = item["album"];
            string? videoCover = albumObj?["videoCover"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(videoCover))
            {
                string? coverUrl = FormatTidalVideoUrl(videoCover);
                if (coverUrl is not null)
                {
                    AppLogger.Info($"Found Tidal motion canvas for '{trackTitle}': {coverUrl}");
                    return new CanvasArtwork(coverUrl, trackTitle, artist, albumObj?["title"]?.GetValue<string>());
                }
            }
        }

        return null;
    }

    private static string? FormatTidalVideoUrl(string id)
    {
        var parts = id.Split('-');
        if (parts.Length != 5) return null;
        return $"https://resources.tidal.com/videos/{string.Join('/', parts)}/1280x1280.mp4";
    }

    private static string CleanNoise(string input)
    {
        string cleaned = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\((?:from|official|lyrical|video|audio)[^)]*\)|\[[^]]*]|\b(?:official (?:video|audio|music video)|lyrical|full song|4k video)\b",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        int pipeIdx = cleaned.IndexOf('|');
        if (pipeIdx >= 0) cleaned = cleaned[..pipeIdx];

        return System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
    }
}
