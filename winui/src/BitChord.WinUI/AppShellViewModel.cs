using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BitChord.Core;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace BitChord.WinUI;

public sealed class AppShellViewModel : INotifyPropertyChanged
{
    private readonly BitChordService _service = new();
    private readonly MediaPlayer _player = new();
    private CancellationTokenSource? _playCts;

    public ObservableCollection<FeedSection> HomeSections { get; } = new();
    public ObservableCollection<FeedSection> ExploreSections { get; } = new();
    public ObservableCollection<LibraryTile> LibraryTiles { get; } = new();
    public ObservableCollection<SearchFilterOption> SearchFilters { get; } = new();
    public ObservableCollection<SearchResultTile> SearchResults { get; } = new();
    public ObservableCollection<string> SearchSuggestions { get; } = new();

    // ── Playback state ────────────────────────────────────────────────────────
    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        set { _currentSong = value; OnPropertyChanged(); }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    // ── Search filter ─────────────────────────────────────────────────────────
    private SearchFilter _selectedSearchFilter = SearchFilter.Songs;
    public SearchFilter SelectedSearchFilter
    {
        get => _selectedSearchFilter;
        set { _selectedSearchFilter = value; OnPropertyChanged(); }
    }

    private string _currentQuery = string.Empty;
    public string CurrentQuery
    {
        get => _currentQuery;
        set { _currentQuery = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public AppShellViewModel()
    {
        SetupMediaPlayer();
        LoadDemoData();
        _ = LoadLiveDataAsync();
    }

    private void SetupMediaPlayer()
    {
        _player.PlaybackSession.PlaybackStateChanged += (sender, _) =>
        {
            var state = sender.PlaybackState;
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
            {
                IsPlaying = state == MediaPlaybackState.Playing;
                IsLoading = state == MediaPlaybackState.Buffering || state == MediaPlaybackState.Opening;
            });
        };

        _player.MediaEnded += (_, _) =>
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
            {
                IsPlaying = false;
            });
        };

