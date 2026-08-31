using BitChord.Core;
using Microsoft.UI.Xaml;

namespace BitChord.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        AppLogger.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Error("FATAL: AppDomain UnhandledException", ex);
            }
            else
            {
                AppLogger.Error($"FATAL: AppDomain UnhandledException: {e.ExceptionObject}");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            AppLogger.Error("UNOBSERVED TASK EXCEPTION", e.Exception);
            e.SetObserved();
        };

        UnhandledException += (s, e) =>
        {
            AppLogger.Error($"XAML UnhandledException: {e.Message}", e.Exception);
            e.Handled = true;
        };

        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppLogger.Info("Launching MainWindow...");
        try
        {
            _window = new MainWindow();
            _window.Activate();
            AppLogger.Info("MainWindow activated successfully.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to launch MainWindow", ex);
            throw;
        }
    }
}
