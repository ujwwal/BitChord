namespace BitChord.Core;

public sealed record Song(
    string VideoId,
    string Title,
    string Artist,
    string? ThumbnailUrl,
    string? DurationText = null,
    string? ArtistId = null,
    string? AlbumId = null,
    string? AlbumName = null,
    bool IsVideo = false,
    string? SetVideoId = null,
    bool FromAutoplay = false,
    string? LocalUri = null,
    string? LocalPath = null,
    string? SourceQuality = null
);

public sealed record Account(
    string Name,
    string Email,
    string? ThumbnailUrl
);

public sealed record ShelfItem(
    string Title,
    string Subtitle,
    string? ThumbnailUrl,
    string? VideoId,
    string? BrowseId
);

public sealed record HomeShelf(
    string Title,
    IReadOnlyList<ShelfItem> Items,
    string Subtitle = ""
);

public sealed record HomeFeed(
    IReadOnlyList<HomeShelf> Shelves,
    string? Continuation
);

public enum BrowseType
{
    Album,
    Artist,
    Playlist,
    Other
}

public sealed record BrowseItem(
    string BrowseId,
    string Title,
    string Subtitle,
    string? ThumbnailUrl,
    BrowseType Type
);

public abstract record SearchResult
{
    private SearchResult()
    {
    }

    public sealed record Track(Song Song) : SearchResult;

    public sealed record Browse(BrowseItem Item) : SearchResult;
}

public enum SearchFilter
{
    Songs,
    Albums,
    Artists,
    Playlists
}

public static class SearchFilterExtensions
{
    public static string GetLabel(this SearchFilter filter) => filter switch
    {
        SearchFilter.Songs => "Songs",
        SearchFilter.Albums => "Albums",
        SearchFilter.Artists => "Artists",
        SearchFilter.Playlists => "Playlists",
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    public static string GetParameters(this SearchFilter filter) => filter switch
    {
        SearchFilter.Songs => "EgWKAQIIAWoKEAkQChAFEAMQBA==",
        SearchFilter.Albums => "EgWKAQIYAWoKEAkQChAFEAMQBA==",
        SearchFilter.Artists => "EgWKAQIgAWoKEAkQChAFEAMQBA==",
        SearchFilter.Playlists => "EgWKAQIoAWoKEAkQChAFEAMQBA==",
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };
}

public sealed record BrowseHeader(
    string Title,
    string Subtitle,
    string? ThumbnailUrl
);

public sealed record BrowsePage(
    IReadOnlyList<Song> Songs,
    string? Continuation,
    BrowseHeader? Header = null,
    string? Description = null,
    IReadOnlyList<HomeShelf>? Sections = null,
    IReadOnlyList<Song>? SuggestedSongs = null
)
{
    public IReadOnlyList<HomeShelf> BrowseSections { get; } = Sections ?? [];

    public IReadOnlyList<Song> Suggestions { get; } = SuggestedSongs ?? [];
}

public sealed record ResolvedStream(
    string Url,
    IReadOnlyDictionary<string, string> MediaHeaders,
    long Bitrate,
    string MimeType,
    string ClientName
);
