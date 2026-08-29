using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BitChord.Core;

internal static partial class InnertubeParser
{
    private static readonly HashSet<string> TypeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "song", "video", "album", "single", "ep", "artist",
        "playlist", "podcast", "episode"
    };

    private static readonly HashSet<string> ReleaseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "album", "single", "ep"
    };

    private static readonly string[] HeaderRenderers =
    [
        "musicResponsiveHeaderRenderer",
        "musicDetailHeaderRenderer"
    ];

    public static IReadOnlyList<SearchResult> ParseSearch(JsonElement root)
    {
        List<SearchResult> results = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach ((string kind, JsonElement renderer) in EnumerateSearchRenderers(root))
        {
            BrowseItem? browse = kind == "musicResponsiveListItemRenderer"
                ? ParseResponsiveBrowseItem(renderer)
                : ParseCardBrowseItem(renderer);
            if (browse is not null)
            {
                if (seen.Add("b:" + browse.BrowseId))
                {
                    results.Add(new SearchResult.Browse(browse));
                }
                continue;
            }

            Song? song = kind == "musicResponsiveListItemRenderer"
                ? ParseResponsiveSong(renderer, default)
                : ParseCardSong(renderer);
            if (song is not null && !song.IsVideo && seen.Add("v:" + song.VideoId))
            {
                results.Add(new SearchResult.Track(song));
            }
        }

        return results;
    }

    public static IReadOnlyList<string> ParseSearchSuggestions(JsonElement root)
    {
        List<string> suggestions = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement renderer in FindRenderers(root, "searchSuggestionRenderer"))
        {
            string? query = StringAt(renderer, "navigationEndpoint", "searchEndpoint", "query");
            query ??= Text(Property(renderer, "suggestion"));
            if (!string.IsNullOrWhiteSpace(query) && seen.Add(query))
            {
                suggestions.Add(query);
            }
        }
        return suggestions;
    }

    public static IReadOnlyList<HomeShelf> ParseHome(JsonElement root)
    {
        List<HomeShelf> shelves = [];
        WalkShelves(root, shelves);
        return shelves;
    }

    public static BrowsePage ParseBrowsePage(JsonElement root)
    {
        BrowseHeader? header = ParseBrowseHeader(root);
        string? description = ParseDescription(root);
        string? continuation = ContinuationToken(root);
        IReadOnlyList<HomeShelf> sections = ParseHome(root);

        List<Song> songs = [];
        List<Song> suggested = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        Credits fallback = PageCredits(root);

        List<JsonElement> playlistScopes = FindRenderers(root, "musicPlaylistShelfRenderer")
            .Concat(FindRenderers(root, "musicPlaylistShelfContinuation"))
            .ToList();

        if (playlistScopes.Count > 0)
        {
            foreach (JsonElement scope in playlistScopes)
            {
                AddSongsDeep(scope, fallback, songs, seen);
            }

            foreach (JsonElement shelf in FindRenderers(root, "musicShelfRenderer"))
            {
                if (!string.Equals(Text(Property(shelf, "title")), "Suggestions", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                HashSet<string> suggestionIds = new(StringComparer.Ordinal);
                AddSongsDeep(shelf, fallback, suggested, suggestionIds);
            }
        }
        else
        {
            AddSongsDeep(root, fallback, songs, seen);
        }

        return new BrowsePage(
            songs,
            continuation,
            header,
            description,
            sections,
            suggested);
    }

    public static IReadOnlyList<Song> ParseWatchQueue(JsonElement root)
    {
        List<Song> songs = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (JsonElement renderer in FindRenderers(root, "playlistPanelVideoRenderer"))
        {
            string? videoId = StringAt(renderer, "videoId");
            string title = Text(Property(renderer, "title"));
            if (string.IsNullOrWhiteSpace(videoId) || string.IsNullOrWhiteSpace(title) || !seen.Add(videoId))
            {
                continue;
            }

            JsonElement? bylineElement = Property(renderer, "longBylineText");
            List<JsonElement> runs = Runs(bylineElement).ToList();
            string artist = string.Concat(
                runs.Select(StringText).TakeWhile(text => !text.Contains('•'))).Trim();
            Credits credits = CreditsOf(runs);
            bool isVideo = runs.Any(run =>
                StringText(run).Contains("views", StringComparison.OrdinalIgnoreCase));

            songs.Add(new Song(
                videoId,
                title,
                string.IsNullOrWhiteSpace(artist) ? credits.ArtistName ?? "Unknown artist" : artist,
                BestThumbnail(renderer),
                NullIfBlank(Text(Property(renderer, "lengthText"))),
                credits.ArtistId,
                credits.AlbumId,
                credits.AlbumName,
                isVideo));
        }

        return songs;
    }

    public static string? ContinuationToken(JsonElement root)
    {
        foreach (JsonElement renderer in FindRenderers(root, "continuationItemRenderer"))
        {
            string? token = StringAt(
                renderer,
                "continuationEndpoint",
                "continuationCommand",
                "token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        foreach (JsonElement continuation in FindRenderers(root, "nextContinuationData"))
        {
            string? token = StringAt(continuation, "continuation");
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }
        }

        return null;
    }

    private static IEnumerable<(string Kind, JsonElement Renderer)> EnumerateSearchRenderers(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if ((property.NameEquals("musicResponsiveListItemRenderer") ||
                     property.NameEquals("musicCardShelfRenderer")) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return (property.Name, property.Value);
                }

                foreach ((string kind, JsonElement renderer) in EnumerateSearchRenderers(property.Value))
                {
                    yield return (kind, renderer);
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                foreach ((string kind, JsonElement renderer) in EnumerateSearchRenderers(item))
                {
                    yield return (kind, renderer);
                }
            }
        }
    }

    private static void WalkShelves(JsonElement node, List<HomeShelf> shelves)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in node.EnumerateObject())
            {
                HomeShelf? shelf = property.Name switch
                {
                    "musicCarouselShelfRenderer" => ParseCarouselShelf(property.Value),
                    "musicImmersiveCarouselShelfRenderer" => ParseCarouselShelf(property.Value),
                    "musicShelfRenderer" => ParsePlainShelf(property.Value),
                    _ => null
                };
                if (shelf is not null)
                {
                    shelves.Add(shelf);
                }

                WalkShelves(property.Value, shelves);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                WalkShelves(item, shelves);
            }
        }
    }

    private static HomeShelf? ParseCarouselShelf(JsonElement renderer)
    {
        JsonElement? header = Property(renderer, "header");
        JsonElement? basicHeader = header is null
            ? null
            : Property(header.Value, "musicCarouselShelfBasicHeaderRenderer");
        JsonElement? immersiveHeader = header is null
            ? null
            : Property(header.Value, "musicImmersiveCarouselShelfBasicHeaderRenderer");
        JsonElement? selectedHeader = basicHeader ?? immersiveHeader;
        string title = Text(selectedHeader is null ? null : Property(selectedHeader.Value, "title"));
        string subtitle = Text(selectedHeader is null ? null : Property(selectedHeader.Value, "strapline"));
        if (ContainsVideoWord(title))
        {
            return null;
        }

        List<ShelfItem> items = [];
        JsonElement? contents = Property(renderer, "contents");
        if (contents is { ValueKind: JsonValueKind.Array })
        {
            foreach (JsonElement content in contents.Value.EnumerateArray())
            {
                ShelfItem? item = ParseShelfItem(content);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }

        return items.Count == 0
            ? null
            : new HomeShelf(string.IsNullOrWhiteSpace(title) ? "For you" : title, items, subtitle);
    }

    private static HomeShelf? ParsePlainShelf(JsonElement renderer)
    {
        string title = Text(Property(renderer, "title"));
        if (ContainsVideoWord(title))
        {
            return null;
        }

        List<ShelfItem> items = [];
        JsonElement? contents = Property(renderer, "contents");
        if (contents is { ValueKind: JsonValueKind.Array })
        {
            foreach (JsonElement content in contents.Value.EnumerateArray())
            {
                ShelfItem? item = ParseShelfItem(content);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }

        return items.Count == 0
            ? null
            : new HomeShelf(string.IsNullOrWhiteSpace(title) ? "For you" : title, items);
    }

    private static ShelfItem? ParseShelfItem(JsonElement container)
    {
        JsonElement? twoRow = Property(container, "musicTwoRowItemRenderer");
        if (twoRow is not null)
        {
            return ParseTwoRowItem(twoRow.Value);
        }

        JsonElement? responsive = Property(container, "musicResponsiveListItemRenderer");
        if (responsive is not null)
        {
            BrowseItem? browse = ParseResponsiveBrowseItem(responsive.Value);
            if (browse is not null)
            {
                return new ShelfItem(
                    browse.Title,
                    browse.Subtitle,
                    browse.ThumbnailUrl,
                    null,
                    browse.BrowseId);
            }

            Song? song = ParseResponsiveSong(responsive.Value, default);
            if (song is not null && !song.IsVideo)
            {
                return new ShelfItem(song.Title, song.Artist, song.ThumbnailUrl, song.VideoId, null);
            }
        }

        JsonElement? navigation = Property(container, "musicNavigationButtonRenderer");
        if (navigation is not null)
        {
            return ParseNavigationButton(navigation.Value);
        }

        JsonElement? card = Property(container, "musicCardShelfRenderer");
        if (card is not null)
        {
            BrowseItem? browse = ParseCardBrowseItem(card.Value);
            if (browse is not null)
            {
                return new ShelfItem(
                    browse.Title,
                    browse.Subtitle,
                    browse.ThumbnailUrl,
                    null,
                    browse.BrowseId);
            }

            Song? song = ParseCardSong(card.Value);
            if (song is not null && !song.IsVideo)
            {
                return new ShelfItem(song.Title, song.Artist, song.ThumbnailUrl, song.VideoId, null);
            }
        }

        return null;
    }

    private static ShelfItem? ParseTwoRowItem(JsonElement renderer)
    {
        string title = Text(Property(renderer, "title"));
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        JsonElement? endpoint = Property(renderer, "navigationEndpoint");
        string? browseId = endpoint is null ? null : StringAt(endpoint.Value, "browseEndpoint", "browseId");
        string? videoId = endpoint is null ? null : StringAt(endpoint.Value, "watchEndpoint", "videoId");
        if (videoId is null && browseId?.StartsWith("MPED", StringComparison.Ordinal) == true)
        {
            videoId = browseId[4..];
            browseId = null;
        }

        string subtitle = Text(Property(renderer, "subtitle"));
        if (browseId is not null && (ContainsVideoWord(title) || ContainsVideoWord(subtitle)))
        {
            return null;
        }
        if (browseId is null && videoId is not null && ThumbnailIsWidescreen(renderer))
        {
            return null;
        }
        if (browseId is null && videoId is null)
        {
            return null;
        }

        return new ShelfItem(title, subtitle, BestThumbnail(renderer), videoId, browseId);
    }

    private static ShelfItem? ParseNavigationButton(JsonElement renderer)
    {
        string title = Text(Property(renderer, "buttonText"));
        string? browseId = StringAt(renderer, "clickCommand", "browseEndpoint", "browseId")
            ?? StringAt(renderer, "navigationEndpoint", "browseEndpoint", "browseId");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(browseId) || ContainsVideoWord(title))
        {
            return null;
        }
        return new ShelfItem(title, string.Empty, BestThumbnail(renderer), null, browseId);
    }

    private static BrowseItem? ParseResponsiveBrowseItem(JsonElement renderer)
    {
        JsonElement? endpoint = DirectBrowseEndpoint(renderer);
        if (endpoint is null)
        {
            return null;
        }

        string? browseId = StringAt(endpoint.Value, "browseId");
        string title = ResponsiveColumnText(renderer, 0);
        string subtitle = ResponsiveColumnText(renderer, 1);
        if (string.IsNullOrWhiteSpace(browseId) || string.IsNullOrWhiteSpace(title) ||
            ContainsVideoWord(title) || ContainsVideoWord(subtitle))
        {
            return null;
        }

        return new BrowseItem(
            browseId,
            title,
            subtitle,
            BestThumbnail(renderer),
            BrowseTypeOf(endpoint.Value, browseId));
    }

    private static BrowseItem? ParseCardBrowseItem(JsonElement renderer)
    {
        string title = Text(Property(renderer, "title"));
        string subtitle = Text(Property(renderer, "subtitle"));
        JsonElement? endpoint = BrowseEndpointFromText(Property(renderer, "title"))
            ?? (Property(renderer, "navigationEndpoint") is { } navigation
                ? Property(navigation, "browseEndpoint")
                : null);
        if (endpoint is null)
        {
            endpoint = FindRenderers(renderer, "browseEndpoint").FirstOrDefaultOrNull();
        }

        string? browseId = endpoint is null ? null : StringAt(endpoint.Value, "browseId");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(browseId) ||
            ContainsVideoWord(title) || ContainsVideoWord(subtitle))
        {
            return null;
        }

        return new BrowseItem(
            browseId,
            title,
            subtitle,
            BestThumbnail(renderer),
            BrowseTypeOf(endpoint.Value, browseId));
    }

    private static Song? ParseResponsiveSong(JsonElement renderer, Credits fallback)
    {
        string? videoId = StringAt(renderer, "playlistItemData", "videoId")
            ?? StringAt(
                renderer,
                "overlay",
                "musicItemThumbnailOverlayRenderer",
                "content",
                "musicPlayButtonRenderer",
                "playNavigationEndpoint",
                "watchEndpoint",
                "videoId");
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        string title = ResponsiveColumnText(renderer, 0);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        string subtitle = ResponsiveColumnText(renderer, 1);
        string[] parts = SplitSubtitle(subtitle);
        string? duration = parts.LastOrDefault(part => DurationRegex().IsMatch(part));
        string? rowType = parts.FirstOrDefault(part => TypeWords.Contains(part));
        string? inferredArtist = parts.FirstOrDefault(part =>
            !DurationRegex().IsMatch(part) &&
            !TypeWords.Contains(part) &&
            !TallyRegex().IsMatch(part));

        Credits credits = CreditsOf(AllRuns(Property(renderer, "flexColumns")));
        bool isVideo = string.Equals(rowType, "video", StringComparison.OrdinalIgnoreCase) ||
            ThumbnailIsWidescreen(renderer);

        return new Song(
            videoId,
            title,
            NullIfBlank(credits.ArtistName) ?? inferredArtist ?? fallback.ArtistName ?? "Unknown artist",
            BestThumbnail(renderer),
            duration,
            credits.ArtistId ?? fallback.ArtistId,
            credits.AlbumId ?? fallback.AlbumId,
            credits.AlbumName ?? fallback.AlbumName,
            isVideo,
            StringAt(renderer, "playlistItemData", "playlistSetVideoId"));
    }

    private static Song? ParseCardSong(JsonElement renderer)
    {
        string title = Text(Property(renderer, "title"));
        JsonElement? watch = WatchEndpointFromText(Property(renderer, "title"))
            ?? FindRenderers(renderer, "watchEndpoint").FirstOrDefaultOrNull();
        string? videoId = watch is null ? null : StringAt(watch.Value, "videoId");
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(videoId))
        {
            return null;
        }

        string subtitle = Text(Property(renderer, "subtitle"));
        Credits credits = CreditsOf(AllRuns(renderer));
        string? artist = NullIfBlank(credits.ArtistName) ?? InferArtist(subtitle);
        return new Song(
            videoId,
            title,
            artist ?? "Unknown artist",
            BestThumbnail(renderer),
            SplitSubtitle(subtitle).LastOrDefault(part => DurationRegex().IsMatch(part)),
            credits.ArtistId,
            credits.AlbumId,
            credits.AlbumName,
            ThumbnailIsWidescreen(renderer));
    }

    private static BrowseHeader? ParseBrowseHeader(JsonElement root)
    {
        JsonElement? header = null;
        foreach (string rendererName in HeaderRenderers)
        {
            header = FindRenderers(root, rendererName).FirstOrDefaultOrNull();
            if (header is not null)
            {
                break;
            }
        }

        if (header is null)
        {
            JsonElement? immersive = FindRenderers(root, "musicImmersiveHeaderRenderer").FirstOrDefaultOrNull();
            if (immersive is not null)
            {
                header = immersive;
            }
        }
        if (header is null)
        {
            return null;
        }

        string title = Text(Property(header.Value, "title"));
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }
        string subtitle = Text(Property(header.Value, "straplineTextOne"));
        if (string.IsNullOrWhiteSpace(subtitle))
        {
            subtitle = Text(Property(header.Value, "subtitle"));
        }

        return new BrowseHeader(title, subtitle, BestThumbnail(header.Value));
    }

    private static string? ParseDescription(JsonElement root)
    {
        foreach (JsonElement renderer in FindRenderers(root, "musicDescriptionShelfRenderer"))
        {
            string text = Text(Property(renderer, "description"));
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        foreach (string rendererName in HeaderRenderers.Append("musicImmersiveHeaderRenderer"))
        {
            foreach (JsonElement renderer in FindRenderers(root, rendererName))
            {
                string text = Text(Property(renderer, "description"));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        return null;
    }

    private static Credits PageCredits(JsonElement root)
    {
        JsonElement? header = null;
        foreach (string rendererName in HeaderRenderers)
        {
            header = FindRenderers(root, rendererName).FirstOrDefaultOrNull();
            if (header is not null)
            {
                break;
            }
        }
        if (header is null)
        {
            return default;
        }

        List<JsonElement> lines = [];
        JsonElement? strapline = Property(header.Value, "straplineTextOne");
        JsonElement? subtitle = Property(header.Value, "subtitle");
        if (strapline is not null)
        {
            lines.AddRange(Runs(strapline));
        }
        if (subtitle is not null)
        {
            lines.AddRange(Runs(subtitle));
        }

        string[] parts = lines
            .Select(StringText)
            .Aggregate(string.Empty, (current, text) => current + text)
            .Split(" • ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!parts.Any(part => ReleaseWords.Contains(part)))
        {
            return default;
        }

        Credits credits = CreditsOf(lines);
        if (!string.IsNullOrWhiteSpace(credits.ArtistName))
        {
            return credits;
        }

        string? artist = parts.FirstOrDefault(part =>
            !TypeWords.Contains(part) &&
            !TallyRegex().IsMatch(part) &&
            !YearRegex().IsMatch(part) &&
            !DurationRegex().IsMatch(part));
        return credits with { ArtistName = artist };
    }

    private static void AddSongsDeep(
        JsonElement root,
        Credits fallback,
        List<Song> songs,
        HashSet<string> seen)
    {
        foreach (JsonElement renderer in FindRenderers(root, "musicResponsiveListItemRenderer"))
        {
            Song? song = ParseResponsiveSong(renderer, fallback);
            if (song is not null && !song.IsVideo && seen.Add(song.VideoId))
            {
                songs.Add(song);
            }
        }
    }

    private static Credits CreditsOf(IEnumerable<JsonElement> runs)
    {
        Credits credits = default;
        foreach (JsonElement run in runs)
        {
            JsonElement? navigation = Property(run, "navigationEndpoint");
            JsonElement? browse = navigation is null ? null : Property(navigation.Value, "browseEndpoint");
            string? browseId = browse is null ? null : StringAt(browse.Value, "browseId");
            if (browseId is null || browse is null)
            {
                continue;
            }

            BrowseType type = BrowseTypeOf(browse.Value, browseId);
            string? name = StringAt(run, "text");
            if (type == BrowseType.Artist && credits.ArtistId is null)
            {
                credits = credits with { ArtistId = browseId, ArtistName = name };
            }
            else if (type == BrowseType.Album && credits.AlbumId is null)
            {
                credits = credits with { AlbumId = browseId, AlbumName = name };
            }
        }
        return credits;
    }

    private static BrowseType BrowseTypeOf(JsonElement browseEndpoint, string browseId)
    {
        string pageType = StringAt(
            browseEndpoint,
            "browseEndpointContextSupportedConfigs",
            "browseEndpointContextMusicConfig",
            "pageType") ?? string.Empty;
        if (pageType.Contains("ALBUM", StringComparison.OrdinalIgnoreCase) ||
            browseId.StartsWith("MPRE", StringComparison.Ordinal))
        {
            return BrowseType.Album;
        }
        if (pageType.Contains("ARTIST", StringComparison.OrdinalIgnoreCase) ||
            browseId.StartsWith("UC", StringComparison.Ordinal) ||
            browseId.StartsWith("MPLA", StringComparison.Ordinal))
        {
            return BrowseType.Artist;
        }
        if (pageType.Contains("PLAYLIST", StringComparison.OrdinalIgnoreCase) ||
            browseId.StartsWith("VL", StringComparison.Ordinal))
        {
            return BrowseType.Playlist;
        }
        return BrowseType.Other;
    }

    private static JsonElement? DirectBrowseEndpoint(JsonElement renderer)
    {
        JsonElement? navigation = Property(renderer, "navigationEndpoint");
        JsonElement? endpoint = navigation is null ? null : Property(navigation.Value, "browseEndpoint");
        if (endpoint is not null)
        {
            return endpoint;
        }

        JsonElement? flexColumns = Property(renderer, "flexColumns");
        if (flexColumns is { ValueKind: JsonValueKind.Array })
        {
            foreach (JsonElement column in flexColumns.Value.EnumerateArray())
            {
                JsonElement? columnRenderer = Property(column, "musicResponsiveListItemFlexColumnRenderer");
                JsonElement? text = columnRenderer is null ? null : Property(columnRenderer.Value, "text");
                endpoint = BrowseEndpointFromText(text);
                if (endpoint is not null)
                {
                    return endpoint;
                }
            }
        }
        return null;
    }

    private static JsonElement? BrowseEndpointFromText(JsonElement? text)
    {
        foreach (JsonElement run in Runs(text))
        {
            JsonElement? navigation = Property(run, "navigationEndpoint");
            JsonElement? browse = navigation is null ? null : Property(navigation.Value, "browseEndpoint");
            if (browse is not null)
            {
                return browse;
            }
        }
        return null;
    }

    private static JsonElement? WatchEndpointFromText(JsonElement? text)
    {
        foreach (JsonElement run in Runs(text))
        {
            JsonElement? navigation = Property(run, "navigationEndpoint");
            JsonElement? watch = navigation is null ? null : Property(navigation.Value, "watchEndpoint");
            if (watch is not null)
            {
                return watch;
            }
        }
        return null;
    }

    private static string ResponsiveColumnText(JsonElement renderer, int index)
    {
        JsonElement? columns = Property(renderer, "flexColumns");
        if (columns is not { ValueKind: JsonValueKind.Array })
        {
            return string.Empty;
        }

        JsonElement[] values = columns.Value.EnumerateArray().ToArray();
        if (index < 0 || index >= values.Length)
        {
            return string.Empty;
        }

        JsonElement? column = Property(values[index], "musicResponsiveListItemFlexColumnRenderer");
        return column is null ? string.Empty : Text(Property(column.Value, "text"));
    }

    private static string? InferArtist(string subtitle)
    {
        return SplitSubtitle(subtitle).FirstOrDefault(part =>
            !TypeWords.Contains(part) &&
            !TallyRegex().IsMatch(part) &&
            !DurationRegex().IsMatch(part));
    }

    private static string[] SplitSubtitle(string subtitle) => subtitle.Split(
        " • ",
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool ContainsVideoWord(string text) => VideoWordRegex().IsMatch(text);

    private static bool ThumbnailIsWidescreen(JsonElement root)
    {
        JsonElement? thumbnail = BestThumbnailElement(root);
        if (thumbnail is null)
        {
            return false;
        }

        double? width = NumberAt(thumbnail.Value, "width");
        double? height = NumberAt(thumbnail.Value, "height");
        if (width is null || height is null || width <= 0 || height <= 0)
        {
            return false;
        }
        double ratio = width.Value / height.Value;
        return ratio is < 0.85 or > 1.15;
    }

    private static string? BestThumbnail(JsonElement root)
    {
        JsonElement? thumbnail = BestThumbnailElement(root);
        return thumbnail is null ? null : StringAt(thumbnail.Value, "url");
    }

    private static JsonElement? BestThumbnailElement(JsonElement root)
    {
        JsonElement? best = null;
        void Walk(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in node.EnumerateObject())
                {
                    if (property.NameEquals("thumbnails") && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object && StringAt(item, "url") is not null)
                            {
                                best = item;
                            }
                        }
                    }
                    else
                    {
                        Walk(property.Value);
                    }
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    Walk(item);
                }
            }
        }
        Walk(root);
        return best;
    }

    private static IEnumerable<JsonElement> FindRenderers(JsonElement node, string name)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if (property.NameEquals(name) && property.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return property.Value;
                }
                foreach (JsonElement child in FindRenderers(property.Value, name))
                {
                    yield return child;
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                foreach (JsonElement child in FindRenderers(item, name))
                {
                    yield return child;
                }
            }
        }
    }

    private static IEnumerable<JsonElement> AllRuns(JsonElement? root)
    {
        if (root is null)
        {
            yield break;
        }

        JsonElement node = root.Value;
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if (property.NameEquals("runs") && property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement run in property.Value.EnumerateArray())
                    {
                        if (run.ValueKind == JsonValueKind.Object)
                        {
                            yield return run;
                        }
                    }
                }
                else
                {
                    foreach (JsonElement run in AllRuns(property.Value))
                    {
                        yield return run;
                    }
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                foreach (JsonElement run in AllRuns(item))
                {
                    yield return run;
                }
            }
        }
    }

    private static IEnumerable<JsonElement> Runs(JsonElement? text)
    {
        if (text is null || text.Value.ValueKind != JsonValueKind.Object ||
            !text.Value.TryGetProperty("runs", out JsonElement runs) ||
            runs.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement run in runs.EnumerateArray())
        {
            if (run.ValueKind == JsonValueKind.Object)
            {
                yield return run;
            }
        }
    }

    private static string Text(JsonElement? text)
    {
        if (text is null)
        {
            return string.Empty;
        }
        if (text.Value.ValueKind == JsonValueKind.String)
        {
            return text.Value.GetString() ?? string.Empty;
        }
        if (text.Value.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }
        if (text.Value.TryGetProperty("simpleText", out JsonElement simple) &&
            simple.ValueKind == JsonValueKind.String)
        {
            return simple.GetString() ?? string.Empty;
        }
        return string.Concat(Runs(text).Select(StringText));
    }

    private static string StringText(JsonElement element) => StringAt(element, "text") ?? string.Empty;

    private static JsonElement? Property(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value)
            ? value
            : null;
    }

    private static string? StringAt(JsonElement element, params string[] path)
    {
        JsonElement current = element;
        foreach (string name in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(name, out current))
            {
                return null;
            }
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.ToString(),
            _ => null
        };
    }

    private static double? NumberAt(JsonElement element, string name)
    {
        JsonElement? value = Property(element, name);
        if (value is null)
        {
            return null;
        }
        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out double number))
        {
            return number;
        }
        return double.TryParse(value.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private readonly record struct Credits(
        string? ArtistId = null,
        string? ArtistName = null,
        string? AlbumId = null,
        string? AlbumName = null);

    private static JsonElement? FirstOrDefaultOrNull(this IEnumerable<JsonElement> values)
    {
        foreach (JsonElement value in values)
        {
            return value;
        }
        return null;
    }

    [GeneratedRegex(@"\d+:\d{2}")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"\d{4}")]
    private static partial Regex YearRegex();

    [GeneratedRegex(
        @"[\d.,]+\s*[KMB]?\s+(plays|views|likes|songs|tracks|subscribers|hours?|minutes?|seconds?)\b.*",
        RegexOptions.IgnoreCase)]
    private static partial Regex TallyRegex();

    [GeneratedRegex(@"\bvideos?\b", RegexOptions.IgnoreCase)]
    private static partial Regex VideoWordRegex();
}
