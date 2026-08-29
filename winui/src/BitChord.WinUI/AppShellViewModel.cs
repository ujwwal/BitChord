using System.Collections.ObjectModel;
using BitChord.Core;

namespace BitChord.WinUI;

public sealed class AppShellViewModel
{
    public ObservableCollection<FeedSection> HomeSections { get; } = new();
    public ObservableCollection<FeedSection> ExploreSections { get; } = new();
    public ObservableCollection<LibraryTile> LibraryTiles { get; } = new();
    public ObservableCollection<SearchFilterOption> SearchFilters { get; } = new();
    public ObservableCollection<SearchResultTile> SearchResults { get; } = new();
    public ObservableCollection<string> SearchSuggestions { get; } = new();

    public SearchFilter SelectedSearchFilter { get; set; } = SearchFilter.Songs;

    public AppShellViewModel()
    {
        LoadDemoData();
    }

    private void LoadDemoData()
    {
        HomeSections.Add(new FeedSection("Featured",
        [
            new FeedCard("Midnight Drive", "Synthwave", null),
            new FeedCard("Velvet Hours", "Chill", null),
            new FeedCard("Sunset Echoes", "Indie pop", null),
            new FeedCard("Night Bloom", "Ambient", null),
        ]));

        HomeSections.Add(new FeedSection("Made for you",
        [
            new FeedCard("Warm Static", "Electronic", null),
            new FeedCard("Slow Motion", "R&B", null),
            new FeedCard("Quiet Current", "Ambient", null),
            new FeedCard("Blue Hour", "Acoustic", null),
            new FeedCard("Glass Horizon", "Electronic", null),
        ]));

        ExploreSections.Add(new FeedSection("Browse",
        [
            new FeedCard("Trending now", "Top songs", null),
            new FeedCard("New releases", "Fresh picks", null),
            new FeedCard("Moods", "Curated mood boards", null),
            new FeedCard("Discover", "New artists", null),
        ]));

        ExploreSections.Add(new FeedSection("Popular playlists",
        [
            new FeedCard("Late night", "7 songs", null),
            new FeedCard("Focus flow", "12 songs", null),
            new FeedCard("Gym mix", "18 songs", null),
            new FeedCard("Road trip", "16 songs", null),
        ]));

        LibraryTiles.Add(new LibraryTile("Downloads", "Downloaded songs", "Downloads"));
        LibraryTiles.Add(new LibraryTile("Local Music", "Audio files on device", "Local"));
        LibraryTiles.Add(new LibraryTile("Liked Songs", "Your favorite tracks", "Liked"));
        LibraryTiles.Add(new LibraryTile("Playlists", "Custom mixes", "Playlists"));

        SearchFilters.Add(new SearchFilterOption(SearchFilter.Songs, "Songs"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Albums, "Albums"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Artists, "Artists"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Playlists, "Playlists"));

        SearchResults.Add(new SearchResultTile("Midnight Drive", "Track • Synthwave"));
        SearchResults.Add(new SearchResultTile("Sunset Echoes", "Album • Indie pop"));
        SearchResults.Add(new SearchResultTile("Velvet Hours", "Playlist • Chill"));
        SearchResults.Add(new SearchResultTile("Night Bloom", "Artist • Ambient"));

        SearchSuggestions.Add("midnight drive");
        SearchSuggestions.Add("sunset echoes");
        SearchSuggestions.Add("night bloom");
    }
}

public sealed class FeedSection
{
    public FeedSection(string title, IEnumerable<FeedCard> cards)
    {
        Title = title;
        Cards = new ObservableCollection<FeedCard>(cards);
    }

    public string Title { get; }

    public ObservableCollection<FeedCard> Cards { get; }
}

public sealed class FeedCard
{
    public FeedCard(string title, string subtitle, string? thumbnailUrl)
    {
        Title = title;
        Subtitle = subtitle;
        ThumbnailUrl = thumbnailUrl;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string? ThumbnailUrl { get; }
}

public sealed class LibraryTile
{
    public LibraryTile(string title, string subtitle, string iconGlyph)
    {
        Title = title;
        Subtitle = subtitle;
        IconGlyph = iconGlyph;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string IconGlyph { get; }
}

public sealed class SearchResultTile
{
    public SearchResultTile(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
    }

    public string Title { get; }

    public string Subtitle { get; }
}

public sealed class SearchFilterOption
{
    public SearchFilterOption(SearchFilter filter, string label)
    {
        Filter = filter;
        Label = label;
    }

    public SearchFilter Filter { get; }

    public string Label { get; }
}
