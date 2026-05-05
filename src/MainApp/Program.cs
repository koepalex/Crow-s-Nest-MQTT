using Avalonia;
using ReactiveUI.Avalonia;
using Serilog;
using Avalonia.Controls.ApplicationLifetimes;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Views;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.BusinessLogic.Contracts; // Added for IMessageCorrelationService
using CrowsNestMqtt.BusinessLogic.Configuration; // Added for EnvironmentSettingsOverrides
using CrowsNestMqtt.UI.Services; // Added for ResponseIconService
using CrowsNestMqtt.UI.Contracts; // Added for IResponseIconService
using Microsoft.Extensions.Logging;
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

    private static readonly string _samplesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrowsNestMqtt",
        "samples");

    private const string SamplesReadmeContents =
        "Drop files in this folder to use them as payloads via the :publish command.\r\n" +
        "\r\n" +
        "Example:\r\n" +
        "  :publish my/topic @sample.json\r\n" +
        "\r\n" +
        "Autocomplete in the command palette will suggest files from this folder.\r\n" +
        "Absolute paths (e.g. @\"S:\\data\\payload.json\") are supported too, but are\r\n" +
        "not auto-completed.\r\n";

    private static void EnsureSamplesDirectory()
    {
        try
        {
            Directory.CreateDirectory(_samplesDirectory);
            var readme = Path.Combine(_samplesDirectory, "README.txt");
            if (!File.Exists(readme))
            {
                File.WriteAllText(readme, SamplesReadmeContents);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize samples directory at {Path}", _samplesDirectory);
        }
    }

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
            var environmentOverrides = EnvironmentSettingsOverrides.Load();
            BuildAvaloniaApp(environmentOverrides).StartWithClassicDesktopLifetime(args);
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

    public static AppBuilder BuildAvaloniaApp(EnvironmentSettingsOverrides? environmentOverrides = null)
    {
        return AppBuilder.Configure<CrowsNestMqtt.UI.App>() // Configure the App from UI project
            .UsePlatformDetect()
            .LogToTrace() // Added for better diagnostics if needed
            .UseReactiveUI(_ => { })
            .AfterSetup(builder => // Add desktop-specific setup here
            {
                if (builder.Instance?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    // Create services for dependency injection
                    var commandParserService = new CommandParserService();
                    var publishHistoryService = new PublishHistoryService();
                    EnsureSamplesDirectory();
                    SyntaxHighlightingPatcher.ApplyDarkThemePatches();
                    var fileAutoCompleteService = new FileAutoCompleteService(_samplesDirectory);

                    // Services will be created in MainViewModel after MqttService is available
                    IDeleteTopicService? deleteTopicService = null;
                    IMessageCorrelationService? correlationService = new MessageCorrelationService();

                    // Navigation service requires additional dependencies that aren't available at startup
                    // The icon service can work without navigation for now (icon display and status tracking)
                    IResponseNavigationService? navigationService = null;
                    IResponseIconService? iconService = new ResponseIconService(correlationService, navigationService!);

                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = new MainViewModel(commandParserService, null, deleteTopicService, correlationService, iconService, environmentOverrides, publishHistoryService: publishHistoryService, fileAutoCompleteService: fileAutoCompleteService)
                    };

                    if (environmentOverrides?.IsAspireEnvironment == true)
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

}
