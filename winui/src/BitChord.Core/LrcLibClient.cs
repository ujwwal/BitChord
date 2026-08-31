using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BitChord.Core;

public sealed record LyricWord(long StartMs, long EndMs, string Text);

public sealed record LyricLine(
    long TimeMs,
    string Text,
    IReadOnlyList<LyricWord>? Words = null,
    long? SungUntilMs = null,
    LyricLine? Background = null)
{
    public bool IsGap => string.IsNullOrWhiteSpace(Text);
    public bool IsWordSynced => Words is { Count: > 0 };
    public long EndMs => Words?.LastOrDefault()?.EndMs ?? SungUntilMs ?? (TimeMs + 4000);
}

public sealed partial class LrcLibClient
{
    private const string BaseUrl = "https://lrclib.net/api";
    private const string UserAgent = "BitChord (https://github.com/bitchord)";
    private const long MinGapMs = 2000;

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        return client;
    }

    public async Task<IReadOnlyList<LyricLine>?> GetLyricsAsync(
        string title,
        string artist,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        string cleanTitle = CleanQuery(title);
        string cleanArtist = CleanQuery(artist);
        int seconds = (int)(durationMs / 1000);

        try
        {
            // 1. Try exact match
            string? lrc = await FetchExactAsync(cleanTitle, cleanArtist, seconds, cancellationToken).ConfigureAwait(false);

            // 2. Try fuzzy search fallback
            if (string.IsNullOrWhiteSpace(lrc))
            {
                lrc = await FetchSearchAsync(cleanTitle, cleanArtist, seconds, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(lrc))
            {
                var parsed = ParseLrc(lrc);
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"LRCLIB fetch error for '{title}' by '{artist}': {ex.Message}");
        }

        return null;
    }

    private async Task<string?> FetchExactAsync(string title, string artist, int seconds, CancellationToken token)
    {
        string url = $"{BaseUrl}/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}&duration={seconds}";
        using var resp = await HttpClient.GetAsync(url, token).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        var dto = await resp.Content.ReadFromJsonAsync<LrcLibResponseDto>(cancellationToken: token).ConfigureAwait(false);
        return dto?.SyncedLyrics ?? dto?.PlainLyrics;
    }

    private async Task<string?> FetchSearchAsync(string title, string artist, int seconds, CancellationToken token)
    {
        string url = $"{BaseUrl}/search?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
        using var resp = await HttpClient.GetAsync(url, token).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return null;

        var items = await resp.Content.ReadFromJsonAsync<List<LrcLibResponseDto>>(cancellationToken: token).ConfigureAwait(false);
        if (items is null || items.Count == 0) return null;

        var best = items
            .Where(i => !string.IsNullOrWhiteSpace(i.SyncedLyrics))
            .MinBy(i => Math.Abs(i.Duration - seconds));

        return best?.SyncedLyrics ?? items.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.PlainLyrics))?.PlainLyrics;
    }

    public static IReadOnlyList<LyricLine> ParseLrc(string lrc)
    {
        var lines = lrc.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<LyricLine>();

        foreach (var rawLine in lines)
        {
            var match = StampRegex().Match(rawLine);
            if (!match.Success) continue;

            int minutes = int.Parse(match.Groups[1].Value);
            int seconds = int.Parse(match.Groups[2].Value);
            string fracStr = match.Groups[3].Value;
            long fracMs = fracStr.Length == 2 ? long.Parse(fracStr) * 10 : long.Parse(fracStr);

            long timeMs = (minutes * 60L + seconds) * 1000L + fracMs;
            string body = rawLine[(match.Index + match.Length)..];
            string cleanText = WordStampRegex().Replace(body, "").Trim();

            var words = ParseWordRuns(body);
            parsed.Add(new LyricLine(timeMs, cleanText, words.Count > 0 ? words : null));
        }

        parsed.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        var kept = new List<LyricLine>();
        for (int i = 0; i < parsed.Count; i++)
        {
            var line = parsed[i];
            if (!line.IsGap)
            {
                kept.Add(line);
                continue;
            }

            var next = i + 1 < parsed.Count ? parsed[i + 1] : null;
            if (next is null || next.TimeMs - line.TimeMs >= MinGapMs)
            {
                kept.Add(line);
            }
        }

        if (kept.Count > 0 && !kept[0].IsGap && kept[0].TimeMs >= MinGapMs)
        {
            kept.Insert(0, new LyricLine(0, ""));
        }

        return kept;
    }

    private static IReadOnlyList<LyricWord> ParseWordRuns(string body)
    {
        var matches = WordStampRegex().Matches(body);
        if (matches.Count == 0) return Array.Empty<LyricWord>();

        var runs = new List<(long StartMs, string Text)>();
        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            int nextStart = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            int wordStart = match.Index + match.Length;
            string wordText = wordStart < nextStart ? body[wordStart..nextStart] : "";

            int minutes = int.Parse(match.Groups[1].Value);
            int seconds = int.Parse(match.Groups[2].Value);
            string fracStr = match.Groups[3].Value;
            long fracMs = fracStr.Length == 2 ? long.Parse(fracStr) * 10 : long.Parse(fracStr);
            long startMs = (minutes * 60L + seconds) * 1000L + fracMs;

            runs.Add((startMs, wordText));
        }

        var result = new List<LyricWord>();
        for (int i = 0; i < runs.Count; i++)
        {
            string text = runs[i].Text.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            long start = runs[i].StartMs;
            long end = i + 1 < runs.Count ? runs[i + 1].StartMs : start + 1000;
            result.Add(new LyricWord(start, Math.Max(start, end), text));
        }

        return result;
    }

    private static string CleanQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return query;
        string cleaned = NoiseRegex().Replace(query, " ");
        int pipeIdx = cleaned.IndexOf('|');
        if (pipeIdx > 0) cleaned = cleaned[..pipeIdx];
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    [GeneratedRegex(@"\[(\d{1,2}):(\d{2})[.:](\d{2,3})\]")]
    private static partial Regex StampRegex();

    [GeneratedRegex(@"<(\d{1,3}):(\d{2})[.:](\d{2,3})>")]
    private static partial Regex WordStampRegex();

    [GeneratedRegex(@"\((?:from|feat\.?|official|lyrical|video|audio|remix)[^)]*\)|\[[^\]]*\]|\b(?:official (?:video|audio|music video)|lyrical|full song|4k video)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NoiseRegex();

    private sealed class LrcLibResponseDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("plainLyrics")]
        public string? PlainLyrics { get; set; }

        [JsonPropertyName("syncedLyrics")]
        public string? SyncedLyrics { get; set; }
    }
}
