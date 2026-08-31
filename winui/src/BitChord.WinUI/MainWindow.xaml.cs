using BitChord.Core;
using BitChord.WinUI.Dialogs;
using BitChord.WinUI.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace BitChord.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly AppShellViewModel _viewModel = new();
    private FeedView? _homeView;
    private FeedView? _exploreView;
    private LibraryView? _libraryView;
    private SearchView? _searchView;
    private DetailView? _detailView;
    private NowPlayingView? _nowPlayingView;
    private int _selectedIndex = -1;

    public MainWindow()
    {
        InitializeComponent();

        AppLogger.Info("Initializing MainWindow components...");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureTitleBar();

        // ── Now Playing View Setup ────────────────────────────────────────────
        _nowPlayingView = new NowPlayingView(_viewModel);
        _nowPlayingView.DismissRequested += () =>
        {
            NowPlayingHost.Visibility = Visibility.Collapsed;
        };
        _nowPlayingView.OpenArtistRequested += (artistId, artistName) =>
        {
            _ = _viewModel.OpenBrowsePageAsync(artistId, artistName, "Artist", null);
        };
        NowPlayingHost.Children.Add(_nowPlayingView);

        // ── Detail View Setup ─────────────────────────────────────────────────
        _detailView = new DetailView(_viewModel);
        _detailView.BackRequested += () =>
        {
            RestoreActiveTabView();
        };

        // ── Tab selection ─────────────────────────────────────────────────────
        BottomBar.TabSelected += SelectTab;
        RootGrid.ActualThemeChanged += (_, _) =>
        {
            BottomBar.RefreshColors();
            ConfigureTitleBar();
        };

        // ── MiniPlayerBar ←→ ViewModel Wiring ────────────────────────────────
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        MiniPlayer.PlayPauseClicked += (_, _) => _viewModel.TogglePlayPause();
        MiniPlayer.NextClicked += (_, _) => _viewModel.PlayNext();
        MiniPlayer.BarClicked += (_, _) =>
        {
            NowPlayingHost.Visibility = Visibility.Visible;
        };

        Activated += OnActivated;
        SelectTab(0);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppShellViewModel.CurrentSong):
                MiniPlayer.Song = _viewModel.CurrentSong;
                MiniPlayer.Visibility = _viewModel.CurrentSong is not null && NowPlayingHost.Visibility != Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                break;

            case nameof(AppShellViewModel.IsPlaying):
                MiniPlayer.IsPlaying = _viewModel.IsPlaying;
                break;

            case nameof(AppShellViewModel.IsLoading):
                MiniPlayer.IsLoading = _viewModel.IsLoading;
                break;

            case nameof(AppShellViewModel.LyricsSnippet):
                MiniPlayer.LyricSnippet = _viewModel.LyricsSnippet;
                break;

            case nameof(AppShellViewModel.IsDetailViewActive):
                if (_viewModel.IsDetailViewActive)
                {
                    ContentHost.Content = _detailView;
                    PageTitle.Text = _viewModel.CurrentDetailPage?.Title ?? "Details";
                    PageTitle.Opacity = 1;
                }
                else
                {
                    RestoreActiveTabView();
                }
                break;
        }
    }

    private void RestoreActiveTabView()
    {
        int idx = _selectedIndex >= 0 ? _selectedIndex : 0;
        _selectedIndex = -1; // force re-selection
        SelectTab(idx);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        AppLogger.Info("Resizing window to desktop dimensions: 1100x750");
        AppWindow.Resize(new SizeInt32(1100, 750));
    }

    private void ConfigureTitleBar()
    {
        if (AppWindow?.TitleBar is null) return;

        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        var isLight = RootGrid.ActualTheme == ElementTheme.Light;
        AppWindow.TitleBar.ButtonForegroundColor = isLight ? Colors.Black : Colors.White;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = isLight
            ? ColorHelper.FromArgb(255, 110, 110, 115)
            : ColorHelper.FromArgb(255, 142, 142, 147);
    }

    private void SelectTab(int index)
    {
        if (_viewModel.IsDetailViewActive)
        {
            _viewModel.CloseDetailPage();
        }

        if (index == _selectedIndex)
        {
            if (index == 3 && _searchView is not null)
                _searchView.FocusSearchBox();
            return;
        }

        AppLogger.Info($"Switching to tab index: {index}");
        _selectedIndex = index;

        UIElement activeView = index switch
        {
            0 => GetHomeView(),
            1 => GetExploreView(),
            2 => GetLibraryView(),
            3 => GetSearchView(),
            _ => GetHomeView(),
        };

        ContentHost.Content = activeView;

        PageTitle.Text = index switch
        {
            0 => "Listen Now",
            1 => "Explore",
            2 => "Library",
            3 => "Search",
            _ => string.Empty,
        };
        PageTitle.Opacity = index == 3 ? 1 : 0;
        BottomBar.SetSelectedIndex(index);
    }

    private FeedView GetHomeView()
    {
        if (_homeView is null)
        {
            _homeView = new FeedView("Listen Now", _viewModel.HomeSections);
            _homeView.CardClicked += card => _viewModel.PlayFeedCard(card);
        }
        return _homeView;
    }

    private FeedView GetExploreView()
    {
        if (_exploreView is null)
        {
            _exploreView = new FeedView("Explore", _viewModel.ExploreSections);
            _exploreView.CardClicked += card => _viewModel.PlayFeedCard(card);
        }
        return _exploreView;
    }

    private LibraryView GetLibraryView()
    {
        if (_libraryView is null)
        {
            _libraryView = new LibraryView(_viewModel.LibraryTiles);
            _libraryView.TileClicked += _ =>
            {
                SelectTab(3);
                _searchView?.FocusSearchBox();
            };
        }
        return _libraryView;
    }

    private SearchView GetSearchView()
    {
        if (_searchView is null)
        {
            _searchView = new SearchView(_viewModel);
            _searchView.ResultClicked += tile => _viewModel.PlaySearchResult(tile);
        }
        return _searchView;
    }

    private async void LogsButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("Opening Live Diagnostics and Logs dialog...");
        try
        {
            var dialog = new LogViewerDialog
            {
                XamlRoot = RootGrid.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to display LogViewerDialog", ex);
        }
    }

    private async void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("Opening Account and Settings dialog...");
        try
        {
            var dialog = new SettingsDialog
            {
                XamlRoot = RootGrid.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to display SettingsDialog", ex);
        }
    }
}
