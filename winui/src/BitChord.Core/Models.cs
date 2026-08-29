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
