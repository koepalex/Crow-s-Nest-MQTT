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
using SharpHook; // Added SharpHook
using SharpHook.Native; // Added SharpHook Native for KeyCode and ModifierMask
using SharpHook.Reactive; // Added SharpHook Reactive
using MQTTnet.Protocol; // For MqttQualityOfServiceLevel

namespace CrowsNestMqtt.UI.ViewModels;

// Simple record for DataGrid items
public record MetadataItem(string Key, string Value);

/// <summary>
/// ViewModel for the main application window.
/// Manages the different sections of the UI: Topic List, Message History, Message Details, and Command Bar.
/// </summary>
public class MainViewModel : ReactiveObject, IDisposable // Implement IDisposable
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
    private readonly IReactiveGlobalHook _globalHook; // Added SharpHook global hook
    private readonly IDisposable _globalHookSubscription; // Added subscription for the hook
    private bool _disposedValue; // For IDisposable pattern

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

    // Removed MessageDetails property, replaced by MessageMetadata and MessageUserProperties

    // Properties for DataGrids
    private ObservableCollection<MetadataItem> _messageMetadata = new();
    public ObservableCollection<MetadataItem> MessageMetadata
    {
        get => _messageMetadata;
        set => this.RaiseAndSetIfChanged(ref _messageMetadata, value);
    }

    private ObservableCollection<MetadataItem> _messageUserProperties = new();
    public ObservableCollection<MetadataItem> MessageUserProperties
    {
        get => _messageUserProperties;
        set => this.RaiseAndSetIfChanged(ref _messageUserProperties, value);
    }

    private bool _hasUserProperties;
    public bool HasUserProperties
    {
        get => _hasUserProperties;
        set => this.RaiseAndSetIfChanged(ref _hasUserProperties, value);
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
    public ReactiveCommand<Unit, Unit> FocusCommandBarCommand { get; } // Added command to trigger focus


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
        FocusCommandBarCommand = ReactiveCommand.Create(() => { Log.Debug("FocusCommandBarCommand executed by global hook."); /* Actual focus happens in View code-behind */ });
    
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
    
            // --- Global Hook Setup ---
            _globalHook = new SimpleReactiveGlobalHook();
            _globalHookSubscription = _globalHook.KeyPressed
                .Do(e => Log.Debug("SharpHook KeyPressed Received: Key={Key}, Modifiers={Modifiers}", e.Data.KeyCode, e.RawEvent.Mask)) // Log every key press
                .Where(e =>
                {
                    // Check for either Left or Right Ctrl/Shift explicitly
                    bool ctrl = e.RawEvent.Mask.HasFlag(ModifierMask.LeftCtrl) || e.RawEvent.Mask.HasFlag(ModifierMask.RightCtrl);
                    bool shift = e.RawEvent.Mask.HasFlag(ModifierMask.LeftShift) || e.RawEvent.Mask.HasFlag(ModifierMask.RightShift);
                    bool pKey = e.Data.KeyCode == KeyCode.VcP;
                    bool match = ctrl && shift && pKey;
                    if (match) Log.Debug("Ctrl+Shift+P MATCHED inside Where filter."); // Log specifically on match
                    // else Log.Verbose("Keypress did not match Ctrl+Shift+P: Ctrl={Ctrl}, Shift={Shift}, Key={Key}", ctrl, shift, e.Data.KeyCode); // Optional: Log non-matches verbosely
                    return match;
                })
                .ObserveOn(RxApp.MainThreadScheduler) // Ensure command execution is on the UI thread
                .Do(_ => Log.Debug("Ctrl+Shift+P detected by SharpHook pipeline (after Where filter).")) // Changed log message slightly
                .Select(_ => Unit.Default) // We don't need the event args anymore
                .InvokeCommand(FocusCommandBarCommand); // Invoke the focus command
    
            // Start the hook asynchronously
            _globalHook.RunAsync().Subscribe(
                _ => { }, // OnNext (not used)
                ex => Log.Error(ex, "Error during Global Hook execution (RunAsync OnError)"), // Log errors during hook runtime
                () => Log.Information("Global Hook stopped.") // OnCompleted
            );
            Log.Information("SharpHook Global Hook RunAsync called."); // Log that startup was attempted
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
        // Clear previous details
        MessageMetadata.Clear();
        MessageUserProperties.Clear();
        HasUserProperties = false;
        JsonViewer.LoadJson(string.Empty);
        IsJsonViewerVisible = false;

        if (messageVm?.FullMessage == null)
        {
            // Add a placeholder if needed, or leave grids empty
            // MessageMetadata.Add(new MetadataItem("Status", "Select a message to see details."));
            return;
        }

        var msg = messageVm.FullMessage;
        var timestamp = messageVm.Timestamp; // Use the arrival timestamp from the ViewModel

        // --- Populate Metadata ---
        MessageMetadata.Add(new MetadataItem("Timestamp", timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")));
        MessageMetadata.Add(new MetadataItem("Topic", msg.Topic ?? "N/A"));
        MessageMetadata.Add(new MetadataItem("QoS", msg.QualityOfServiceLevel.ToString()));
        MessageMetadata.Add(new MetadataItem("Retain", msg.Retain.ToString()));
        MessageMetadata.Add(new MetadataItem("Payload Format", msg.PayloadFormatIndicator.ToString()));
        MessageMetadata.Add(new MetadataItem("Expiry (s)", msg.MessageExpiryInterval.ToString()));
        MessageMetadata.Add(new MetadataItem("ContentType", msg.ContentType));
        MessageMetadata.Add(new MetadataItem("Response Topic", msg.ResponseTopic));

        
        if (msg.CorrelationData != null && msg.CorrelationData.Length > 0)
        {
            // Attempt to display correlation data as string, otherwise show byte count
            string correlationDisplay;
            try
            {
                correlationDisplay = Encoding.UTF8.GetString(msg.CorrelationData);
            }
            catch
            {
                correlationDisplay = $"[{msg.CorrelationData.Length} bytes]";
            }
            MessageMetadata.Add(new MetadataItem("Correlation Data", correlationDisplay));
        }
        // Add more metadata fields as needed...

        // --- Populate User Properties ---
        if (msg.UserProperties != null && msg.UserProperties.Any())
        {
            HasUserProperties = true;
            foreach (var prop in msg.UserProperties)
            {
                MessageUserProperties.Add(new MetadataItem(prop.Name, prop.Value));
            }
        }
        else
        {
            HasUserProperties = false;
        }

        // --- Handle Payload and JSON Viewer ---
        string payloadAsString = string.Empty;
        bool isPayloadValidUtf8 = false;
        if (msg.Payload.Length > 0) // Check only Length, as ReadOnlySequence<byte> cannot be null
        {
            try
            {
                // Attempt to decode as UTF-8
                payloadAsString = Encoding.UTF8.GetString(msg.Payload);
                isPayloadValidUtf8 = true; // Assume valid if no exception
            }
            catch (DecoderFallbackException)
            {
                isPayloadValidUtf8 = false;
                StatusBarText = "Payload is not valid UTF-8.";
                Log.Warning("Could not decode MQTT message payload for topic '{Topic}' as UTF-8.", msg.Topic);
            }
            catch (Exception ex) // Catch other potential exceptions during decoding
            {
                isPayloadValidUtf8 = false;
                StatusBarText = $"Error decoding payload: {ex.Message}";
                Log.Error(ex, "Error decoding MQTT message payload for topic '{Topic}'.", msg.Topic);
            }
        }
        else
        {
             payloadAsString = "[No Payload]";
             isPayloadValidUtf8 = true; // Treat no payload as valid for JSON viewer (it will show nothing)
        }

        IsJsonViewerVisible = isPayloadValidUtf8;
        if (IsJsonViewerVisible)
        {
            JsonViewer.LoadJson(payloadAsString); // LoadJson handles JSON parsing internally
            if (!string.IsNullOrEmpty(JsonViewer.JsonParseError))
            {
                // If LoadJson resulted in an error, update status bar
                 StatusBarText = $"JSON Parse Error: {JsonViewer.JsonParseError}";
            }
        }
        else
        {
            // If not valid UTF-8, ensure JSON viewer is cleared and hidden
            JsonViewer.LoadJson(string.Empty);
            IsJsonViewerVisible = false;
            // Status bar already set in the catch block or if payload was null/empty
        }
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

                        (ClipboardText,_,_) = textExporter.GenerateDetailedTextFromMessage(msg, SelectedMessage.Timestamp);

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
                    Export(command);
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

    private void Export(ParsedCommand command)
    {
        if (SelectedMessage?.FullMessage == null)
        {
            StatusBarText = "Error: No message selected to export.";
            Log.Warning("Export command failed: No message selected.");
            return;
        }

        if (command.Arguments.Count < 2)
        {
            StatusBarText = "Error: :export requires at least 2 arguments: <format> <folder_path>";
            Log.Warning("Invalid arguments for :export command. Expected format and folder path.");
            return;
        }

        string format = command.Arguments[0].ToLowerInvariant();
        string folderPath = command.Arguments[1];

        IMessageExporter exporter;
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
            return;
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

    // --- IDisposable Implementation ---

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects).
                Log.Debug("Disposing MainViewModel resources...");
                StopTimer();
                _messageHistorySubscription?.Dispose();
                _globalHookSubscription?.Dispose(); // Dispose hook subscription
                _globalHook?.Dispose(); // Dispose the hook itself
                // _mqttEngine?.Dispose(); // MqttEngine does not seem to be IDisposable
                // Dispose other managed resources like commands if necessary
                ConnectCommand?.Dispose();
                DisconnectCommand?.Dispose();
                ClearHistoryCommand?.Dispose();
                PauseResumeCommand?.Dispose();
                OpenSettingsCommand?.Dispose();
                SubmitInputCommand?.Dispose();
                FocusCommandBarCommand?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            _disposedValue = true;
            Log.Debug("MainViewModel disposed.");
        }
    }

    // // Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~MainViewModel()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
