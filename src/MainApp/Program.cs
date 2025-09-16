using Avalonia;
using Avalonia.ReactiveUI;
using Serilog;
using Avalonia.Controls.ApplicationLifetimes;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Views;
using CrowsNestMqtt.BusinessLogic.Services;
using System.Timers; // Added for Timer
using System.Runtime; // Added for GCSettings
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

[assembly: InternalsVisibleTo("CrowsNestMqtt.UnitTests")]

namespace CrowsNestMqtt.App;

[ExcludeFromCodeCoverage]
class Program
{
    private static System.Timers.Timer? _gcTimer; // Moved from App.axaml.cs

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
            (var aspireHostname, var aspirePort) = LoadMqttEndpointFromEnv();
            BuildAvaloniaApp(aspireHostname, aspirePort).StartWithClassicDesktopLifetime(args);
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

    public static AppBuilder BuildAvaloniaApp(string? aspireHostname = null, int? aspirePort = null)
    {
        return AppBuilder.Configure<CrowsNestMqtt.UI.App>() // Configure the App from UI project
            .UsePlatformDetect()
            .LogToTrace() // Added for better diagnostics if needed
            .UseReactiveUI()
            .AfterSetup(builder => // Add desktop-specific setup here
            {
                if (builder.Instance?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = new MainViewModel(new CommandParserService(), null, aspireHostname, aspirePort)
                    };

                    if (!string.IsNullOrEmpty(aspireHostname) && aspirePort.HasValue)
                    {
                        (desktop.MainWindow.DataContext as MainViewModel)?.ConnectCommand.Execute().Subscribe();
                    }

                    // Subscribe to the ShutdownRequested event
                    desktop.ShutdownRequested += OnShutdownRequested;

                    // Start GC compaction timer
                    _gcTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
                    _gcTimer.Elapsed += CollectAndCompactHeap;
                    _gcTimer.AutoReset = true;
                    _gcTimer.Enabled = true;
                    Log.Information("Heap compaction timer started.");
                }
            });
    }

    // Moved from App.axaml.cs and made static
    private static void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (sender is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is IDisposable disposableViewModel)
        {
            Log.Information("Shutdown requested. Disposing MainViewModel.");
            disposableViewModel.Dispose();
        }
        else
        {
            Log.Warning("Could not find MainViewModel to dispose during shutdown.");
        }

        // Stop and dispose the timer
        if (_gcTimer != null)
        {
            _gcTimer.Stop();
            _gcTimer.Elapsed -= CollectAndCompactHeap;
            _gcTimer.Dispose();
            _gcTimer = null;
            Log.Information("Heap compaction timer stopped and disposed.");
        }
    }

    // Moved from App.axaml.cs and made static
    private static void CollectAndCompactHeap(object? sender, ElapsedEventArgs e)
    {
        Log.Debug("Triggering GC Collect and Compaction.");
        // Collect generations 0, 1, and 2
        GC.Collect();
        // Wait for finalizers to complete
        GC.WaitForPendingFinalizers();
        // Compact the LOH
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        Log.Debug("GC Collect and Compaction finished.");
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

    private static (string? aspireHostname, int? aspirePort) LoadMqttEndpointFromEnv()
    {
        string? aspireHostname = null;
        int? aspirePort = null;
        const string aspireMqttEnvVar = "services__mqtt__default__0";
        var mqttConnectionString = Environment.GetEnvironmentVariable(aspireMqttEnvVar);

        if (!string.IsNullOrEmpty(mqttConnectionString))
        {
            Log.Information("Found Aspire MQTT connection string from environment variable {EnvVarName}: {ConnectionString}", aspireMqttEnvVar, mqttConnectionString);
            try
            {
                var uri = new Uri(mqttConnectionString);
                if (!string.IsNullOrEmpty(uri.Host) && uri.Port > 0)
                {
                    aspireHostname = uri.Host;
                    aspirePort = uri.Port;
                    Log.Information("Successfully parsed Aspire MQTT configuration. Hostname: {Hostname}, Port: {Port}", aspireHostname, aspirePort);
                }
                else
                {
                    Log.Error("Failed to parse Hostname/Port from Aspire MQTT connection string: {ConnectionString}. Host or Port missing or invalid.", mqttConnectionString);
                }
            }
            catch (UriFormatException ex)
            {
                Log.Error(ex, "Invalid URI format for Aspire MQTT connection string: {ConnectionString}", mqttConnectionString);
            }
        }
        else
        {
            Log.Information("Aspire MQTT environment variable {EnvVarName} not found or empty.", aspireMqttEnvVar);
        }

        return (aspireHostname, aspirePort);
    }
}
