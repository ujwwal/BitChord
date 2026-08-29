using BitChord.WinUI.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace BitChord.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly FeedView _homeView = new("Listen Now");
    private readonly FeedView _exploreView = new("Explore");
    private readonly LibraryView _libraryView = new();
    private readonly SearchView _searchView = new();
    private int _selectedIndex = -1;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureTitleBar();

        BottomBar.TabSelected += SelectTab;
        RootGrid.ActualThemeChanged += (_, _) =>
        {
            BottomBar.RefreshColors();
            ConfigureTitleBar();
        };
        Activated += OnActivated;
        SelectTab(0);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        AppWindow.Resize(new SizeInt32(520, 860));
    }

    private void ConfigureTitleBar()
    {
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
            {
                _searchView.FocusSearchBox();
            }

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
