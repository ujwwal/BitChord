using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;

namespace BitChord.WinUI.Views;

public sealed partial class LibraryView : UserControl
{
    public LibraryView(ObservableCollection<LibraryTile>? tiles = null)
    {
        InitializeComponent();
        TilesHost.ItemsSource = tiles ?? new ObservableCollection<LibraryTile>();
    }
}
