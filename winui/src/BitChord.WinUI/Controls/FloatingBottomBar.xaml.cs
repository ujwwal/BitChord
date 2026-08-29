using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;

namespace BitChord.WinUI.Controls;

public sealed partial class FloatingBottomBar : UserControl
{
    private int _selectedIndex;
    private bool _hasSelection;

    public event Action<int>? TabSelected;

    public FloatingBottomBar()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshColors();
            UpdateIndicator(false);
        };
    }

    public void SetSelectedIndex(int index)
    {
        if (index is < 0 or > 3)
        {
            return;
        }

        var animate = _hasSelection && index != _selectedIndex;
        _selectedIndex = index;
        _hasSelection = true;
        RefreshColors();
        UpdateIndicator(animate);
        UpdateScales(animate);
    }

    public void RefreshColors()
    {
        var selected = new SolidColorBrush(ColorHelper.FromArgb(255, 250, 45, 72));
        var unselected = new SolidColorBrush(
            ActualTheme == ElementTheme.Light
                ? ColorHelper.FromArgb(255, 110, 110, 115)
                : ColorHelper.FromArgb(255, 142, 142, 147));

        var buttons = new[] { PlayButton, ExploreButton, LibraryButton, SearchButton };
        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index].Foreground = index == _selectedIndex ? selected : unselected;
        }
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string value } && int.TryParse(value, out var index))
        {
            TabSelected?.Invoke(index);
        }
    }

    private void ItemsGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateIndicator(false);
    }

    private void UpdateIndicator(bool animate)
    {
        var slotWidth = ItemsGrid.ActualWidth / 4;
        if (slotWidth <= 0)
        {
            return;
        }

        SelectionIndicator.Width = slotWidth;
        var target = slotWidth * _selectedIndex;
        if (!animate)
        {
            SelectionTransform.X = target;
            return;
        }

        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(320)),
            EnableDependentAnimation = true,
            EasingFunction = new BackEase
            {
                Amplitude = 0.18,
                EasingMode = EasingMode.EaseOut,
            },
        };
        Storyboard.SetTarget(animation, SelectionTransform);
        Storyboard.SetTargetProperty(animation, "X");
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void UpdateScales(bool animate)
    {
        var scales = new[] { PlayScale, ExploreScale, LibraryScale, SearchScale };
        for (var index = 0; index < scales.Length; index++)
        {
            var target = index == _selectedIndex ? 1.08 : 1.0;
            if (!animate)
            {
                scales[index].ScaleX = target;
                scales[index].ScaleY = target;
                continue;
            }

            AnimateScale(scales[index], "ScaleX", target);
            AnimateScale(scales[index], "ScaleY", target);
        }
    }

    private static void AnimateScale(ScaleTransform transform, string property, double target)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EnableDependentAnimation = true,
            EasingFunction = new BackEase
            {
                Amplitude = 0.12,
                EasingMode = EasingMode.EaseOut,
            },
        };
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
