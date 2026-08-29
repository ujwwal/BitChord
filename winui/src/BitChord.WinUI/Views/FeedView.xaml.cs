using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace BitChord.WinUI.Views;

public sealed partial class FeedView : UserControl
{
    public FeedView(string title)
    {
        InitializeComponent();
        Heading.Text = title;
        Loaded += (_, _) => StartSkeletonPulse();
    }

    private void StartSkeletonPulse()
    {
        var animation = new DoubleAnimation
        {
            From = 0.62,
            To = 0.9,
            Duration = new Duration(TimeSpan.FromMilliseconds(900)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true,
        };

        Storyboard.SetTarget(animation, SkeletonPanel);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
