using System.Text.Json;

namespace BitChord.Core;

public sealed class BitChordService
{
    private readonly AnonymousInnertubeClient _client;

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

    /// <summary>
    /// Resolves an unciphered audio stream URL for a given video ID by querying
    /// AndroidMusic, AndroidVr, or TvHtml5 player endpoints.
    /// </summary>
    public async Task<string?> GetStreamUrlAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        InnertubePlayerClient[] clients =
        [
            InnertubePlayerClient.AndroidMusic,
            InnertubePlayerClient.AndroidVr,
            InnertubePlayerClient.TvHtml5
        ];

        foreach (var client in clients)
        {
            try
            {
                JsonElement response = await _client.PlayerAsync(videoId, client, cancellationToken)
                    .ConfigureAwait(false);

                string? url = ExtractDirectAudioUrl(response);
                if (!string.IsNullOrEmpty(url))
                {
                    return url;
                }
            }
            catch
            {
                // Try next player client fallback
            }
        }

        return null;
    }

    private static string? ExtractDirectAudioUrl(JsonElement root)
    {
        if (!root.TryGetProperty("streamingData", out JsonElement streamingData))
        {
            return null;
        }

        List<(string Url, int Bitrate)> audioCandidates = new();

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
                    audioCandidates.Add((url, bitrate));
                }
            }
        }

        ScanFormats("adaptiveFormats");
        ScanFormats("formats");

        if (audioCandidates.Count > 0)
        {
            // Pick highest bitrate audio stream
            return audioCandidates.OrderByDescending(c => c.Bitrate).First().Url;
        }

        return null;
    }
}
