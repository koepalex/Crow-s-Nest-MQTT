using Avalonia;
using Avalonia.Input.Platform; // For IClipboard
using Avalonia.Platform.Storage; // Potentially needed for other storage operations
using Avalonia.Controls; // For TopLevel (if needed later)
using Avalonia.VisualTree; // For GetVisualRoot (if needed later)
using Avalonia.Interactivity; // For RoutedEventArgs (if needed later)
using Avalonia.Threading; // Already present
using ReactiveUI;
using Serilog; // Added Serilog
using System;
using System.Collections.Generic; // Added for List<string>
using System.Collections.ObjectModel;
using System.Linq; // For LINQ operations
using System.Reactive; // Required for Unit
using System.Reactive.Concurrency; // For RxApp.MainThreadScheduler
using System.Reactive.Linq; // Required for Select, ObserveOn, Throttle, DistinctUntilChanged
using System.Text; // For Encoding and StringBuilder
using System.Threading;
using System.Threading.Tasks; // For Task
// using Avalonia.Threading; // Removed duplicate - already on line 7
// using Avalonia.Controls; // No longer needed for ItemsSourceView
using CrowsNestMqtt.BusinessLogic; // Required for MqttEngine, MqttConnectionStateChangedEventArgs
using CrowsNestMqtt.Businesslogic.Commands; // Added for command parsing
using CrowsNestMqtt.Businesslogic.Services; // Added for command parsing
using DynamicData; // Added for SourceList and reactive filtering
using DynamicData.Binding; // Added for Bind()
using FuzzySharp; // Added for fuzzy search
using MQTTnet;
using CrowsNestMqtt.Businesslogic.Exporter; // Required for MqttApplicationMessage, MqttApplicationMessageReceivedEventArgs

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages the different sections of the UI: Topic List, Message History, Message Details, and Command Bar.
/// </summary>
public class MainViewModel : ReactiveObject
{
    private readonly MqttEngine _mqttEngine;
    private readonly ICommandParserService _commandParserService; // Added command parser service
    private Timer? _updateTimer;
    private readonly SynchronizationContext? _syncContext; // To post updates to the UI thread
    private readonly SourceList<MessageViewModel> _messageHistorySource = new(); // Backing source for DynamicData
    private readonly IDisposable _messageHistorySubscription; // To dispose the pipeline
    private readonly ReadOnlyObservableCollection<MessageViewModel> _filteredMessageHistory; // Field for the bound collection
    private string _currentSearchTerm = string.Empty; // Backing field for search term
    private readonly List<string> _availableCommands; // Added list of commands for suggestions

    /// <summary>
    /// Gets or sets the current search term used for filtering message history.
    /// </summary>
    public string CurrentSearchTerm
    {
        get => _currentSearchTerm;
        set => this.RaiseAndSetIfChanged(ref _currentSearchTerm, value);
    }
    // --- Settings ---
    public SettingsViewModel Settings { get; }

    // --- Reactive Properties ---
    private string? _commandText;
    public string? CommandText
    {
        get => _commandText;
        set => this.RaiseAndSetIfChanged(ref _commandText, value);
    }

