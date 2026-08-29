using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BitChord.WinUI.Views;

public sealed partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
    }

    public void FocusSearchBox()
    {
        DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
    }
}
