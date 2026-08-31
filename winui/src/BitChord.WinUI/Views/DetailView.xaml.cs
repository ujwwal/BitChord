using BitChord.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace BitChord.WinUI.Views;

public sealed partial class DetailView : UserControl
{
    public AppShellViewModel ViewModel { get; }

    public event Action? BackRequested;

    public DetailView(AppShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseDetailPage();
        BackRequested?.Invoke();
    }

    private void PlayAll_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentDetailPage is not null)
        {
            ViewModel.PlayAllDetail(ViewModel.CurrentDetailPage);
        }
    }

    private void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentDetailPage is not null)
        {
            ViewModel.ShuffleDetail(ViewModel.CurrentDetailPage);
        }
    }

    private void Track_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Song song && ViewModel.CurrentDetailPage is not null)
        {
            int idx = ViewModel.CurrentDetailPage.Songs.IndexOf(song);
            if (idx >= 0)
            {
                ViewModel.PlayDetailTrack(ViewModel.CurrentDetailPage, idx);
            }
        }
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FeedCard card)
        {
            ViewModel.PlayFeedCard(card);
        }
    }
}
