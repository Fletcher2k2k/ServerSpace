using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ServerSpace.UI.Localization;

namespace ServerSpace.UI;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationManager.ApplySavedLanguage(this);
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ExceptionLogger.Log(e.Exception);
        e.Handled = true;
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ExceptionLogger.Log(exception);
        }
        else
        {
            ExceptionLogger.Log(new Exception(e.ExceptionObject?.ToString() ?? "Unbekannte Ausnahme"));
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ExceptionLogger.Log(e.Exception);
        e.SetObserved();
    }
}

internal static class ExceptionLogger
{
    private static readonly object SyncRoot = new();

    public static void Log(Exception exception)
    {
        try
        {
            StringBuilder entry = new();
            entry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
            entry.AppendLine($"Type: {exception.GetType().FullName}");
            entry.AppendLine($"Message: {exception.Message}");
            entry.AppendLine($"StackTrace: {exception.StackTrace}");
            entry.AppendLine($"InnerException: {exception.InnerException}");
            entry.AppendLine();

            lock (SyncRoot)
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "ServerSpace.log"), entry.ToString());
            }
        }
        catch
        {
            // Logging must never cause a second unhandled exception.
        }
    }
}
