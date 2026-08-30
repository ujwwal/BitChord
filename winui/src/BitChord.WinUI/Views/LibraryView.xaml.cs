using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace BitChord.WinUI.Views;

public sealed partial class LibraryView : UserControl
{
    public event Action<LibraryTile>? TileClicked;
    public event Action? ReplayClicked;

    public LibraryView(ObservableCollection<LibraryTile>? tiles = null)
    {
        InitializeComponent();
        TilesHost.ItemsSource = tiles ?? new ObservableCollection<LibraryTile>();
    }

    private void Tile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LibraryTile tile })
        {
            TileClicked?.Invoke(tile);
        }
    }

    private void Replay_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ReplayClicked?.Invoke();
    }
}
