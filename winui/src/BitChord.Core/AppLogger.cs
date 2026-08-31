using System.Collections.Concurrent;
using System.Text;

namespace BitChord.Core;

public record LogItem(DateTime Timestamp, string Level, string Message)
{
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");
    public string FormattedEntry => $"[{FormattedTimestamp}] [{Level,-5}] {Message}";
}

public static class AppLogger
{
    private static readonly object _lock = new();
    private static string _logFilePath = string.Empty;
    private static readonly ConcurrentQueue<LogItem> _recentEntries = new();
    private const int MaxInMemoryLogs = 1000;

    public static string LogFilePath => _logFilePath;
    public static event Action<LogItem>? LogEntryAdded;

    public static IReadOnlyList<LogItem> GetRecentLogs() => _recentEntries.ToArray();

    public static void Initialize(string? customLogDir = null)
    {
        try
        {
            string dir = customLogDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BitChord",
                "logs");

            Directory.CreateDirectory(dir);
            _logFilePath = Path.Combine(dir, "bitchord.log");

            Info("==================================================");
            Info($"BitChord Logging Initialized at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Info($"Log file: {_logFilePath}");
            Info($"OS: {Environment.OSVersion}, 64-bit: {Environment.Is64BitProcess}");
            Info("==================================================");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize AppLogger: {ex.Message}");
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var sb = new StringBuilder(message);
        if (ex is not null)
        {
            sb.AppendLine().Append($"Exception: {ex.GetType().FullName}: {ex.Message}").AppendLine().Append(ex.StackTrace);
            if (ex.InnerException is not null)
            {
                sb.AppendLine().Append($"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}").AppendLine().Append(ex.InnerException.StackTrace);
            }
        }
        Log("ERROR", sb.ToString());
    }

    private static void Log(string level, string message)
    {
        var item = new LogItem(DateTime.Now, level, message);
        _recentEntries.Enqueue(item);
        while (_recentEntries.Count > MaxInMemoryLogs && _recentEntries.TryDequeue(out _)) { }

        try
        {
            LogEntryAdded?.Invoke(item);
        }
        catch
        {
            // Ignore subscriber errors
        }

        string entry = $"[{item.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
        Console.WriteLine(entry);

        if (string.IsNullOrEmpty(_logFilePath)) return;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, entry + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Silently ignore file write failures
            }
        }
    }

    public static void ClearLogs()
    {
        while (_recentEntries.TryDequeue(out _)) { }
        if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
        {
            try
            {
                File.WriteAllText(_logFilePath, string.Empty);
            }
            catch { }
        }
    }
}
