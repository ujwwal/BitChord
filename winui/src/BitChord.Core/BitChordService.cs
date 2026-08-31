using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitChord.Core;

public sealed class BitChordService
{
    private static readonly HttpClient ProbeClient = new() { Timeout = TimeSpan.FromSeconds(6) };
    private readonly AnonymousInnertubeClient _client;
    private readonly LrcLibClient _lrcClient = new();
    private readonly AudioStreamCache _audioCache = new();
    private readonly CanvasService _canvasService = new();

    public AudioStreamCache AudioCache => _audioCache;
    public CanvasService Canvas => _canvasService;

    public BitChordService(AnonymousInnertubeClient? client = null)
    {
        _client = client ?? AnonymousInnertubeClient.Shared;
    }

    public async Task<IReadOnlyList<HomeShelf>> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        JsonElement response = await _client.BrowseAsync("FEmusic_home", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseHome(response).ToList();
    }

    public async Task<IReadOnlyList<HomeShelf>> GetExploreAsync(CancellationToken cancellationToken = default)
    {
        JsonElement exploreResponse = await _client.BrowseAsync("FEmusic_explore", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        JsonElement chartsResponse = await _client.BrowseAsync("FEmusic_charts", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        List<HomeShelf> shelves = new();
        shelves.AddRange(InnertubeParser.ParseHome(exploreResponse));
        shelves.AddRange(InnertubeParser.ParseHome(chartsResponse));

        return shelves
            .GroupBy(shelf => shelf.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        SearchFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<SearchResult>();
        }

        JsonElement response = await _client.SearchAsync(query, filter.GetParameters(), cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseSearch(response).ToList();
    }

    public async Task<IReadOnlyList<string>> GetSearchSuggestionsAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        JsonElement response = await _client.SearchSuggestionsAsync(input, cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseSearchSuggestions(response).ToList();
    }

    public async Task<BrowsePage> GetBrowsePageAsync(
        string browseId,
        string? parameters = null,
        CancellationToken cancellationToken = default)
    {
        JsonElement response = await _client.BrowseAsync(browseId, parameters, cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseBrowsePage(response);
    }

    public async Task<IReadOnlyList<Song>> GetWatchQueueAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        JsonElement response = await _client.NextAsync(videoId, cancellationToken)
            .ConfigureAwait(false);

        return InnertubeParser.ParseWatchQueue(response);
    }

    public async Task<IReadOnlyList<LyricLine>?> GetLyricsAsync(
        string title,
        string artist,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        return await _lrcClient.GetLyricsAsync(title, artist, durationMs, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetStreamUrlAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        var resolved = await GetResolvedStreamAsync(videoId, cancellationToken).ConfigureAwait(false);
        return resolved?.Url;
    }

    public async Task<ResolvedStream?> GetResolvedStreamAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        InnertubePlayerClient[] clients =
        [
            InnertubePlayerClient.AndroidMusic,
            InnertubePlayerClient.TvHtml5,
            InnertubePlayerClient.AndroidVr,
            InnertubePlayerClient.Ios,
            InnertubePlayerClient.IosRecent
        ];

        foreach (var client in clients)
        {
            try
            {
                JsonElement response = await _client.PlayerAsync(videoId, client, cancellationToken)
                    .ConfigureAwait(false);

                var candidates = ExtractAudioFormats(response);
                foreach (var candidate in candidates)
                {
                    string url = PatchClientVersion(candidate.Url, client.ClientVersion);
                    var headers = client.GetMediaHeaders();

                    bool isValid = await ProbeStreamAsync(url, headers, cancellationToken).ConfigureAwait(false);
                    if (isValid)
                    {
                        AppLogger.Info($"Resolved valid stream for {videoId} via {client.ClientName} ({candidate.MimeType} @ {candidate.Bitrate / 1000}kbps)");
                        return new ResolvedStream(
                            Url: url,
                            MediaHeaders: headers,
                            Bitrate: candidate.Bitrate,
                            MimeType: candidate.MimeType,
                            ClientName: client.ClientName
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Client {client.ClientName} failed for {videoId}: {ex.Message}");
                // Try next player client fallback
            }
        }

        return null;
    }

    private static async Task<bool> ProbeStreamAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            foreach (var (k, v) in headers)
            {
                req.Headers.TryAddWithoutValidation(k, v);
            }
            req.Headers.TryAddWithoutValidation("Range", "bytes=0-16383");

            using var resp = await ProbeClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 206)
            {
                return false;
            }

            string? contentType = resp.Content.Headers.ContentType?.MediaType;
            if (contentType is not null && !contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string PatchClientVersion(string url, string clientVersion)
    {
        if (url.Contains("cver="))
        {
            return Regex.Replace(url, @"cver=[^&]+", $"cver={clientVersion}");
        }
        return url;
    }

    private static List<(string Url, int Bitrate, string MimeType)> ExtractAudioFormats(JsonElement root)
    {
        List<(string Url, int Bitrate, string MimeType)> audioCandidates = new();

        if (!root.TryGetProperty("streamingData", out JsonElement streamingData))
        {
            return audioCandidates;
        }

        void ScanFormats(string propertyName)
        {
            if (!streamingData.TryGetProperty(propertyName, out JsonElement formats) ||
                formats.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement format in formats.EnumerateArray())
            {
                string? mimeType = format.GetString("mimeType");
                if (mimeType is null || !mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string? url = format.GetString("url");
                if (!string.IsNullOrEmpty(url))
                {
                    int bitrate = 0;
                    if (format.TryGetProperty("bitrate", out JsonElement br) && br.TryGetInt32(out int bVal))
                    {
                        bitrate = bVal;
                    }
                    audioCandidates.Add((url, bitrate, mimeType));
                }
            }
        }

        ScanFormats("adaptiveFormats");
        ScanFormats("formats");

        return audioCandidates.OrderByDescending(c => c.Bitrate).ToList();
    }
}
