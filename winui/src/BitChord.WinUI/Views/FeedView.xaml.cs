using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;

namespace BitChord.WinUI.Views;

public sealed partial class FeedView : UserControl
{
    public FeedView(string title, ObservableCollection<FeedSection>? sections = null)
    {
        InitializeComponent();
        Heading.Text = title;
        SectionsHost.ItemsSource = sections ?? new ObservableCollection<FeedSection>();
    }
}
