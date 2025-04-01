using ReactiveUI; // Required for RaiseAndSetIfChanged
using System.Collections.ObjectModel; // Required for ObservableCollection
using System.Threading; // Required for Timer
using System; // Required for Timer and Action

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages the different sections of the UI: Topic List, Message History, Message Details, and Command Bar.
/// </summary>
public class MainViewModel : ReactiveViewModel
{
    private string? _commandText;
    private Timer? _updateTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// Sets up placeholder data and starts the UI update timer.
    /// </summary>
    public MainViewModel()
    {
        // Placeholder data - replace with actual ViewModels later
        Topics = new ObservableCollection<string> { "topic/one", "topic/two/sub", "topic/three" };
        MessageHistory = new ObservableCollection<string> { "Message 1 received", "Message 2 received", "Message 3 received" };
        MessageDetails = "Select a message to see details.";

        // Setup timer for interval-based updates (e.g., every 1 second)
        // StartTimer(); // Start timer when needed, perhaps after connection?
    }

    /// <summary>
    /// Gets the list of topics (placeholder).
    /// </summary>
    public ObservableCollection<string> Topics { get; }

    /// <summary>
    /// Gets the message history for the selected topic (placeholder).
    /// </summary>
    public ObservableCollection<string> MessageHistory { get; }

    /// <summary>
    /// Gets or sets the details of the selected message (placeholder).
    /// </summary>
    public string MessageDetails { get; set; } // Consider making this a dedicated ViewModel

    /// <summary>
    /// Gets or sets the text entered in the command/search bar.
    /// </summary>
    public string? CommandText
    {
        get => _commandText;
        set => this.RaiseAndSetIfChanged(ref _commandText, value);
    }

    /// <summary>
    /// Starts the UI update timer.
    /// </summary>
    public void StartTimer()
    {
        // Dispose existing timer if any
        _updateTimer?.Dispose();

        // Create a timer that ticks every second
        _updateTimer = new Timer(UpdateTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Stops the UI update timer.
    /// </summary>
    public void StopTimer()
    {
        _updateTimer?.Dispose();
        _updateTimer = null;
    }

    /// <summary>
    /// Called by the timer on each tick to update UI elements.
    /// </summary>
    /// <param name="state">State object (not used).</param>
    private void UpdateTick(object? state)
    {
        // This method will be called on a background thread.
        // Dispatch UI updates to the UI thread if necessary.
        // For now, just a placeholder.
        // Example: Update message counters, check buffer status, etc.
        Console.WriteLine("UI Update Tick"); // Placeholder action

        // If updating Avalonia UI elements directly, ensure it's done on the UI thread:
        // Avalonia.Threading.Dispatcher.UIThread.Post(() => { /* UI update code here */ });
    }

    // Consider adding Commands for actions like executing the command text,
    // selecting topics, etc., using ReactiveCommand.
}