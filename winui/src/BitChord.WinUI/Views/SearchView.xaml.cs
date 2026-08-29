using System.Collections.ObjectModel;
using BitChord.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BitChord.WinUI.Views;

public sealed partial class SearchView : UserControl
{
    public SearchView(
        ObservableCollection<SearchResultTile>? results = null,
        ObservableCollection<SearchFilterOption>? filters = null,
        SearchFilter selectedFilter = SearchFilter.Songs)
    {
        InitializeComponent();
        ResultsList.ItemsSource = results ?? new ObservableCollection<SearchResultTile>();
        FilterHost.ItemsSource = filters ?? new ObservableCollection<SearchFilterOption>();

        var matchingFilter = filters?
            .FirstOrDefault(option => option.Filter == selectedFilter);

        if (matchingFilter is not null)
        {
            SearchBox.PlaceholderText = matchingFilter.Label;
        }
    }

    public void FocusSearchBox()
    {
        DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
    }
}
