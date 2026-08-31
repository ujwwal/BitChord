using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BitChord.WinUI.Views;

public sealed partial class NowPlayingView : UserControl
{
    public AppShellViewModel ViewModel { get; }
    public event Action? DismissRequested;
    public event Action? QueueRequested;

    private static readonly SolidColorBrush AccentBrush = new(ColorHelper.FromArgb(255, 230, 57, 70));
    private static readonly SolidColorBrush RedHeartBrush = new(ColorHelper.FromArgb(255, 255, 59, 48));
    private static readonly SolidColorBrush WhiteBrush = new(Colors.White);
    private static readonly SolidColorBrush TranslucentWhite = new(ColorHelper.FromArgb(40, 255, 255, 255));
    private static readonly SolidColorBrush ActivePillBg = new(ColorHelper.FromArgb(120, 230, 57, 70));
    private static readonly SolidColorBrush InactivePillBg = new(ColorHelper.FromArgb(26, 255, 255, 255));

    public NowPlayingView(AppShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e) => DismissRequested?.Invoke();

    private void Like_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleLike();

    private void More_Click(object sender, RoutedEventArgs e)
    {
        // Display more options flyout (e.g. Go to Artist, Go to Album, Share)
    }

    private void Lyrics_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Toggle or open lyrics view
    }

    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Handled via TwoWay binding
    }

    private void Prev_Click(object sender, RoutedEventArgs e) => ViewModel.PlayPrevious();

    private void PlayPause_Click(object sender, RoutedEventArgs e) => ViewModel.TogglePlayPause();

    private void Next_Click(object sender, RoutedEventArgs e) => ViewModel.PlayNext();

    private void Shuffle_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleShuffle();

    private void Repeat_Click(object sender, RoutedEventArgs e) => ViewModel.CycleRepeatMode();

    private void Autoplay_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleAutoplay();

    private void Queue_Click(object sender, RoutedEventArgs e) => QueueRequested?.Invoke();

    // ── Bindable Helpers ──────────────────────────────────────────────────────

    public string GetLikeGlyph(bool isLiked) => isLiked ? "\uEB52" : "\uEB51";

    public Brush GetLikeBrush(bool isLiked) => isLiked ? RedHeartBrush : WhiteBrush;

    public string GetPlayPauseGlyph(bool isPlaying) => isPlaying ? "\uE769" : "\uE768";

    public Brush GetButtonActiveBrush(bool isActive) => isActive ? ActivePillBg : InactivePillBg;

    public Brush GetButtonActiveForeground(bool isActive) => isActive ? AccentBrush : WhiteBrush;

    public Brush GetRepeatActiveBrush(PlayerRepeatMode mode) => mode != PlayerRepeatMode.Off ? ActivePillBg : InactivePillBg;

    public Brush GetRepeatActiveForeground(PlayerRepeatMode mode) => mode != PlayerRepeatMode.Off ? AccentBrush : WhiteBrush;
}
