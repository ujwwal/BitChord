using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using BitChord.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace BitChord.WinUI.Dialogs;

public sealed class LogItemUiModel
{
    public LogItem Model { get; }
    public string FormattedTimestamp => Model.FormattedTimestamp;
    public string Level => Model.Level;
    public string Message => Model.Message;
    public Brush LevelBackground => LogViewerDialog.GetLevelBackground(Model.Level);
    public Brush LevelForeground => LogViewerDialog.GetLevelForeground(Model.Level);

    public LogItemUiModel(LogItem model)
    {
        Model = model;
    }
}

public sealed partial class LogViewerDialog : ContentDialog
{
    public ObservableCollection<LogItemUiModel> FilteredLogs { get; } = new();
    private readonly List<LogItem> _allLogs = new();
    private string _filterText = string.Empty;
    private int _filterLevelIndex = 0;

    public LogViewerDialog()
    {
        InitializeComponent();
        LogPathText.Text = $"File: {AppLogger.LogFilePath}";

        // Load current memory snapshot
        var initial = AppLogger.GetRecentLogs();
        _allLogs.AddRange(initial);
        ApplyFilter();

        // Subscribe to live log updates
        AppLogger.LogEntryAdded += OnLogEntryAdded;
        Closed += (_, _) => AppLogger.LogEntryAdded -= OnLogEntryAdded;
    }

    private void OnLogEntryAdded(LogItem item)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _allLogs.Add(item);
            if (MatchesFilter(item))
            {
                var uiModel = new LogItemUiModel(item);
                FilteredLogs.Add(uiModel);
                if (LogListView.Items.Count > 0)
                {
                    LogListView.ScrollIntoView(uiModel);
                }
            }
        });
    }

    private bool MatchesFilter(LogItem item)
    {
        if (_filterLevelIndex == 1 && !item.Level.Equals("INFO", StringComparison.OrdinalIgnoreCase)) return false;
        if (_filterLevelIndex == 2 && !item.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase)) return false;
        if (_filterLevelIndex == 3 && !item.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            return item.Message.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                   item.Level.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private void ApplyFilter()
    {
        FilteredLogs.Clear();
        foreach (var item in _allLogs.Where(MatchesFilter))
        {
            FilteredLogs.Add(new LogItemUiModel(item));
        }

        if (FilteredLogs.Count > 0)
        {
            LogListView.ScrollIntoView(FilteredLogs[^1]);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _filterText = SearchBox.Text.Trim();
        ApplyFilter();
    }

    private void LevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _filterLevelIndex = LevelFilterCombo.SelectedIndex;
        ApplyFilter();
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var sb = new StringBuilder();
        foreach (var log in _allLogs)
        {
            sb.AppendLine(log.FormattedEntry);
        }

        var dp = new DataPackage();
        dp.SetText(sb.ToString());
        Clipboard.SetContent(dp);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        AppLogger.ClearLogs();
        _allLogs.Clear();
        FilteredLogs.Clear();
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        string? path = AppLogger.LogFilePath;
        if (!string.IsNullOrEmpty(path))
        {
            string? dir = Path.GetDirectoryName(path);
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
        }
    }

    public static Brush GetLevelBackground(string level) => level switch
    {
        "ERROR" => new SolidColorBrush(ColorHelper.FromArgb(60, 239, 68, 68)),
        "WARN" => new SolidColorBrush(ColorHelper.FromArgb(60, 250, 204, 21)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(60, 56, 189, 248))
    };

    public static Brush GetLevelForeground(string level) => level switch
    {
        "ERROR" => new SolidColorBrush(ColorHelper.FromArgb(255, 248, 113, 113)),
        "WARN" => new SolidColorBrush(ColorHelper.FromArgb(255, 253, 224, 71)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 125, 211, 252))
    };
}
