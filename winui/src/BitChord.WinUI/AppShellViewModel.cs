using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using BitChord.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace BitChord.WinUI;

public enum PlayerRepeatMode
{
    Off,
    All,
    One
}

public enum LyricsState
{
    None,
    Loading,
    Loaded,
    Unavailable
}

public enum NowPlayingMode
{
    Artwork,
    Lyrics,
    Queue
}

public sealed class LyricLineUiModel : INotifyPropertyChanged
{
    public long TimeMs { get; }
    public string Text { get; }
    public bool IsGap { get; }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineOpacity));
                OnPropertyChanged(nameof(FontWeight));
                OnPropertyChanged(nameof(Scale));
                OnPropertyChanged(nameof(GlowVisibility));
            }
        }
    }

    public double LineOpacity => IsActive ? 1.0 : 0.42;
    public double Scale => IsActive ? 1.04 : 1.0;
    public Windows.UI.Text.FontWeight FontWeight => IsActive ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold;
    public Visibility GlowVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public LyricLineUiModel(long timeMs, string text, bool isGap = false)
    {
        TimeMs = timeMs;
        Text = text;
        IsGap = isGap;
    }
}

public sealed class DetailPageModel : INotifyPropertyChanged
{
    public string BrowseId { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string? ThumbnailUrl { get; }
    public BrowseType Type { get; }

    public ObservableCollection<Song> Songs { get; } = new();
    public ObservableCollection<FeedSection> Sections { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string? _description;
    public string? Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    private string? _subscriberCount;
    public string? SubscriberCount
    {
        get => _subscriberCount;
        set { _subscriberCount = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public DetailPageModel(string browseId, string title, string subtitle, string? thumbnailUrl, BrowseType type)
    {
        BrowseId = browseId;
        Title = title;
        Subtitle = subtitle;
        ThumbnailUrl = thumbnailUrl;
        Type = type;
    }
}

public sealed class AppShellViewModel : INotifyPropertyChanged
{
    private readonly BitChordService _service = new();
    private readonly MediaPlayer _player = new();
    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherTimer _playbackTimer;
    private CancellationTokenSource? _playCts;
    private CancellationTokenSource? _lyricsCts;

    public ObservableCollection<FeedSection> HomeSections { get; } = new();
    public ObservableCollection<FeedSection> ExploreSections { get; } = new();
    public ObservableCollection<LibraryTile> LibraryTiles { get; } = new();
    public ObservableCollection<SearchFilterOption> SearchFilters { get; } = new();
    public ObservableCollection<SearchResultTile> SearchResults { get; } = new();
    public ObservableCollection<string> SearchSuggestions { get; } = new();
    public ObservableCollection<Song> ActiveQueue { get; } = new();
    public ObservableCollection<LyricLineUiModel> ActiveLyrics { get; } = new();

    // ── Detail View State ─────────────────────────────────────────────────────
    private DetailPageModel? _currentDetailPage;
    public DetailPageModel? CurrentDetailPage
    {
        get => _currentDetailPage;
        set
        {
            _currentDetailPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDetailViewActive));
        }
    }

    public bool IsDetailViewActive => CurrentDetailPage is not null;

    // ── Now Playing View Mode ─────────────────────────────────────────────────
    private NowPlayingMode _nowPlayingMode = NowPlayingMode.Artwork;
    public NowPlayingMode ActiveNowPlayingMode
    {
        get => _nowPlayingMode;
        set
        {
            _nowPlayingMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsArtworkMode));
            OnPropertyChanged(nameof(IsLyricsMode));
            OnPropertyChanged(nameof(IsQueueMode));
        }
    }

    public bool IsArtworkMode => ActiveNowPlayingMode == NowPlayingMode.Artwork;
    public bool IsLyricsMode => ActiveNowPlayingMode == NowPlayingMode.Lyrics;
    public bool IsQueueMode => ActiveNowPlayingMode == NowPlayingMode.Queue;

