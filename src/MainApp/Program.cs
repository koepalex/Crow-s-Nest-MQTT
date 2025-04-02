using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;

namespace CrowsNestMqtt.App;

class Program
{
    private static readonly string _settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrowsNestMqtt",
        "logs",
        "app-.log");

    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(_settingsFilePath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Register global exception handlers
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            Log.Information("Starting application");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI();
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception occurred.");
        e.SetObserved(); // Mark the exception as observed to prevent process termination
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex, "Unhandled domain exception occurred. IsTerminating: {IsTerminating}", e.IsTerminating);
        // Ensure logs are written before termination, especially if IsTerminating is true
        if (e.IsTerminating)
        {
             Log.CloseAndFlush();
        }
        // Depending on the application's needs, you might want to exit explicitly
        // if (e.IsTerminating) { Environment.Exit(1); }
    }
}
