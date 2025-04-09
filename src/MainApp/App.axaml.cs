using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrowsNestMqtt.UI.ViewModels; // Added for MainViewModel
using CrowsNestMqtt.UI.Views; // Assuming MainWindow is now in App namespace or adjust if needed
using CrowsNestMqtt.Businesslogic.Services;
using Avalonia.Controls;
using ReactiveUI; // Added for CommandParserService
using System; // Added for IDisposable
using System.Runtime; // Added for GCSettings
using System.Timers; // Added for Timer
namespace CrowsNestMqtt.App;

public partial class App : Application
{
    private System.Timers.Timer? _gcTimer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                // Set the DataContext for the MainWindow
                // Instantiate the service and inject it into the ViewModel
                DataContext = new MainViewModel(new CommandParserService())
            };

            // Subscribe to the ShutdownRequested event to dispose the ViewModel
            desktop.ShutdownRequested += OnShutdownRequested;

            // Start GC compaction timer
            _gcTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            _gcTimer.Elapsed += CollectAndCompactHeap;
            _gcTimer.AutoReset = true;
            _gcTimer.Enabled = true;
            Serilog.Log.Information("Heap compaction timer started.");
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (sender is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is IDisposable disposableViewModel)
        {
            Serilog.Log.Information("Shutdown requested. Disposing MainViewModel.");
            disposableViewModel.Dispose();
        }
        else
        {
            Serilog.Log.Warning("Could not find MainViewModel to dispose during shutdown.");
        }

        // Stop and dispose the timer
        if (_gcTimer != null)
        {
            _gcTimer.Stop();
            _gcTimer.Elapsed -= CollectAndCompactHeap;
            _gcTimer.Dispose();
            _gcTimer = null;
            Serilog.Log.Information("Heap compaction timer stopped and disposed.");
        }
    }

    private void CollectAndCompactHeap(object? sender, ElapsedEventArgs e)
    {
        Serilog.Log.Debug("Triggering GC Collect and Compaction.");
        // Collect generations 0, 1, and 2
        GC.Collect();
        // Wait for finalizers to complete
        GC.WaitForPendingFinalizers();
        // Compact the LOH
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        Serilog.Log.Debug("GC Collect and Compaction finished.");
    }
}