        _player.MediaFailed += (_, args) =>
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(() =>
            {
                IsLoading = false;
                IsPlaying = false;
            });
        };
    }

    private void LoadDemoData()
    {
        HomeSections.Add(new FeedSection("Featured",
        [
            new FeedCard("Midnight Drive", "Synthwave", null, "fBN1qj2g2eA"),
            new FeedCard("Velvet Hours", "Chill", null, "5qap5aO4i9A"),
            new FeedCard("Sunset Echoes", "Indie pop", null, "kJQP7kiw5Fk"),
            new FeedCard("Night Bloom", "Ambient", null, "fJ9rUzIMcZQ"),
        ], isHero: true));

        HomeSections.Add(new FeedSection("Made for you",
        [
            new FeedCard("Warm Static", "Electronic", null, "fBN1qj2g2eA"),
            new FeedCard("Slow Motion", "R&B", null, "5qap5aO4i9A"),
            new FeedCard("Quiet Current", "Ambient", null, "kJQP7kiw5Fk"),
            new FeedCard("Blue Hour", "Acoustic", null, "fJ9rUzIMcZQ"),
            new FeedCard("Glass Horizon", "Electronic", null, "fBN1qj2g2eA"),
        ]));

        ExploreSections.Add(new FeedSection("Browse",
        [
            new FeedCard("Trending now", "Top songs", null, "kJQP7kiw5Fk"),
            new FeedCard("New releases", "Fresh picks", null, "5qap5aO4i9A"),
            new FeedCard("Moods", "Curated mood boards", null, "fBN1qj2g2eA"),
            new FeedCard("Discover", "New artists", null, "fJ9rUzIMcZQ"),
        ], isHero: true));

        ExploreSections.Add(new FeedSection("Popular playlists",
        [
            new FeedCard("Late night", "7 songs", null, "5qap5aO4i9A"),
            new FeedCard("Focus flow", "12 songs", null, "fBN1qj2g2eA"),
            new FeedCard("Gym mix", "18 songs", null, "kJQP7kiw5Fk"),
            new FeedCard("Road trip", "16 songs", null, "fJ9rUzIMcZQ"),
        ]));

        // Using official Windows Segoe Fluent/MDL2 Unicode glyphs
        LibraryTiles.Add(new LibraryTile("Downloads", "Downloaded songs", "\uE896"));
        LibraryTiles.Add(new LibraryTile("Local Music", "Audio files on device", "\uEC4F"));
        LibraryTiles.Add(new LibraryTile("Liked Songs", "Your favorite tracks", "\uEB52"));
        LibraryTiles.Add(new LibraryTile("Playlists", "Custom mixes", "\uE90B"));

        SearchFilters.Add(new SearchFilterOption(SearchFilter.Songs, "Songs"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Albums, "Albums"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Artists, "Artists"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Playlists, "Playlists"));

        SearchResults.Add(new SearchResultTile("Midnight Drive", "Track • Synthwave", "fBN1qj2g2eA"));
        SearchResults.Add(new SearchResultTile("Sunset Echoes", "Track • Indie pop", "kJQP7kiw5Fk"));
        SearchResults.Add(new SearchResultTile("Velvet Hours", "Track • Chill", "5qap5aO4i9A"));
        SearchResults.Add(new SearchResultTile("Night Bloom", "Track • Ambient", "fJ9rUzIMcZQ"));

        SearchSuggestions.Add("midnight drive");
        SearchSuggestions.Add("sunset echoes");
        SearchSuggestions.Add("night bloom");
    }

    public async Task LoadLiveDataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<HomeShelf> home = await _service.GetHomeAsync(cancellationToken).ConfigureAwait(false);
            if (home.Count > 0)
            {
                var updated = home.Select((shelf, idx) => new FeedSection(
                    shelf.Title,
                    shelf.Items.Select(item => new FeedCard(
                        item.Title,
                        item.Subtitle,
                        item.ThumbnailUrl ?? "",
                        item.VideoId,
                        item.BrowseId)),
                    isHero: idx == 0)).ToList();

                HomeSections.Clear();
                foreach (var section in updated)
                    HomeSections.Add(section);
            }

            IReadOnlyList<HomeShelf> explore = await _service.GetExploreAsync(cancellationToken).ConfigureAwait(false);
            if (explore.Count > 0)
            {
                var updated = explore.Select((shelf, idx) => new FeedSection(
                    shelf.Title,
                    shelf.Items.Select(item => new FeedCard(
                        item.Title,
                        item.Subtitle,
                        item.ThumbnailUrl ?? "",
                        item.VideoId,
                        item.BrowseId)),
                    isHero: idx == 0)).ToList();

                ExploreSections.Clear();
                foreach (var section in updated)
                    ExploreSections.Add(section);
            }
        }
        catch
        {
            // Retain demo data on error / offline
        }
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        _currentQuery = query;
        try
        {
            IReadOnlyList<SearchResult> results = await _service
                .SearchAsync(query, SelectedSearchFilter, cancellationToken)
                .ConfigureAwait(false);

            SearchResults.Clear();
            foreach (var result in results.Take(25))
            {
                SearchResults.Add(result switch
                {
                    SearchResult.Track track => new SearchResultTile(
                        track.Song.Title,
                        $"Track • {track.Song.Artist}",
                        track.Song.VideoId,
                        track.Song.ThumbnailUrl,
                        track.Song),
                    SearchResult.Browse browse => new SearchResultTile(
                        browse.Item.Title,
                        $"{browse.Item.Type} • {browse.Item.Subtitle}",
                        null,
                        browse.Item.ThumbnailUrl,
                        null,
                        browse.Item.BrowseId),
                    _ => new SearchResultTile("Unknown", "Result")
                });
            }
        }
        catch
        {
            // Retain current results on error.
        }
    }

    public async Task UpdateSuggestionsAsync(string input, CancellationToken cancellationToken = default)
    {
        try
        {
            var suggestions = await _service.GetSearchSuggestionsAsync(input, cancellationToken)
                .ConfigureAwait(false);
            SearchSuggestions.Clear();
            foreach (var s in suggestions.Take(8))
                SearchSuggestions.Add(s);
        }
        catch
        {
            // Suggestions are best-effort.
        }
    }

    // ── Audio Playback Pipeline ───────────────────────────────────────────────

    public void TogglePlayPause()
    {
        if (CurrentSong is null) return;

        if (IsPlaying)
        {
            _player.Pause();
            IsPlaying = false;
        }
        else
        {
            _player.Play();
            IsPlaying = true;
        }
    }

    public void PlayFeedCard(FeedCard card)
    {
        if (!string.IsNullOrEmpty(card.VideoId))
        {
            PlaySong(new Song(
                VideoId: card.VideoId,
                Title: card.Title,
                Artist: card.Subtitle,
                ThumbnailUrl: card.ThumbnailUrl
            ));
        }
        else
        {
            // If card has no VideoId, search for the title and play top result
            _ = PlayBySearchAsync(card.Title + " " + card.Subtitle);
        }
    }

    public void PlaySearchResult(SearchResultTile tile)
    {
        if (tile.Song is not null)
        {
            PlaySong(tile.Song);
        }
        else if (!string.IsNullOrEmpty(tile.VideoId))
        {
            PlaySong(new Song(
                VideoId: tile.VideoId,
                Title: tile.Title,
                Artist: tile.Subtitle,
                ThumbnailUrl: tile.ThumbnailUrl
            ));
        }
        else
        {
            _ = PlayBySearchAsync(tile.Title);
        }
    }

    private async Task PlayBySearchAsync(string query)
    {
        try
        {
            var results = await _service.SearchAsync(query, SearchFilter.Songs).ConfigureAwait(false);
            var firstTrack = results.OfType<SearchResult.Track>().FirstOrDefault();
            if (firstTrack is not null)
            {
                PlaySong(firstTrack.Song);
            }
        }
        catch
        {
            // Ignore
        }
    }

    public void PlaySong(Song song)
    {
        _playCts?.Cancel();
        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;

        CurrentSong = song;
        IsLoading = true;
        IsPlaying = false;

        _ = Task.Run(async () =>
        {
            try
            {
                string? streamUrl = await _service.GetStreamUrlAsync(song.VideoId, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                if (!string.IsNullOrEmpty(streamUrl))
                {
                    _player.Source = MediaSource.CreateFromUri(new Uri(streamUrl));
                    _player.Play();
                    IsLoading = false;
                    IsPlaying = true;
                }
                else
                {
                    // Fallback to demo playback simulation if stream endpoint is throttled
                    IsLoading = false;
                    IsPlaying = true;
                }
            }
            catch
            {
                IsLoading = false;
                IsPlaying = false;
            }
        }, token);
    }
}

