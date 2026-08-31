using BitChord.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BitChord.WinUI.Views;

public sealed partial class SearchView : UserControl
{
    private DispatcherTimer? _debounceTimer;
    private CancellationTokenSource? _suggestionCts;
    private CancellationTokenSource? _searchCts;
    private readonly List<(Border chip, SearchFilter filter)> _chips = new();

    public AppShellViewModel ViewModel { get; }
    public event Action<SearchResultTile>? ResultClicked;

    public SearchView(AppShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        BuildFilterChips();

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppShellViewModel.SelectedSearchFilter))
                RefreshChipStyles();
        };
    }

    private void BuildFilterChips()
    {
        FilterPanel.Children.Clear();
        _chips.Clear();

        foreach (var option in ViewModel.SearchFilters)
        {
            var label = new TextBlock
            {
                Text = option.Label,
                FontFamily = (FontFamily)Application.Current.Resources["SemiboldFont"],
                FontSize = 13,
            };

            var chip = new Border
            {
                Padding = new Thickness(14, 6, 14, 6),
                CornerRadius = new CornerRadius(14),
                Child = label,
                Tag = option.Filter,
                IsHitTestVisible = true,
                Background = (SolidColorBrush)Application.Current.Resources["SurfaceVariantBrush"]
            };

            chip.Tapped += OnFilterChipTapped;

            _chips.Add((chip, option.Filter));
            FilterPanel.Children.Add(chip);
        }

        RefreshChipStyles();
    }

    private void OnFilterChipTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: SearchFilter filter })
        {
            ViewModel.SelectedSearchFilter = filter;
            RefreshChipStyles();

            var query = SearchBox.Text.Trim();
            if (!string.IsNullOrEmpty(query))
                _ = RunSearchAsync(query);
        }
    }

    private void RefreshChipStyles()
    {
        var accentBrush = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
        var surfaceBrush = (SolidColorBrush)Application.Current.Resources["SurfaceVariantBrush"];

        foreach (var (chip, filter) in _chips)
        {
            bool isSelected = filter == ViewModel.SelectedSearchFilter;
            chip.Background = isSelected ? accentBrush : surfaceBrush;

            if (chip.Child is TextBlock label)
            {
                label.Foreground = isSelected
                    ? new SolidColorBrush(Colors.White)
                    : (SolidColorBrush)Application.Current.Resources["PrimaryTextBrush"];
            }
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;

        var text = sender.Text.Trim();

        _suggestionCts?.Cancel();
        _suggestionCts = new CancellationTokenSource();

        if (string.IsNullOrEmpty(text))
        {
            ViewModel.SearchSuggestions.Clear();
            return;
        }

        _debounceTimer?.Stop();
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        var capturedToken = _suggestionCts.Token;
        _debounceTimer.Tick += async (_, _) =>
        {
            _debounceTimer?.Stop();
            if (!capturedToken.IsCancellationRequested)
                await ViewModel.UpdateSuggestionsAsync(text, capturedToken);
        };
        _debounceTimer.Start();
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = (args.QueryText ?? sender.Text).Trim();
        if (!string.IsNullOrEmpty(query))
            _ = RunSearchAsync(query);
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string suggestion)
        {
            sender.Text = suggestion;
            _ = RunSearchAsync(suggestion);
        }
    }

    private async Task RunSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        await ViewModel.SearchAsync(query, _searchCts.Token);
    }

    private void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResultTile tile)
        {
            ResultClicked?.Invoke(tile);
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SearchResultTile tile)
        {
            ResultClicked?.Invoke(tile);
        }
    }

    public void FocusSearchBox()
    {
        DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
    }
}

