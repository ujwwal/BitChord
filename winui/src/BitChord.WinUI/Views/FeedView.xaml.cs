using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace BitChord.WinUI.Views;

public sealed partial class FeedView : UserControl
{
    public event Action<FeedCard>? CardClicked;

    public FeedView(string title, ObservableCollection<FeedSection>? sections = null)
    {
        InitializeComponent();
        Heading.Text = title;
        SectionsHost.ItemsSource = sections ?? new ObservableCollection<FeedSection>();
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is FeedCard card)
        {
            CardClicked?.Invoke(card);
        }
        else if (e.OriginalSource is FrameworkElement orig && orig.DataContext is FeedCard card2)
        {
            CardClicked?.Invoke(card2);
        }
    }
}
