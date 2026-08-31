using System.Diagnostics;
using BitChord.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BitChord.WinUI.Dialogs;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.Info("Cache cleared by user.");
    }

    private async void ViewLiveLogs_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        try
        {
            var logDialog = new LogViewerDialog
            {
                XamlRoot = XamlRoot
            };
            await logDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open LogViewerDialog from settings", ex);
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? dir = Path.GetDirectoryName(AppLogger.LogFilePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open logs folder", ex);
        }
    }
}