    // Replaced SelectedTopic with SelectedNode for the TreeView
    private NodeViewModel? _selectedNode;
    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            // Clear search term and load history when a node is selected
            CurrentSearchTerm = string.Empty; // Clear the search term via the public property
            // No need to call LoadMessageHistory here, the filter will react to the change in SelectedNode
        }
    }

    private MessageViewModel? _selectedMessage;
    public MessageViewModel? SelectedMessage
    {
        get => _selectedMessage;
        set => this.RaiseAndSetIfChanged(ref _selectedMessage, value);
    }

    private string _messageDetails = "Select a message to see details.";
    public string MessageDetails
    {
        get => _messageDetails;
        set => this.RaiseAndSetIfChanged(ref _messageDetails, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set => this.RaiseAndSetIfChanged(ref _isConnected, value); // Keep private set
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set => this.RaiseAndSetIfChanged(ref _isPaused, value);
    }

    private bool _isSettingsVisible = false;
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set => this.RaiseAndSetIfChanged(ref _isSettingsVisible, value);
    }

    private string _statusBarText = "Ready"; // Added for status feedback
    public string StatusBarText
    {
        get => _statusBarText;
        set => this.RaiseAndSetIfChanged(ref _statusBarText, value);
    }

    private string _clipboardText = "";
    public string ClipboardText
    {
        get => _clipboardText;
        set => this.RaiseAndSetIfChanged(ref _clipboardText, value);
    }

    // --- JSON Viewer ---
    public JsonViewerViewModel JsonViewer { get; } // Added for JSON display

    private bool _isJsonViewerVisible = false; // Added backing field for visibility
    public bool IsJsonViewerVisible // Added property for visibility binding
    {
        get => _isJsonViewerVisible;
        set => this.RaiseAndSetIfChanged(ref _isJsonViewerVisible, value);
    }

    // --- Collections ---
    // Replaced Topics with TopicTreeNodes for the TreeView
    public ObservableCollection<NodeViewModel> TopicTreeNodes { get; } = new();
    // public ObservableCollection<MessageViewModel> MessageHistory { get; } = new(); // Replaced by SourceList + ReadOnlyObservableCollection

    // --- Views ---
    // Filtered collection bound to the UI
    public ReadOnlyObservableCollection<MessageViewModel> FilteredMessageHistory { get; }

    // --- Suggestions ---
    private ObservableCollection<string> _commandSuggestions = new(); // Added for AutoCompleteBox
    public ObservableCollection<string> CommandSuggestions
    {
        get => _commandSuggestions;
        set => this.RaiseAndSetIfChanged(ref _commandSuggestions, value);
    }

    // --- Commands ---
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseResumeCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; } // Added Settings Command
    public ReactiveCommand<Unit, Unit> SubmitInputCommand { get; } // Added for command/search input


    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// Sets up placeholder data and starts the UI update timer.
    /// </summary>
    // Constructor now requires ICommandParserService
    public MainViewModel(ICommandParserService commandParserService)
    {
        _commandParserService = commandParserService ?? throw new ArgumentNullException(nameof(commandParserService)); // Store injected service
        _syncContext = SynchronizationContext.Current; // Capture sync context
        Settings = new SettingsViewModel(); // Instantiate settings
        JsonViewer = new JsonViewerViewModel(); // Instantiate JSON viewer VM

        // Populate the list of available commands (assuming CommandType enum has all commands)
        _availableCommands = Enum.GetNames(typeof(CommandType))
                                 .Select(name => ":" + name.ToLowerInvariant()) // Prefix with ':' and make lowercase
                                 .ToList();

        // --- DynamicData Pipeline for Message History Filtering ---

        // Define the filter predicate based on the search term
        // Define the filter predicate based on the search term AND the selected node
        var filterPredicate = this.WhenAnyValue(x => x.CurrentSearchTerm, x => x.SelectedNode)
            .Throttle(TimeSpan.FromMilliseconds(250), RxApp.MainThreadScheduler) // Debounce input
            .Select(tuple =>
            {
                var (term, node) = tuple;
                var selectedPath = node?.FullPath;
                // Log when the filter criteria changes
                Log.Verbose("Filter criteria updated. SelectedPath: '{SelectedPath}', Term: '{SearchTerm}'", selectedPath ?? "[None]", term ?? "[Empty]");

                // Return the actual filter function
                return (Func<MessageViewModel, bool>)(message =>
                {
                    string? msgTopic = message.FullMessage?.Topic; // Get topic safely

                    // Condition 1: Topic must match selected path (or no path selected)
                    bool topicMatch = selectedPath == null || msgTopic == selectedPath;

                    // Condition 2: Payload must match search term (or no search term)
                    bool searchTermMatch = string.IsNullOrWhiteSpace(term) ||
                                           (message.PayloadPreview?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

                    // Message passes if both conditions are met
                    bool pass = topicMatch && searchTermMatch;

                    // Consolidated Debug logging just before returning the result
                    if (msgTopic?.StartsWith("process-control-demo/data/process-control-simple-write-and-call") ?? false)
                    {
                        Log.Verbose("Filter Eval: Msg='{MsgTopic}' | Selected='{SelectedPath}' | Term='{SearchTerm}' | TopicMatch={TMatch} | SearchMatch={SMatch} | Result={Pass}",
                                    msgTopic ?? "N/A",
                                    selectedPath ?? "[None]",
                                    term ?? "[Empty]",
                                    topicMatch,
                                    searchTermMatch,
                                    pass);
                    }
                    return pass;
                });
            });

        _messageHistorySubscription = _messageHistorySource.Connect() // Connect to the source
            .Filter(filterPredicate) // Apply the dynamic filter
            .Sort(SortExpressionComparer<MessageViewModel>.Descending(m => m.Timestamp)) // Keep newest messages on top
            .ObserveOn(RxApp.MainThreadScheduler) // Ensure updates are on the UI thread
            .Bind(out _filteredMessageHistory) // Bind the results to the ReadOnlyObservableCollection
            .DisposeMany() // Dispose items when they are removed from the collection
            .Subscribe(_ => { }, ex => Log.Error(ex, "Error in MessageHistory DynamicData pipeline")); // Log errors

        FilteredMessageHistory = _filteredMessageHistory; // Assign the bound collection

        // Create connection settings based on the SettingsViewModel
        var connectionSettings = new MqttConnectionSettings
        {
            Hostname = Settings.Hostname,
            Port = Settings.Port,
            ClientId = Settings.ClientId,
            KeepAliveInterval = Settings.KeepAliveInterval,
            CleanSession = Settings.CleanSession,
            SessionExpiryInterval = Settings.SessionExpiryInterval
            // TODO: Map other settings like TLS, Credentials if added
        };

        _mqttEngine = new MqttEngine(connectionSettings); // Pass connection settings

        // Subscribe to MQTT Engine events
        _mqttEngine.ConnectionStateChanged += OnConnectionStateChanged;
        _mqttEngine.MessageReceived += OnMessageReceived;
        _mqttEngine.LogMessage += OnLogMessage; // Optional: Log engine messages

        // --- Command Implementations ---
        // Rebuild connection settings before connecting if they might change after initial setup
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync); // Connect uses current settings via MqttEngine
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, this.WhenAnyValue(x => x.IsConnected));
        ClearHistoryCommand = ReactiveCommand.Create(ClearHistory);
        PauseResumeCommand = ReactiveCommand.Create(TogglePause);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings); // Initialize Settings Command
        SubmitInputCommand = ReactiveCommand.Create(ExecuteSubmitInput, this.WhenAnyValue(x => x.CommandText).Select(txt => !string.IsNullOrWhiteSpace(txt))); // Enable only when text exists

        // --- Property Change Reactions ---

        // When SelectedMessage changes, update the MessageDetails
        this.WhenAnyValue(x => x.SelectedMessage)
           .ObserveOn(RxApp.MainThreadScheduler)
           .Subscribe(selected => UpdateMessageDetails(selected));

        // When CommandText changes, update the CommandSuggestions
        this.WhenAnyValue(x => x.CommandText)
            .Throttle(TimeSpan.FromMilliseconds(150), RxApp.MainThreadScheduler) // Small debounce
            .DistinctUntilChanged() // Only update if text actually changed
            .ObserveOn(RxApp.MainThreadScheduler) // Ensure UI update is on the correct thread
            .Subscribe(text => UpdateCommandSuggestions(text));
    }

    private void OnLogMessage(object? sender, string log)
    {
        // TODO: Implement proper logging (e.g., to a log panel or file)
        Log.Debug("[MQTT Engine]: {LogMessage}", log);
    }

    // --- MQTT Event Handlers ---

    private void OnConnectionStateChanged(object? sender, MqttConnectionStateChangedEventArgs e)
    {
        // Ensure UI updates happen on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = e.IsConnected;
            if (e.IsConnected)
            {
                Log.Information("MQTT Client Connected. Starting UI Timer.");
                StartTimer(TimeSpan.FromSeconds(1)); // Start timer with 1-second interval
                LoadInitialTopics(); // Load any topics already buffered before connection event
            }
            else
            {
                Log.Warning(e.Error, "MQTT Client Disconnected. Stopping UI Timer."); // Removed Reason part, Error is logged if present
                StopTimer();
                // Optionally clear UI state or show disconnected status
            }
        });
    }

    private void OnMessageReceived(object? sender, MqttApplicationMessageReceivedEventArgs e)
    {
        if (IsPaused) return; // Don't update UI if paused

        // Ensure UI updates happen on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            var topic = e.ApplicationMessage.Topic;
            UpdateOrCreateNode(topic); // Update tree structure and counts

            // Always add incoming messages to the source list. The DynamicData pipeline will filter it.
            // Create and add the ViewModel directly to the source list
            // Basic payload preview (handle potential null/empty payload)
            string preview = e.ApplicationMessage.Payload.Length > 0
                ? Encoding.UTF8.GetString(e.ApplicationMessage.Payload) // Use overload for ReadOnlySequence<byte>
                : "[No Payload]";

            // Limit preview length
            const int maxPreviewLength = 100;
            if (preview.Length > maxPreviewLength)
            {
                preview = preview.Substring(0, maxPreviewLength) + "...";
            }

            var messageVm = new MessageViewModel
            {
                Timestamp = DateTime.Now, // Use arrival time
                PayloadPreview = preview.Replace(Environment.NewLine, " "), // Remove newlines for preview
                FullMessage = e.ApplicationMessage // Store the original message
            };
            _messageHistorySource.Add(messageVm); // Add to the source list
            Log.Verbose("Added message for topic '{Topic}'. Source count: {Count}", topic, _messageHistorySource.Count); // Log source count

            // Optional: Limit total history size in the source list
            const int maxHistoryDisplay = 5000; // Consider a larger limit for the total buffer
            while (_messageHistorySource.Count > maxHistoryDisplay)
            {
                _messageHistorySource.RemoveAt(0); // Remove oldest from the global buffer
            }
        });
    }

    // --- UI Update Logic ---

    private void LoadInitialTopics()
    {
        var bufferedTopics = _mqttEngine.GetBufferedTopics();
        foreach (var topicName in bufferedTopics)
        {
            UpdateOrCreateNode(topicName, incrementCount: false); // Populate tree without incrementing count initially
        }
    }


    // Removed LoadMessageHistory method as it's no longer needed.
    // The DynamicData pipeline handles filtering based on SelectedNode.
    private void UpdateMessageDetails(MessageViewModel? messageVm)
    {
        if (messageVm?.FullMessage == null)
        {
            MessageDetails = "Select a message to see details.";
            JsonViewer.LoadJson(string.Empty); // Clear JSON viewer content by loading empty string
            IsJsonViewerVisible = false; // Hide JSON viewer
            return;
        }

        var msg = messageVm.FullMessage;
        var sb = new StringBuilder();
        sb.AppendLine($"Timestamp: {messageVm.Timestamp:yyyy-MM-dd HH:mm:ss.fff}"); 
        sb.AppendLine($"Topic: {msg.Topic}");
        sb.AppendLine($"Response Topic: {msg.ResponseTopic}");
        sb.AppendLine($"QoS: {msg.QualityOfServiceLevel}");
        sb.AppendLine($"Message Expiry Interval: {msg.MessageExpiryInterval}");
        sb.AppendLine($"Correlation Data: {msg.CorrelationData}");
        sb.AppendLine($"Payload Format: {msg.PayloadFormatIndicator}");
        sb.AppendLine($"Content Type: {msg.ContentType ?? "N/A"}");
        sb.AppendLine($"Retain: {msg.Retain}");

        // Add User Properties if they exist
        if (msg.UserProperties != null && msg.UserProperties.Count > 0)
        {
            sb.AppendLine("\n--- User Properties ---");
            foreach (var prop in msg.UserProperties)
            {
                sb.AppendLine($"{prop.Name}: {prop.Value}");
            }
        }

        sb.AppendLine("\n--- Payload ---");
        string payloadText = "[No Payload]"; // Default

        // Attempt to decode payload as UTF-8 text
        try
        {
            if (msg.Payload.Length > 0)
            {
                payloadText = Encoding.UTF8.GetString(msg.Payload); // Use overload for ReadOnlySequence<byte>
                sb.AppendLine(payloadText);

                // Attempt to parse the decoded text as JSON using LoadJson
                JsonViewer.LoadJson(payloadText);
                IsJsonViewerVisible = true; // Show the viewer area
            }
            else
            {
                sb.AppendLine(payloadText); // Append "[No Payload]"
                JsonViewer.LoadJson(string.Empty); // Clear JSON viewer if no payload
                IsJsonViewerVisible = false; // Hide viewer if no payload
            }
        }
        catch (Exception ex) // Catch potential UTF-8 decoding errors
        {
            sb.AppendLine($"[Could not decode payload as UTF-8: {ex.Message}]");
            JsonViewer.LoadJson(string.Empty); // Clear viewer on decode error
            IsJsonViewerVisible = false; // Hide viewer on decode error
        }

        MessageDetails = sb.ToString();
    }


    // --- Timer Logic ---

    public void StartTimer(TimeSpan interval)
    {
        StopTimer(); // Ensure previous timer is stopped
        Log.Debug("Starting UI update timer with interval {Interval}.", interval);
        _updateTimer = new Timer(UpdateTick, null, interval, interval);
    }


    /// <summary>
    /// Stops the UI update timer.
    /// </summary>
    public void StopTimer()
    {
        Log.Debug("Stopping UI update timer.");
        _updateTimer?.Dispose();
        _updateTimer = null;
    }


    /// <summary>
    /// Called by the timer on each tick to update UI elements.
    /// </summary>
    /// <param name="state">State object (not used).</param>
    private void UpdateTick(object? state)
    {
        // This runs on a background thread pool thread.
        // Use Dispatcher for UI updates if needed.
    }

    // --- Command Methods ---

    private async Task ConnectAsync()
    {
        Log.Information("Connect command executed.");
        // Rebuild connection settings from ViewModel just before connecting
        var connectionSettings = new MqttConnectionSettings
        {
            Hostname = Settings.Hostname,
            Port = Settings.Port,
            ClientId = Settings.ClientId,
            KeepAliveInterval = Settings.KeepAliveInterval,
            CleanSession = Settings.CleanSession,
            SessionExpiryInterval = Settings.SessionExpiryInterval
        };
        // Update the engine with the latest settings before connecting
        _mqttEngine.UpdateSettings(connectionSettings);
        await _mqttEngine.ConnectAsync(); // Now uses the updated settings
    }

    private async Task DisconnectAsync()
    {
        Log.Information("Disconnect command executed.");
        await _mqttEngine.DisconnectAsync();
    }

    private void ClearHistory()
    {
        Log.Information("Clear history command executed.");
        // Update ClearHistory to potentially use SelectedNode if needed,
        // or clear based on a different criteria. For now, clearing based on selected node path.
        if (SelectedNode != null)
        {
            // Optionally clear buffer in engine too?
            // _mqttEngine.ClearBufferForTopic(SelectedNode.FullPath);
            _messageHistorySource.Clear(); // Clear the source list
                                           // MessageHistory.Clear(); // Removed - _messageHistorySource.Clear() handles it
            SelectedMessage = null;
            StatusBarText = $"History cleared for {SelectedNode.FullPath}."; // Update status
        }
        else
        {
            StatusBarText = "Select a topic node to clear its history."; // Update status
        }
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
        Log.Information("UI Updates Paused: {IsPaused}", IsPaused);
        StatusBarText = IsPaused ? "Updates Paused" : "Updates Resumed"; // Update status
    }

    private void OpenSettings()
    {
        IsSettingsVisible = !IsSettingsVisible; // Toggle the visibility
        Log.Information("Settings Visible: {IsSettingsVisible}", IsSettingsVisible);
    }

    private void ExecuteSubmitInput()
    {
        string currentInput = CommandText ?? string.Empty; // Capture text before potential clear

        if (string.IsNullOrWhiteSpace(currentInput))
        {
            // If user submits empty text, clear the filter
            CurrentSearchTerm = string.Empty; // Use property setter
            Log.Information("Clearing search filter due to empty input.");
            Dispatcher.UIThread.Post(() =>
            {
                // No need to refresh, DynamicData handles updates
                StatusBarText = "Filter cleared.";
            });
            CommandText = string.Empty; // Clear the input box as well
            return;
        }

        var result = _commandParserService.ParseInput(currentInput, Settings.Into());

        if (result.IsSuccess)
        {
            if (result.ParsedCommand != null)
            {
                // Successfully parsed a command
                StatusBarText = $"Executing command: {result.ParsedCommand.Type}...";
                Log.Information("Parsed command: {CommandType} with args: {Arguments}", result.ParsedCommand.Type, string.Join(", ", result.ParsedCommand.Arguments));
                DispatchCommand(result.ParsedCommand); // Call helper to handle command execution
                CommandText = string.Empty; // Clear input on successful command execution
            }
            else if (result.SearchTerm != null)
            {
                // Treat as search term
                CurrentSearchTerm = result.SearchTerm; // Store the search term via property setter
                Log.Information("Applying search filter: '{SearchTerm}'", _currentSearchTerm);

                // Ensure refresh happens on UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    // No need to refresh, DynamicData handles updates
                    StatusBarText = $"Filter applied: '{CurrentSearchTerm}'. {FilteredMessageHistory.Count} results."; // Show result count from filtered list
                });
                // Do not clear CommandText for search, allow refinement
            }
        }
        else
        {
            // Parsing failed
            StatusBarText = $"Error: {result.ErrorMessage}";
            Log.Warning("Command parsing error: {ErrorMessage}", result.ErrorMessage);
            // Do not clear CommandText, allow user to fix
        }
    }

    private void DispatchCommand(ParsedCommand command)
    {
        Log.Debug("Dispatching command {CommandType}", command.Type);
        try
        {
            switch (command.Type)
            {
                case CommandType.Connect:
                    if (command.Arguments.Count != 1)
                    {
                        StatusBarText = "Error: :connect requires exactly one argument: <server_address:port>";
                        Log.Warning("Invalid arguments for :connect command.");
                        break;
                    }
                    // Parse server:port
                    var parts = command.Arguments[0].Split(':');
                    if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !int.TryParse(parts[1], out int port) || port < 1 || port > 65535)
                    {
                        StatusBarText = $"Error: Invalid format for :connect argument '{command.Arguments[0]}'. Expected: <server_address:port>";
                        Log.Warning("Invalid format for :connect argument: {Argument}", command.Arguments[0]);
                        break;
                    }
                    string host = parts[0];

                    // Update settings before connecting
                    Settings.Hostname = host;
                    Settings.Port = port;
                    StatusBarText = $"Attempting to connect to {host}:{port}...";
                    ConnectCommand.Execute().Subscribe(
                        _ => StatusBarText = $"Successfully initiated connection to {host}:{port}.", // Success here means command executed, not necessarily connected yet
                        ex =>
                        {
                            StatusBarText = $"Error initiating connection: {ex.Message}";
                            Log.Error(ex, "Error executing ConnectCommand");
                        });
                    break;

                case CommandType.Disconnect:
                    StatusBarText = "Disconnecting...";
                    DisconnectCommand.Execute().Subscribe(
                         _ => StatusBarText = "Successfully initiated disconnection.",
                         ex =>
                         {
                             StatusBarText = $"Error initiating disconnection: {ex.Message}";
                             Log.Error(ex, "Error executing DisconnectCommand");
                         });
                    break;

                case CommandType.Clear: 
                    StatusBarText = "Clearing history...";
                    ClearHistoryCommand.Execute().Subscribe(); // Execute the existing clear command
                    break;
                case CommandType.Copy:
                    if (SelectedMessage?.FullMessage != null)
                    {
                        var msg = SelectedMessage.FullMessage;
                        var textExporter = new TextExporter();

                        ClipboardText = textExporter.GenerateDetailedTextFromMessage(msg, SelectedMessage.Timestamp);

                        StatusBarText = "Updated system clipboard with selected message";
                        Log.Information("Copy command executed.");
                    }
                    else
                    {
                        StatusBarText = "No message selected to copy.";
                        Log.Information("Copy command executed but no message was selected.");
                    }
                    break; // Correct placement outside the if/else block
                case CommandType.Help:
                    // TODO: Implement a more sophisticated help system (e.g., show available commands)
                    StatusBarText = "Available commands: :connect, :disconnect, :export, :filter, :copy, :clear, :help, :pause, :resume"; 
                    Log.Information("Displaying help information.");
                    break; 
                case CommandType.Pause:
                    TogglePause();
                    break;
                case CommandType.Resume:
                    TogglePause();
                    break;
                case CommandType.Export:
                    // Expected arguments: :export <format> <folder_path> [message_index] (index optional for now)
                    // Example: :export json C:\temp\mqtt_exports
                    // Example: :export text /home/user/mqtt_logs
                    if (SelectedMessage?.FullMessage == null)
                    {
                        StatusBarText = "Error: No message selected to export.";
                        Log.Warning("Export command failed: No message selected.");
                        break;
                    }

                    if (command.Arguments.Count < 2)
                    {
                        StatusBarText = "Error: :export requires at least 2 arguments: <format> <folder_path>";
                        Log.Warning("Invalid arguments for :export command. Expected format and folder path.");
                        break;
                    }

                    string format = command.Arguments[0].ToLowerInvariant();
                    string folderPath = command.Arguments[1];

                    IMessageExporter? exporter = null;
                    if (format == "json")
                    {
                        exporter = new JsonExporter();
                    }
                    else if (format == "text")
                    {
                        exporter = new TextExporter();
                    }
                    else
                    {
                        StatusBarText = $"Error: Invalid export format '{format}'. Use 'json' or 'text'.";
                        Log.Warning("Invalid export format specified: {Format}", format);
                        break;
                    }

                    try
                    {
                        // Use the timestamp from the ViewModel as it represents arrival time
                        string? exportedFilePath = exporter.ExportToFile(SelectedMessage.FullMessage, SelectedMessage.Timestamp, folderPath);

                        if (exportedFilePath != null)
                        {
                            StatusBarText = $"Successfully exported message to: {exportedFilePath}";
                            ClipboardText = exportedFilePath;
                            Log.Information("Export command successful. File: {FilePath}", exportedFilePath);
                        }
                        else
                        {
                            // ExportToFile logs warnings/errors internally, just provide general feedback
                            StatusBarText = "Export failed or was skipped. Check logs for details.";
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusBarText = $"Error during export: {ex.Message}";
                        Log.Error(ex, "Exception during export command execution.");
                    }
                    break;
                case CommandType.Filter:
                    break;
                case CommandType.Search:
                    break;
                default:
                    StatusBarText = $"Error: Unknown command type '{command.Type}'.";
                    Log.Warning("Unknown command type encountered: {CommandType}", command.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusBarText = $"Error executing command: {ex.Message}";
            Log.Error(ex, "Exception during command dispatch for {CommandType}", command.Type);
        }
    }

    // FilterMessages method removed - filtering handled by DynamicData pipeline

    // --- Topic Tree Management ---

    /// <summary>
    /// Updates or creates a node in the topic tree structure based on the full topic path.
    /// Handles splitting the topic by '/' and creating/updating nodes recursively.
    /// </summary>
    /// <param name="topic">The full MQTT topic string.</param>
    /// <param name="incrementCount">Whether to increment the message count for the final node.</param>
    private void UpdateOrCreateNode(string topic, bool incrementCount = true)
    {
        var parts = topic.Split('/');
        ObservableCollection<NodeViewModel> currentLevel = TopicTreeNodes;
        NodeViewModel? parentNode = null; // Keep track of the parent for full path construction

        string currentPath = ""; // Build the path segment by segment

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part)) continue; // Skip empty parts (e.g., leading/trailing slashes)

            // Build the path for the current node
            currentPath = (i == 0) ? part : $"{currentPath}/{part}";

            var existingNode = currentLevel.FirstOrDefault(n => n.Name == part);

            if (existingNode == null)
            {
                // Create new node
                // Create new node
                existingNode = new NodeViewModel(part, parentNode) { FullPath = currentPath }; // Pass parent and set full path

                // Insert the new node in sorted order instead of rebuilding the collection
                int insertIndex = 0;
                while (insertIndex < currentLevel.Count && string.Compare(currentLevel[insertIndex].Name, existingNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertIndex++;
                }
                currentLevel.Insert(insertIndex, existingNode);
            }

            // Increment count only for the final node in the path if requested
            if (i == parts.Length - 1 && incrementCount)
            {
                existingNode.IncrementMessageCount();
            }

            // Move to the next level
            currentLevel = existingNode.Children;
            parentNode = existingNode; // Update parent for the next iteration
        }
    }

    // --- Suggestion Logic ---

    /// <summary>
    /// Updates the CommandSuggestions collection based on the current CommandText.
    /// </summary>
    /// <param name="currentText">The current text in the command input box.</param>
    private void UpdateCommandSuggestions(string? currentText)
    {
        CommandSuggestions.Clear(); // Clear previous suggestions

        if (string.IsNullOrWhiteSpace(currentText) || !currentText.StartsWith(":"))
        {
            // If text is empty or doesn't start with ':', show no suggestions
            return;
        }

        // Filter available commands based on the input (case-insensitive)
        var matchingCommands = _availableCommands
            .Where(cmd => cmd.StartsWith(currentText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(cmd => cmd); // Optional: Sort suggestions alphabetically

        foreach (var cmd in matchingCommands)
        {
            CommandSuggestions.Add(cmd);
        }
        Log.Verbose("Updated command suggestions for '{InputText}'. Found {Count} matches.", currentText, CommandSuggestions.Count);
    }
}