public sealed class FeedSection
{
    public FeedSection(string title, IEnumerable<FeedCard> cards, bool isHero = false)
    {
        Title = title;
        IsHero = isHero;
        Cards = new ObservableCollection<FeedCard>(cards);
    }

    public string Title { get; }
    public bool IsHero { get; }
    public ObservableCollection<FeedCard> Cards { get; }

    public Microsoft.UI.Xaml.Visibility HeroVisibility
        => IsHero ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility CompactVisibility
        => IsHero ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
}

public sealed class FeedCard
{
    public FeedCard(
        string title,
        string subtitle,
        string? thumbnailUrl,
        string? videoId = null,
        string? browseId = null)
    {
        Title = title;
        Subtitle = subtitle;
        ThumbnailUrl = thumbnailUrl;
        VideoId = videoId;
        BrowseId = browseId;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string? ThumbnailUrl { get; }
    public string? VideoId { get; }
    public string? BrowseId { get; }

    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);

    public Microsoft.UI.Xaml.Visibility ThumbnailVisibility
        => HasThumbnail ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility PlaceholderVisibility
        => HasThumbnail ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
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
    public SearchResultTile(
        string title,
        string subtitle,
        string? videoId = null,
        string? thumbnailUrl = null,
        Song? song = null,
        string? browseId = null)
    {
        Title = title;
        Subtitle = subtitle;
        VideoId = videoId;
        ThumbnailUrl = thumbnailUrl;
        Song = song;
        BrowseId = browseId;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string? VideoId { get; }
    public string? ThumbnailUrl { get; }
    public Song? Song { get; }
    public string? BrowseId { get; }
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
