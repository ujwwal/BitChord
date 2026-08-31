using BitChord.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace BitChord.WinUI.Views;

public sealed partial class NowPlayingView : UserControl
{
    public AppShellViewModel ViewModel { get; }

    public event Action? DismissRequested;
    public event Action<string, string>? OpenArtistRequested;

    public NowPlayingView(AppShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppShellViewModel.CurrentLyricIndex))
            {
                ScrollToActiveLyric();
            }
            else if (e.PropertyName == nameof(AppShellViewModel.CanvasVideoUrl))
            {
                UpdateCanvasPlayer();
            }
        };
    }

    private void UpdateCanvasPlayer()
    {
        if (ViewModel.HasCanvas && !string.IsNullOrEmpty(ViewModel.CanvasVideoUrl))
        {
            try
            {
                CanvasVideoPlayer.Source = Windows.Media.Core.MediaSource.CreateFromUri(new Uri(ViewModel.CanvasVideoUrl));
                if (CanvasVideoPlayer.MediaPlayer is not null)
                {
                    CanvasVideoPlayer.MediaPlayer.IsMuted = true;
                    CanvasVideoPlayer.MediaPlayer.IsLoopingEnabled = true;
                    CanvasVideoPlayer.MediaPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to load Canvas video: {ex.Message}");
            }
        }
        else
        {
            try
            {
                CanvasVideoPlayer.Source = null;
            }
            catch { }
        }
    }

    private void ScrollToActiveLyric()
    {
        if (ViewModel.ActiveLyrics.Count == 0 || ViewModel.CurrentLyricIndex < 0) return;

        // Auto-scroll lyrics smoothly into vertical center
        try
        {
            double targetOffset = Math.Max(0, (ViewModel.CurrentLyricIndex * 54.0) - 150.0);
            LyricsScrollViewer.ChangeView(null, targetOffset, null, false);
        }
        catch
        {
            // Ignore scroll race
        }
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        DismissRequested?.Invoke();
    }

    private void ModeArtwork_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNowPlayingMode(NowPlayingMode.Artwork);
    }

    private void ModeLyrics_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNowPlayingMode(NowPlayingMode.Lyrics);
    }

    private void ModeQueue_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNowPlayingMode(NowPlayingMode.Queue);
    }

    private void LyricsPreview_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.SetNowPlayingMode(NowPlayingMode.Lyrics);
    }

    private void LyricLine_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is LyricLineUiModel line)
        {
            ViewModel.SeekToLyric(line);
        }
    }

    private void Artist_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.CurrentSong is not null && !string.IsNullOrEmpty(ViewModel.CurrentSong.ArtistId))
        {
            OpenArtistRequested?.Invoke(ViewModel.CurrentSong.ArtistId, ViewModel.CurrentSong.Artist);
            DismissRequested?.Invoke();
        }
    }

    private void QueueItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Song song)
        {
            int idx = ViewModel.ActiveQueue.IndexOf(song);
            if (idx >= 0)
            {
                ViewModel.JumpToQueueIndex(idx);
            }
        }
    }

    private void RemoveQueueItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Song song)
        {
            int idx = ViewModel.ActiveQueue.IndexOf(song);
            if (idx >= 0)
            {
                ViewModel.RemoveFromQueue(idx);
            }
        }
    }

    private void ClearQueue_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearQueue();
    }

    private void Like_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleLike();
    private void PlayPause_Click(object sender, RoutedEventArgs e) => ViewModel.TogglePlayPause();
    private void Prev_Click(object sender, RoutedEventArgs e) => ViewModel.PlayPrevious();
    private void Next_Click(object sender, RoutedEventArgs e) => ViewModel.PlayNext();
    private void Shuffle_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleShuffle();
    private void Repeat_Click(object sender, RoutedEventArgs e) => ViewModel.CycleRepeatMode();
    private void Autoplay_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleAutoplay();

    public string GetPlayPauseGlyph(bool isPlaying) => isPlaying ? "\uE769" : "\uE768";

    public string GetLikeGlyph(bool isLiked) => isLiked ? "\uEB52" : "\uEB51";

    public Brush GetLikeBrush(bool isLiked) => isLiked
        ? new SolidColorBrush(ColorHelper.FromArgb(255, 250, 45, 72))
        : new SolidColorBrush(Colors.White);

    public Brush GetModeBackground(NowPlayingMode currentMode, int target)
    {
        int cur = (int)currentMode;
        return cur == target
            ? new SolidColorBrush(ColorHelper.FromArgb(255, 250, 45, 72))
            : new SolidColorBrush(Colors.Transparent);
    }

    public Brush GetModeForeground(NowPlayingMode currentMode, int target)
    {
        int cur = (int)currentMode;
        return cur == target
            ? new SolidColorBrush(Colors.White)
            : new SolidColorBrush(ColorHelper.FromArgb(200, 255, 255, 255));
    }

    public Brush GetButtonActiveBrush(bool active) => active
        ? new SolidColorBrush(ColorHelper.FromArgb(50, 250, 45, 72))
        : new SolidColorBrush(ColorHelper.FromArgb(26, 255, 255, 255));

    public Brush GetButtonActiveForeground(bool active) => active
        ? new SolidColorBrush(ColorHelper.FromArgb(255, 250, 45, 72))
        : new SolidColorBrush(Colors.White);

    public Brush GetRepeatActiveBrush(PlayerRepeatMode mode) => mode != PlayerRepeatMode.Off
        ? new SolidColorBrush(ColorHelper.FromArgb(50, 250, 45, 72))
        : new SolidColorBrush(ColorHelper.FromArgb(26, 255, 255, 255));

    public Brush GetRepeatActiveForeground(PlayerRepeatMode mode) => mode != PlayerRepeatMode.Off
        ? new SolidColorBrush(ColorHelper.FromArgb(255, 250, 45, 72))
        : new SolidColorBrush(Colors.White);

    public Visibility GetLyricsUnavailableVisibility(LyricsState state)
        => state == LyricsState.Unavailable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GetCanvasVisibility(bool hasCanvas)
        => hasCanvas ? Visibility.Visible : Visibility.Collapsed;
}
