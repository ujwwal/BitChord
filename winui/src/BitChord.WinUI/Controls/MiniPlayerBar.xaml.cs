using BitChord.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace BitChord.WinUI.Controls;

public sealed partial class MiniPlayerBar : UserControl
{
    public event EventHandler? PlayPauseClicked;
    public event EventHandler? NextClicked;
    public event EventHandler? BarClicked;

    public MiniPlayerBar()
    {
        InitializeComponent();
    }

    private Song? _song;
    public Song? Song
    {
        get => _song;
        set
        {
            _song = value;
            ApplySong(value);
        }
    }

    private void ApplySong(Song? song)
    {
        if (song is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;
        TitleText.Text = song.Title;
        ArtistText.Text = song.Artist;

        if (!string.IsNullOrEmpty(song.ThumbnailUrl))
        {
            try
            {
                ArtworkImage.Source = new BitmapImage(new Uri(song.ThumbnailUrl));
                ArtworkImage.Visibility = Visibility.Visible;
                ArtworkPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ArtworkImage.Visibility = Visibility.Collapsed;
                ArtworkPlaceholder.Visibility = Visibility.Visible;
            }
        }
        else
        {
            ArtworkImage.Visibility = Visibility.Collapsed;
            ArtworkPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            // In Segoe MDL2/Fluent: \uE768 = Play, \uE769 = Pause
            PlayPauseIcon.Glyph = value ? "\uE769" : "\uE768";
            ToolTipService.SetToolTip(PlayPauseButton, value ? "Pause" : "Play");
        }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            LoadingRing.IsActive = value;
            LoadingRing.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            PlayPauseButton.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        => PlayPauseClicked?.Invoke(this, EventArgs.Empty);

    private void NextButton_Click(object sender, RoutedEventArgs e)
        => NextClicked?.Invoke(this, EventArgs.Empty);

    private void PillBorder_Tapped(object sender, TappedRoutedEventArgs e)
        => BarClicked?.Invoke(this, EventArgs.Empty);
}
