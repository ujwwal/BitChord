using System.Text;

namespace BitChord.Core;

public static class AppLogger
{
    private static readonly object _lock = new();
    private static string _logFilePath = string.Empty;

    public static string LogFilePath => _logFilePath;

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
        string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
        
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
                // Silently ignore file write failures to avoid logging recursion
            }
        }
    }
}
