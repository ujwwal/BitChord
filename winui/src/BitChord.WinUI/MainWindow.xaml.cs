using BitChord.WinUI.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace BitChord.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly AppShellViewModel _viewModel = new();
    private readonly FeedView _homeView;
    private readonly FeedView _exploreView;
    private readonly LibraryView _libraryView;
    private readonly SearchView _searchView;
    private int _selectedIndex = -1;

    public MainWindow()
    {
        _homeView = new FeedView("Listen Now", _viewModel.HomeSections);
        _exploreView = new FeedView("Explore", _viewModel.ExploreSections);
        _libraryView = new LibraryView(_viewModel.LibraryTiles);
        _searchView = new SearchView(_viewModel);

        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureTitleBar();

        // ── Tab selection ─────────────────────────────────────────────────────
        BottomBar.TabSelected += SelectTab;
        RootGrid.ActualThemeChanged += (_, _) =>
        {
            BottomBar.RefreshColors();
            ConfigureTitleBar();
        };

        // ── Click to Play Wiring ──────────────────────────────────────────────
        _homeView.CardClicked += card => _viewModel.PlayFeedCard(card);
        _exploreView.CardClicked += card => _viewModel.PlayFeedCard(card);
        _searchView.ResultClicked += tile => _viewModel.PlaySearchResult(tile);
        _libraryView.TileClicked += tile =>
        {
            // When a library tile is clicked, switch to search or query for that category
            SelectTab(3);
            _searchView.FocusSearchBox();
        };

        // ── MiniPlayerBar ←→ ViewModel Wiring ────────────────────────────────
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        MiniPlayer.PlayPauseClicked += (_, _) => _viewModel.TogglePlayPause();
        MiniPlayer.NextClicked += (_, _) =>
        {
            // Skip to next item in home feed or queue
            var firstCard = _viewModel.HomeSections.FirstOrDefault()?.Cards.FirstOrDefault();
            if (firstCard is not null)
            {
                _viewModel.PlayFeedCard(firstCard);
            }
        };
        MiniPlayer.BarClicked += (_, _) => _viewModel.TogglePlayPause();

        Activated += OnActivated;
        SelectTab(0);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppShellViewModel.CurrentSong):
                MiniPlayer.Song = _viewModel.CurrentSong;
                MiniPlayer.Visibility = _viewModel.CurrentSong is not null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                break;

            case nameof(AppShellViewModel.IsPlaying):
                MiniPlayer.IsPlaying = _viewModel.IsPlaying;
                break;

            case nameof(AppShellViewModel.IsLoading):
                MiniPlayer.IsLoading = _viewModel.IsLoading;
                break;
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        AppWindow.Resize(new SizeInt32(500, 840));
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
        if (index == _selectedIndex)
        {
            if (index == 3)
                _searchView.FocusSearchBox();
            return;
        }

        _selectedIndex = index;
        ContentHost.Content = index switch
        {
            0 => _homeView,
            1 => _exploreView,
            2 => _libraryView,
            3 => _searchView,
            _ => _homeView,
        };

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
}
