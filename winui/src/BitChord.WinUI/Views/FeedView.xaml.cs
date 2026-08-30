using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BitChord.WinUI.Views;

public sealed partial class FeedView : UserControl
{
    public event Action<FeedCard>? CardClicked;

    public FeedView(string title, ObservableCollection<FeedSection>? sections = null)
    {
        InitializeComponent();
        Heading.Text = title;
        SectionsHost.ItemsSource = sections ?? new ObservableCollection<FeedSection>();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        LoadThumbnailsInSubtree(this);
    }

    private void Card_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FeedCard card })
        {
            CardClicked?.Invoke(card);
        }
    }

    private static void LoadThumbnailsInSubtree(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();

            if (node is Image img && img.DataContext is FeedCard card && card.HasThumbnail)
            {
                try
                {
                    img.Source = new BitmapImage(new Uri(card.ThumbnailUrl!));
                }
                catch
                {
                    // Fallback to placeholder
                }
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
                queue.Enqueue(VisualTreeHelper.GetChild(node, i));
        }
    }
}