    // ── Lyrics Subsystem State ────────────────────────────────────────────────
    private LyricsState _lyricsState = LyricsState.None;
    public LyricsState ActiveLyricsState
    {
        get => _lyricsState;
        set
        {
            _lyricsState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLyrics));
            OnPropertyChanged(nameof(IsLyricsLoading));
        }
    }

    public bool HasLyrics => ActiveLyricsState == LyricsState.Loaded && ActiveLyrics.Count > 0;
    public bool IsLyricsLoading => ActiveLyricsState == LyricsState.Loading;

    private int _currentLyricIndex = -1;
    public int CurrentLyricIndex
    {
        get => _currentLyricIndex;
        set
        {
            if (_currentLyricIndex != value)
            {
                _currentLyricIndex = value;
                OnPropertyChanged();
                UpdateLyricsHighlight();
            }
        }
    }

    private string _lyricsSnippet = "Tap for synchronized lyrics >";
    public string LyricsSnippet
    {
        get => _lyricsSnippet;
        set { _lyricsSnippet = value; OnPropertyChanged(); }
    }

    private CanvasArtwork? _activeCanvas;
    public CanvasArtwork? ActiveCanvas
    {
        get => _activeCanvas;
        set
        {
            _activeCanvas = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasCanvas));
            OnPropertyChanged(nameof(CanvasVideoUrl));
        }
    }

    public bool HasCanvas => ActiveCanvas is not null && !string.IsNullOrEmpty(ActiveCanvas.VideoUrl);
    public string? CanvasVideoUrl => ActiveCanvas?.VideoUrl;

    // ── Playback State Properties ─────────────────────────────────────────────
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

    private TimeSpan _position = TimeSpan.Zero;
    public TimeSpan Position
    {
        get => _position;
        set
        {
            _position = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PositionText));
            OnPropertyChanged(nameof(RemainingText));
            OnPropertyChanged(nameof(Progress));
        }
    }

    private TimeSpan _duration = TimeSpan.FromMinutes(3.5);
    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            _duration = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(RemainingText));
            OnPropertyChanged(nameof(Progress));
        }
    }

    public double Progress
    {
        get => Duration.TotalSeconds > 0 ? Position.TotalSeconds / Duration.TotalSeconds : 0;
        set
        {
            if (Duration.TotalSeconds > 0)
            {
                SeekTo(value);
            }
        }
    }

    public string PositionText => FormatTime(Position);
    public string DurationText => FormatTime(Duration);
    public string RemainingText => "-" + FormatTime(Duration > Position ? Duration - Position : TimeSpan.Zero);

    private static string FormatTime(TimeSpan time)
    {
        return time.Hours > 0
            ? $"{time.Hours}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes}:{time.Seconds:D2}";
    }

    private double _volume = 1.0;
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0.0, 1.0);
            _player.Volume = _volume;
            OnPropertyChanged();
        }
    }

    private bool _isLiked;
    public bool IsLiked
    {
        get => _isLiked;
        set { _isLiked = value; OnPropertyChanged(); }
    }

    private bool _isShuffled;
    public bool IsShuffled
    {
        get => _isShuffled;
        set { _isShuffled = value; OnPropertyChanged(); }
    }

    private PlayerRepeatMode _repeatMode = PlayerRepeatMode.All;
    public PlayerRepeatMode RepeatMode
    {
        get => _repeatMode;
        set
        {
            _repeatMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RepeatGlyph));
        }
    }

    public string RepeatGlyph => RepeatMode switch
    {
        PlayerRepeatMode.One => "1",
        PlayerRepeatMode.All => "\uE8EE",
        _ => "\uF5E7"
    };

    private bool _isAutoplayEnabled = true;
    public bool IsAutoplayEnabled
    {
        get => _isAutoplayEnabled;
        set { _isAutoplayEnabled = value; OnPropertyChanged(); }
    }

    private string _audioQualityBadge = "🎧 256 kbps AAC";
    public string AudioQualityBadge
    {
        get => _audioQualityBadge;
        set { _audioQualityBadge = value; OnPropertyChanged(); }
    }

    private int _queueIndex = -1;
    public int CurrentQueueIndex
    {
        get => _queueIndex;
        set { _queueIndex = value; OnPropertyChanged(); }
    }

    // ── Search State ──────────────────────────────────────────────────────────
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
    public void RunOnUI(Action action)
    {
        if (_dispatcher is not null && !_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => RunOnUI(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)));

    public AppShellViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread() ?? DispatcherQueue.GetForCurrentThread();
        
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _playbackTimer.Tick += OnPlaybackTimerTick;

        SetupMediaPlayer();
        LoadInitialData();
        _ = LoadLiveDataAsync();
    }

    private void SetupMediaPlayer()
    {
        _player.PlaybackSession.PlaybackStateChanged += (sender, _) =>
        {
            var state = sender.PlaybackState;
            RunOnUI(() =>
            {
                IsPlaying = state == MediaPlaybackState.Playing;
                IsLoading = state == MediaPlaybackState.Buffering || state == MediaPlaybackState.Opening;
                if (IsPlaying)
                {
                    _playbackTimer.Start();
                }
                else
                {
                    _playbackTimer.Stop();
                }
            });
        };

        _player.MediaEnded += (_, _) =>
        {
            RunOnUI(() =>
            {
                if (RepeatMode == PlayerRepeatMode.One)
                {
                    _player.PlaybackSession.Position = TimeSpan.Zero;
                    _player.Play();
                }
                else
                {
                    PlayNext();
                }
            });
        };

        _player.MediaFailed += (_, args) =>
        {
            AppLogger.Error($"MediaPlayer Failed: {args.Error}, Code=0x{args.ExtendedErrorCode?.HResult:X8}, Message={args.ErrorMessage}");
            RunOnUI(() =>
            {
                IsLoading = false;
                IsPlaying = false;
                _playbackTimer.Stop();
            });
        };
    }

    private void OnPlaybackTimerTick(object? sender, object e)
    {
        try
        {
            var session = _player.PlaybackSession;
            if (session is not null && (session.PlaybackState == MediaPlaybackState.Playing || session.PlaybackState == MediaPlaybackState.Buffering))
            {
                Position = session.Position;
                if (session.NaturalDuration > TimeSpan.Zero)
                {
                    Duration = session.NaturalDuration;
                }

                // Update lyrics index
                if (ActiveLyrics.Count > 0)
                {
                    long posMs = (long)Position.TotalMilliseconds;
                    int matchIdx = -1;
                    for (int i = 0; i < ActiveLyrics.Count; i++)
                    {
                        if (ActiveLyrics[i].TimeMs <= posMs)
                        {
                            matchIdx = i;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (matchIdx >= 0 && matchIdx != CurrentLyricIndex)
                    {
                        CurrentLyricIndex = matchIdx;
                        var line = ActiveLyrics[matchIdx];
                        if (!string.IsNullOrWhiteSpace(line.Text))
                        {
                            LyricsSnippet = line.Text;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore session race conditions
        }
    }

    private void UpdateLyricsHighlight()
    {
        for (int i = 0; i < ActiveLyrics.Count; i++)
        {
            ActiveLyrics[i].IsActive = (i == _currentLyricIndex);
        }
    }

    private void LoadInitialData()
    {
        HomeSections.Add(new FeedSection("Featured",
        [
            new FeedCard("Levitating", "Dua Lipa", "https://i.ytimg.com/vi/OsfAnsMY21M/hqdefault.jpg", "OsfAnsMY21M"),
            new FeedCard("Starboy", "The Weeknd • Daft Punk", "https://i.ytimg.com/vi/34Na4j8AVgA/hqdefault.jpg", "34Na4j8AVgA"),
            new FeedCard("Blinding Lights", "The Weeknd", "https://i.ytimg.com/vi/4NRXx6U8ABQ/hqdefault.jpg", "4NRXx6U8ABQ"),
            new FeedCard("Save Your Tears", "The Weeknd", "https://i.ytimg.com/vi/XXYlFuWEuKI/hqdefault.jpg", "XXYlFuWEuKI"),
        ], isHero: true));

        HomeSections.Add(new FeedSection("Made for you",
        [
            new FeedCard("Midnight City", "M83", "https://i.ytimg.com/vi/dX3k_QDnzHE/hqdefault.jpg", "dX3k_QDnzHE"),
            new FeedCard("After Hours", "The Weeknd", "https://i.ytimg.com/vi/ygTZZpVkm3o/hqdefault.jpg", "ygTZZpVkm3o"),
            new FeedCard("As It Was", "Harry Styles", "https://i.ytimg.com/vi/H5v3kku4y6Q/hqdefault.jpg", "H5v3kku4y6Q"),
            new FeedCard("Nightcall", "Kavinsky", "https://i.ytimg.com/vi/MV_3Dpw-BRY/hqdefault.jpg", "MV_3Dpw-BRY"),
        ]));

        ExploreSections.Add(new FeedSection("Trending Worldwide",
        [
            new FeedCard("Top Hits", "Popular now", "https://i.ytimg.com/vi/OsfAnsMY21M/hqdefault.jpg", "OsfAnsMY21M"),
            new FeedCard("New Releases", "Fresh music this week", "https://i.ytimg.com/vi/34Na4j8AVgA/hqdefault.jpg", "34Na4j8AVgA"),
            new FeedCard("Moods & Chill", "Relaxing acoustic & lofi", "https://i.ytimg.com/vi/dX3k_QDnzHE/hqdefault.jpg", "dX3k_QDnzHE"),
        ], isHero: true));

        LibraryTiles.Add(new LibraryTile("Downloads", "Downloaded songs", "\uE896"));
        LibraryTiles.Add(new LibraryTile("Local Music", "Audio files on device", "\uEC4F"));
        LibraryTiles.Add(new LibraryTile("Liked Songs", "Your favorite tracks", "\uEB52"));
        LibraryTiles.Add(new LibraryTile("Playlists", "Custom mixes", "\uE90B"));

        SearchFilters.Add(new SearchFilterOption(SearchFilter.Songs, "Songs"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Albums, "Albums"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Artists, "Artists"));
        SearchFilters.Add(new SearchFilterOption(SearchFilter.Playlists, "Playlists"));

        SearchResults.Add(new SearchResultTile("Levitating", "Track • Dua Lipa", "OsfAnsMY21M", "https://i.ytimg.com/vi/OsfAnsMY21M/hqdefault.jpg"));
        SearchResults.Add(new SearchResultTile("Blinding Lights", "Track • The Weeknd", "4NRXx6U8ABQ", "https://i.ytimg.com/vi/4NRXx6U8ABQ/hqdefault.jpg"));
        SearchResults.Add(new SearchResultTile("Starboy", "Track • The Weeknd", "34Na4j8AVgA", "https://i.ytimg.com/vi/34Na4j8AVgA/hqdefault.jpg"));

        SearchSuggestions.Add("dua lipa");
        SearchSuggestions.Add("the weeknd");
        SearchSuggestions.Add("blinding lights");
    }

    public async Task LoadLiveDataAsync(CancellationToken cancellationToken = default)
    {
        AppLogger.Info("Loading live YouTube Music home and explore shelves...");
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

                RunOnUI(() =>
                {
                    HomeSections.Clear();
                    foreach (var section in updated) HomeSections.Add(section);
                });
                AppLogger.Info($"Loaded {HomeSections.Count} home shelves.");
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

                RunOnUI(() =>
                {
                    ExploreSections.Clear();
                    foreach (var section in updated) ExploreSections.Add(section);
                });
                AppLogger.Info($"Loaded {ExploreSections.Count} explore shelves.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load live feeds", ex);
        }
    }

    public async Task SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        _currentQuery = query;
        AppLogger.Info($"Executing search query: '{query}' with filter {SelectedSearchFilter}");
        try
        {
            IReadOnlyList<SearchResult> results = await _service
                .SearchAsync(query, SelectedSearchFilter, cancellationToken)
                .ConfigureAwait(false);

            var items = results.Take(30).Select(result => result switch
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
            }).ToList();

            RunOnUI(() =>
            {
                SearchResults.Clear();
                foreach (var item in items) SearchResults.Add(item);
            });
            AppLogger.Info($"Found {SearchResults.Count} search results.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Search failed for query '{query}'", ex);
        }
    }

    public async Task UpdateSuggestionsAsync(string input, CancellationToken cancellationToken = default)
    {
        try
        {
            var suggestions = await _service.GetSearchSuggestionsAsync(input, cancellationToken).ConfigureAwait(false);
            var list = suggestions.Take(8).ToList();
            RunOnUI(() =>
            {
                SearchSuggestions.Clear();
                foreach (var s in list) SearchSuggestions.Add(s);
            });
        }
        catch
        {
            // Ignore
        }
    }

    // ── Collection / Detail Page Support ─────────────────────────────────────

    public async Task OpenBrowsePageAsync(string browseId, string title, string? subtitle, string? thumbUrl)
    {
        AppLogger.Info($"Opening Browse Page: {browseId} ('{title}')");

        var type = BrowseType.Other;
        if (browseId.StartsWith("MPRE", StringComparison.OrdinalIgnoreCase) || browseId.Contains("album", StringComparison.OrdinalIgnoreCase))
            type = BrowseType.Album;
        else if (browseId.StartsWith("UC", StringComparison.OrdinalIgnoreCase) || browseId.StartsWith("MPLA", StringComparison.OrdinalIgnoreCase))
            type = BrowseType.Artist;
        else if (browseId.StartsWith("VL", StringComparison.OrdinalIgnoreCase) || browseId.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
            type = BrowseType.Playlist;

        var detail = new DetailPageModel(browseId, title, subtitle ?? "", thumbUrl, type)
        {
            IsLoading = true
        };

        CurrentDetailPage = detail;

        try
        {
            var page = await _service.GetBrowsePageAsync(browseId).ConfigureAwait(false);
            
            RunOnUI(() =>
            {
                detail.IsLoading = false;
                detail.Description = page.Description;

                detail.Songs.Clear();
                foreach (var song in page.Songs)
                {
                    detail.Songs.Add(song);
                }

                detail.Sections.Clear();
                foreach (var shelf in page.BrowseSections)
                {
                    detail.Sections.Add(new FeedSection(
                        shelf.Title,
                        shelf.Items.Select(i => new FeedCard(i.Title, i.Subtitle, i.ThumbnailUrl, i.VideoId, i.BrowseId))));
                }
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to fetch browse page for {browseId}", ex);
            RunOnUI(() => detail.IsLoading = false);
        }
    }

    public void CloseDetailPage()
    {
        CurrentDetailPage = null;
    }

    public void PlayDetailTrack(DetailPageModel detailPage, int trackIndex)
    {
        if (trackIndex < 0 || trackIndex >= detailPage.Songs.Count) return;

        ActiveQueue.Clear();
        foreach (var song in detailPage.Songs)
        {
            ActiveQueue.Add(song);
        }
        CurrentQueueIndex = trackIndex;
        PlaySong(ActiveQueue[trackIndex]);
    }

    public void PlayAllDetail(DetailPageModel detailPage)
    {
        if (detailPage.Songs.Count == 0) return;
        PlayDetailTrack(detailPage, 0);
    }

    public void ShuffleDetail(DetailPageModel detailPage)
    {
        if (detailPage.Songs.Count == 0) return;

        var list = detailPage.Songs.ToList();
        var rnd = new Random();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = rnd.Next(i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }

        ActiveQueue.Clear();
        foreach (var s in list) ActiveQueue.Add(s);
        CurrentQueueIndex = 0;
        PlaySong(ActiveQueue[0]);
    }

    // ── Controls & Actions ───────────────────────────────────────────────────

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

    public void SeekTo(double progress)
    {
        if (Duration.TotalSeconds > 0)
        {
            var target = TimeSpan.FromSeconds(Math.Clamp(progress, 0.0, 1.0) * Duration.TotalSeconds);
            _player.PlaybackSession.Position = target;
            Position = target;
        }
    }

    public void SeekToLyric(LyricLineUiModel line)
    {
        if (line is not null && line.TimeMs >= 0)
        {
            var target = TimeSpan.FromMilliseconds(line.TimeMs);
            _player.PlaybackSession.Position = target;
            Position = target;
        }
    }

    public void ToggleLike()
    {
        IsLiked = !IsLiked;
    }

    public void ToggleShuffle()
    {
        IsShuffled = !IsShuffled;
        if (IsShuffled && ActiveQueue.Count > 1 && CurrentQueueIndex >= 0)
        {
            var cur = ActiveQueue[CurrentQueueIndex];
            var rest = ActiveQueue.Where((_, idx) => idx != CurrentQueueIndex).ToList();
            var rnd = new Random();
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int k = rnd.Next(i + 1);
                (rest[i], rest[k]) = (rest[k], rest[i]);
            }
            ActiveQueue.Clear();
            ActiveQueue.Add(cur);
            foreach (var r in rest) ActiveQueue.Add(r);
            CurrentQueueIndex = 0;
        }
    }

    public void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            PlayerRepeatMode.Off => PlayerRepeatMode.All,
            PlayerRepeatMode.All => PlayerRepeatMode.One,
            PlayerRepeatMode.One => PlayerRepeatMode.Off,
            _ => PlayerRepeatMode.Off
        };
    }

    public void ToggleAutoplay()
    {
        IsAutoplayEnabled = !IsAutoplayEnabled;
    }

    public void SetNowPlayingMode(NowPlayingMode mode)
    {
        ActiveNowPlayingMode = mode;
    }

    public void PlayPrevious()
    {
        if (Position.TotalSeconds > 3)
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
            Position = TimeSpan.Zero;
            return;
        }

        if (ActiveQueue.Count > 0 && CurrentQueueIndex > 0)
        {
            CurrentQueueIndex--;
            PlaySong(ActiveQueue[CurrentQueueIndex]);
        }
        else
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
            Position = TimeSpan.Zero;
        }
    }

    public void PlayNext()
    {
        if (ActiveQueue.Count > 0 && CurrentQueueIndex + 1 < ActiveQueue.Count)
        {
            CurrentQueueIndex++;
            PlaySong(ActiveQueue[CurrentQueueIndex]);
        }
        else if (IsAutoplayEnabled && CurrentSong is not null)
        {
            _ = FetchAutoplayTrackAsync(CurrentSong.VideoId);
        }
    }

    public void JumpToQueueIndex(int index)
    {
        if (index >= 0 && index < ActiveQueue.Count)
        {
            CurrentQueueIndex = index;
            PlaySong(ActiveQueue[index]);
        }
    }

    public void RemoveFromQueue(int index)
    {
        if (index >= 0 && index < ActiveQueue.Count)
        {
            ActiveQueue.RemoveAt(index);
            if (index < CurrentQueueIndex)
            {
                CurrentQueueIndex--;
            }
            else if (index == CurrentQueueIndex && ActiveQueue.Count > 0)
            {
                CurrentQueueIndex = Math.Min(CurrentQueueIndex, ActiveQueue.Count - 1);
                PlaySong(ActiveQueue[CurrentQueueIndex]);
            }
        }
    }

    public void ClearQueue()
    {
        if (CurrentSong is not null)
        {
            var cur = CurrentSong;
            ActiveQueue.Clear();
            ActiveQueue.Add(cur);
            CurrentQueueIndex = 0;
        }
        else
        {
            ActiveQueue.Clear();
            CurrentQueueIndex = -1;
        }
    }

    private async Task FetchAutoplayTrackAsync(string videoId)
    {
        try
        {
            var queue = await _service.GetWatchQueueAsync(videoId).ConfigureAwait(false);
            var nextSong = queue.FirstOrDefault(s => s.VideoId != videoId);
            if (nextSong is not null)
            {
                RunOnUI(() =>
                {
                    ActiveQueue.Add(nextSong);
                    CurrentQueueIndex = ActiveQueue.Count - 1;
                    PlaySong(nextSong);
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to fetch autoplay next track", ex);
        }
    }

    public void PlayFeedCard(FeedCard card)
    {
        if (!string.IsNullOrEmpty(card.BrowseId) && string.IsNullOrEmpty(card.VideoId))
        {
            // Open collection / artist page
            _ = OpenBrowsePageAsync(card.BrowseId, card.Title, card.Subtitle, card.ThumbnailUrl);
            return;
        }

        string thumb = card.ThumbnailUrl ?? "";
        if (string.IsNullOrEmpty(thumb) && !string.IsNullOrEmpty(card.VideoId))
        {
            thumb = $"https://i.ytimg.com/vi/{card.VideoId}/hqdefault.jpg";
        }

        var song = new Song(
            VideoId: !string.IsNullOrEmpty(card.VideoId) ? card.VideoId : "OsfAnsMY21M",
            Title: card.Title,
            Artist: card.Subtitle,
            ThumbnailUrl: thumb
        );

        PopulateQueueWithFeedCards(card);
        PlaySong(song);
    }

    private void PopulateQueueWithFeedCards(FeedCard selected)
    {
        ActiveQueue.Clear();
        foreach (var section in HomeSections.Concat(ExploreSections))
        {
            foreach (var c in section.Cards)
            {
                if (!string.IsNullOrEmpty(c.VideoId))
                {
                    ActiveQueue.Add(new Song(
                        VideoId: c.VideoId,
                        Title: c.Title,
                        Artist: c.Subtitle,
                        ThumbnailUrl: c.ThumbnailUrl
                    ));
                }
            }
        }
        CurrentQueueIndex = ActiveQueue.ToList().FindIndex(s => s.Title == selected.Title);
        if (CurrentQueueIndex < 0) CurrentQueueIndex = 0;
    }

    public void PlaySearchResult(SearchResultTile tile)
    {
        if (!string.IsNullOrEmpty(tile.BrowseId) && string.IsNullOrEmpty(tile.VideoId))
        {
            _ = OpenBrowsePageAsync(tile.BrowseId, tile.Title, tile.Subtitle, tile.ThumbnailUrl);
            return;
        }

        string thumb = tile.ThumbnailUrl ?? "";
        if (string.IsNullOrEmpty(thumb) && !string.IsNullOrEmpty(tile.VideoId))
        {
            thumb = $"https://i.ytimg.com/vi/{tile.VideoId}/hqdefault.jpg";
        }

        var song = tile.Song ?? new Song(
            VideoId: !string.IsNullOrEmpty(tile.VideoId) ? tile.VideoId : "OsfAnsMY21M",
            Title: tile.Title,
            Artist: tile.Subtitle,
            ThumbnailUrl: thumb
        );

        ActiveQueue.Clear();
        foreach (var item in SearchResults)
        {
            if (!string.IsNullOrEmpty(item.VideoId))
            {
                ActiveQueue.Add(item.Song ?? new Song(
                    VideoId: item.VideoId,
                    Title: item.Title,
                    Artist: item.Subtitle,
                    ThumbnailUrl: item.ThumbnailUrl
                ));
            }
        }
        CurrentQueueIndex = ActiveQueue.ToList().FindIndex(s => s.Title == song.Title);
        if (CurrentQueueIndex < 0) CurrentQueueIndex = 0;

        PlaySong(song);
    }

    public void PlaySong(Song song)
    {
        AppLogger.Info($"PlaySong requested: '{song.Title}' by '{song.Artist}' (videoId: {song.VideoId})");

        _playCts?.Cancel();
        _playCts = new CancellationTokenSource();
        var token = _playCts.Token;

        _lyricsCts?.Cancel();
        _lyricsCts = new CancellationTokenSource();
        var lyricsToken = _lyricsCts.Token;

        RunOnUI(() =>
        {
            CurrentSong = song;
            IsLoading = true;
            IsPlaying = false;
            Position = TimeSpan.Zero;
            LyricsSnippet = $"Playing {song.Title} >";
            ActiveLyrics.Clear();
            ActiveLyricsState = LyricsState.Loading;
            CurrentLyricIndex = -1;
        });

        // 1. Fetch Lyrics in parallel
        _ = Task.Run(async () =>
        {
            try
            {
                var lines = await _service.GetLyricsAsync(song.Title, song.Artist, (long)Duration.TotalMilliseconds, lyricsToken).ConfigureAwait(false);
                if (lyricsToken.IsCancellationRequested) return;

                RunOnUI(() =>
                {
                    if (lines is not null && lines.Count > 0)
                    {
                        ActiveLyrics.Clear();
                        foreach (var l in lines)
                        {
                            ActiveLyrics.Add(new LyricLineUiModel(l.TimeMs, l.Text, l.IsGap));
                        }
                        ActiveLyricsState = LyricsState.Loaded;
                        AppLogger.Info($"Loaded {ActiveLyrics.Count} synced lyric lines for '{song.Title}'");
                    }
                    else
                    {
                        ActiveLyricsState = LyricsState.Unavailable;
                    }
                });
            }
            catch
            {
                RunOnUI(() => ActiveLyricsState = LyricsState.Unavailable);
            }
        }, lyricsToken);

        // 2. Fetch Motion Canvas in parallel
        _ = Task.Run(async () =>
        {
            try
            {
                var canvas = await _service.Canvas.GetCanvasAsync(song.VideoId, song.Title, song.Artist, null, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                RunOnUI(() =>
                {
                    ActiveCanvas = canvas;
                });
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Canvas lookup failed for '{song.Title}': {ex.Message}");
            }
        }, token);

        // 3. Resolve Audio Stream and Cache / Play
        _ = Task.Run(async () =>
        {
            try
            {
                // Check if already in disk cache
                string? cachedPath = _service.AudioCache.GetCachedFilePath(song.VideoId);
                if (cachedPath is not null && File.Exists(cachedPath))
                {
                    AppLogger.Info($"Playing from existing disk cache: {cachedPath}");
                    await StartPlaybackFromFileAsync(cachedPath, "🎧 Lossless / Local Cache").ConfigureAwait(false);
                    return;
                }

                // Download and cache stream using primary YoutubeExplode engine with fallback
                AppLogger.Info($"Downloading and streaming track '{song.Title}' ({song.VideoId})...");

                string targetFile = await _service.AudioCache.DownloadAndCacheStreamAsync(
                    song.VideoId,
                    null,
                    null,
                    token).ConfigureAwait(false);

                if (token.IsCancellationRequested) return;

                if (File.Exists(targetFile))
                {
                    await StartPlaybackFromFileAsync(targetFile, "🎧 256 kbps AAC").ConfigureAwait(false);
                    return;
                }

                AppLogger.Warn($"Failed to resolve playable stream for videoId: {song.VideoId}");
                RunOnUI(() =>
                {
                    IsLoading = false;
                    IsPlaying = false;
                    _playbackTimer.Stop();
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Playback error", ex);
                RunOnUI(() =>
                {
                    IsLoading = false;
                    IsPlaying = false;
                    _playbackTimer.Stop();
                });
            }
        }, token);
    }

    private async Task StartPlaybackFromFileAsync(string filePath, string qualityBadge)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        RunOnUI(() =>
        {
            AudioQualityBadge = qualityBadge;
            _player.Source = MediaSource.CreateFromStorageFile(file);
            _player.Play();
            IsLoading = false;
            IsPlaying = true;
            _playbackTimer.Start();
        });
        AppLogger.Info($"MediaPlayer playback started for {Path.GetFileName(filePath)}");
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
