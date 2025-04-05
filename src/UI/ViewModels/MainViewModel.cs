using Avalonia;
using Avalonia.Input.Platform; // For IClipboard
using Avalonia.Platform.Storage; // Potentially needed for other storage operations
using Avalonia.Controls; // For TopLevel
using Avalonia.VisualTree; // For GetVisualRoot
using Avalonia.Interactivity; // For RoutedEventArgs (if needed later)
using Avalonia.Threading; // Already present
using ReactiveUI;
using ReactiveUI.Fody.Helpers; // For [Reactive] attribute if needed, but also for Interaction
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
    private readonly CancellationTokenSource _cts = new(); // Added cancellation token source for graceful shutdown
    private bool _isWindowFocused; // Added to track window focus for global hook
    private bool _isTopicFilterActive; // Added to track if the topic filter is active

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

    /// <summary>
    /// Gets a value indicating whether the topic tree filter is currently active.
    /// </summary>
    public bool IsTopicFilterActive
    {
        get => _isTopicFilterActive;
        private set => this.RaiseAndSetIfChanged(ref _isTopicFilterActive, value); // Private setter
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
  public ReactiveCommand<MessageViewModel, Unit> CopyPayloadCommand { get; } // Added command to copy payload

  // Interaction for requesting clipboard copy from the View
  public Interaction<string, Unit> CopyTextToClipboardInteraction { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the main application window currently has focus.
    /// This is used to conditionally enable the global hotkey.
    /// </summary>
    public bool IsWindowFocused
    {
        get => _isWindowFocused;
        set => this.RaiseAndSetIfChanged(ref _isWindowFocused, value);
    }

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
       CopyTextToClipboardInteraction = new Interaction<string, Unit>(); // Initialize the interaction

        // Populate the list of available commands (assuming CommandType enum has all commands)
        _availableCommands = Enum.GetNames(typeof(CommandType))
                                 .Where(name => name != nameof(CommandType.Unknown)) // Exclude Unknown
                                 .Select(name => ":" + name.ToLowerInvariant()) // Prefix with ':' and make lowercase
                                 .OrderBy(cmd => cmd) // Sort alphabetically
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
       SubmitInputCommand = ReactiveCommand.Create(ExecuteSubmitInput); // Allow execution even when text is empty (handled inside method)
       FocusCommandBarCommand = ReactiveCommand.Create(() => { Log.Debug("FocusCommandBarCommand executed by global hook."); /* Actual focus happens in View code-behind */ });
       CopyPayloadCommand = ReactiveCommand.CreateFromTask<MessageViewModel>(CopyPayloadToClipboardAsync); // Initialize copy payload command

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
                .Do(e => {})
                .Where(e =>
                {
                    // Check for either Left or Right Ctrl/Shift explicitly
                    bool ctrl = e.RawEvent.Mask.HasFlag(ModifierMask.LeftCtrl) || e.RawEvent.Mask.HasFlag(ModifierMask.RightCtrl);
                    bool shift = e.RawEvent.Mask.HasFlag(ModifierMask.LeftShift) || e.RawEvent.Mask.HasFlag(ModifierMask.RightShift);
                    bool pKey = e.Data.KeyCode == KeyCode.VcP;

                    // NEW: Check if the window is focused
                    bool focused = IsWindowFocused;

                    bool match = focused && ctrl && shift && pKey;

                    // Log details for debugging
                    if (match)
                    {
                        Log.Debug("Ctrl+Shift+P MATCHED inside Where filter (Window Focused: {IsFocused}).", focused);
                    }
                    else if (ctrl && shift && pKey) // Log if keys match but focus doesn't
                    {
                        Log.Verbose("Ctrl+Shift+P detected but window not focused. Hook suppressed.");
                    }
                    // else Log.Verbose("Keypress did not match Ctrl+Shift+P: Focused={Focused}, Ctrl={Ctrl}, Shift={Shift}, Key={Key}", focused, ctrl, shift, e.Data.KeyCode); // Optional: Log non-matches verbosely

                    // Log the state being evaluated
                    Log.Verbose("Global Hook Filter Check: Key={Key}, Modifiers={Modifiers}, IsWindowFocused={IsFocused}, Result={Match}", e.Data.KeyCode, e.RawEvent.Mask, focused, match);

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
        await _mqttEngine.DisconnectAsync(_cts.Token); // Pass cancellation token
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
        Log.Debug("ExecuteSubmitInput triggered. CommandText: '{CommandText}'", CommandText); // Add logging
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
                    ConnectToMqttBroker(command);
                    break;
                case CommandType.Disconnect:
                    DisconnectFromMqttBroker();
                    break;
                case CommandType.Clear:
                    ClearMessageHistory();
                    break;
                case CommandType.Copy:
                    CopySelectedMessageDetails();
                    break; // Correct placement outside the if/else block
                case CommandType.Help:
                    DisplayHelpInformation();
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
                    // Example (filter to MQTT paths containing "foo"): :filter foo 
                    // Example (clear filter): :filter
                    ApplyTopicFilter(command.Arguments.FirstOrDefault()); // Pass the first argument as the filter
                    break;
                case CommandType.Search:
                    // Apply the search term from the command arguments
                    string searchTerm = command.Arguments.FirstOrDefault() ?? string.Empty;
                    CurrentSearchTerm = searchTerm; // Set the property, which triggers the filter
                    StatusBarText = string.IsNullOrWhiteSpace(searchTerm) ? "Search cleared." : $"Search filter applied: '{searchTerm}'.";
                    Log.Information("Search command executed. Term: '{SearchTerm}'", searchTerm);
                    // Optionally clear the command text box after executing :search
                    // CommandText = string.Empty;
                    break;
                case CommandType.Expand:
                    ExpandAllNodes();
                    break;
                case CommandType.Collapse:
                    CollapseAllNodes();
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

    private void DisplayHelpInformation()
    {
        StatusBarText = "Available commands: :connect, :disconnect, :export, :filter, :search, :copy, :clear, :help, :pause, :resume, :expand, :collapse";
        Log.Information("Displaying help information.");
    }

    private void ClearMessageHistory()
    {
        StatusBarText = "Clearing history...";
        ClearHistoryCommand.Execute().Subscribe();
    }

    private void CopySelectedMessageDetails()
    {
        if (SelectedMessage?.FullMessage != null)
        {
            var msg = SelectedMessage.FullMessage;
            var textExporter = new TextExporter();

            (ClipboardText, _, _) = textExporter.GenerateDetailedTextFromMessage(msg, SelectedMessage.Timestamp);

            StatusBarText = "Updated system clipboard with selected message";
            Log.Information("Copy command executed.");
        }
        else
        {
            StatusBarText = "No message selected to copy.";
            Log.Information("Copy command executed but no message was selected.");
        }
    }

    private void DisconnectFromMqttBroker()
    {
        StatusBarText = "Disconnecting...";
        DisconnectCommand.Execute().Subscribe(
             _ => StatusBarText = "Successfully initiated disconnection.",
             ex =>
             {
                 StatusBarText = $"Error initiating disconnection: {ex.Message}";
                 Log.Error(ex, "Error executing DisconnectCommand");
             });
    }

    private void ConnectToMqttBroker(ParsedCommand command)
    {
        if (command.Arguments.Count != 1)
        {
            StatusBarText = "Error: :connect requires exactly one argument: <server_address:port>";
            Log.Warning("Invalid arguments for :connect command.");
            return;
        }
        // Parse server:port
        var parts = command.Arguments[0].Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !int.TryParse(parts[1], out int port) || port < 1 || port > 65535)
        {
            StatusBarText = $"Error: Invalid format for :connect argument '{command.Arguments[0]}'. Expected: <server_address:port>";
            Log.Warning("Invalid format for :connect argument: {Argument}", command.Arguments[0]);
            return;
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
        return;
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

    /// <summary>
    /// Applies a filter to the topic tree, showing only nodes where at least one path segment fuzzy matches the filter.
    /// </summary>
    /// <param name="filter">The filter string. If null or empty, clears the filter.</param>
    private void ApplyTopicFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            // Clear filter: Make all nodes visible
            int dummyMatchCount = 0; // Need a variable for the ref parameter
            SetNodeVisibilityRecursive(TopicTreeNodes, isVisible: true, clearFilter: true, ref dummyMatchCount); // Pass the ref parameter
            StatusBarText = "Topic filter cleared.";
            IsTopicFilterActive = false; // Update filter state
            Log.Information("Topic filter cleared.");
            return;
        }

        Log.Information("Applying topic filter: '{Filter}'", filter);
        int matchCount = 0;
        SetNodeVisibilityRecursive(TopicTreeNodes, isVisible: false, clearFilter: false, ref matchCount, filter); // Start recursion (matchCount before optional filter)
        StatusBarText = $"Topic filter applied: '{filter}'. Found {matchCount} matching node(s).";
        IsTopicFilterActive = true; // Update filter state
    }

    /// <summary>
    /// Recursively sets the IsVisible property on nodes based on a filter or clears the filter.
    /// </summary>
    /// <param name="nodes">The collection of nodes to process.</param>
    /// <param name="isVisible">The initial visibility state to assume (used when clearing).</param>
    /// <param name="clearFilter">If true, sets all nodes to visible.</param>
    /// <param name="matchCount">Reference to the count of matching nodes.</param>
    /// <param name="filter">The filter string (only used if clearFilter is false).</param>
    /// <returns>True if any node in this branch (itself or descendants) is visible.</returns>
    private bool SetNodeVisibilityRecursive(IEnumerable<NodeViewModel> nodes, bool isVisible, bool clearFilter, ref int matchCount, string? filter = null) // Reordered parameters
    {
        bool anyChildVisible = false;
        foreach (var node in nodes)
        {
            bool nodeMatches = false;
            if (!clearFilter && !string.IsNullOrEmpty(node.FullPath) && !string.IsNullOrEmpty(filter))
            {
                var segments = node.FullPath.Split('/');
                // Check if any segment fuzzy matches the filter (e.g., partial ratio > 70)
                nodeMatches = segments.Any(segment => !string.IsNullOrEmpty(segment) && Fuzz.PartialRatio(segment.ToLowerInvariant(), filter.ToLowerInvariant()) > 70);
            }

            // Recursively check children first. The result indicates if any child *became* visible.
            bool childVisible = SetNodeVisibilityRecursive(node.Children, isVisible, clearFilter, ref matchCount, filter); // Updated call

            // Determine visibility
            if (clearFilter)
            {
                node.IsVisible = true; // Always visible when clearing
                anyChildVisible = true; // If clearing, assume visibility propagates up
            }
            else
            {
                // Node is visible if it matches directly OR if any of its children are visible
                node.IsVisible = nodeMatches || childVisible;
                if (node.IsVisible)
                {
                    anyChildVisible = true; // Mark that at least one node at this level (or below) is visible
                    if (nodeMatches) // Count if the node itself is a match
                    {
                        matchCount++;
                    }
                }
            }
        }
        return anyChildVisible; // Return true if any node at this level or below is visible
    }


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

    /// <summary>
    /// Expands all nodes in the topic tree.
    /// </summary>
    private void ExpandAllNodes()
    {
        Log.Information("Expand all nodes command executed.");
        // Ensure the recursive update happens on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            SetNodeExpandedRecursive(TopicTreeNodes, true);
            StatusBarText = "All topic nodes expanded.";
            Log.Debug("Finished setting IsExpanded=true on nodes via Dispatcher.");
        });
    }

    /// <summary>
    /// Collapses all nodes in the topic tree.
    /// </summary>
    private void CollapseAllNodes()
    {
        Log.Information("Collapse all nodes command executed.");
        // Ensure the recursive update happens on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            SetNodeExpandedRecursive(TopicTreeNodes, false);
            StatusBarText = "All topic nodes collapsed.";
            Log.Debug("Finished setting IsExpanded=false on nodes via Dispatcher.");
        });
    }

    /// <summary>
    /// Recursively sets the IsExpanded property on nodes.
    /// </summary>
    /// <param name="nodes">The collection of nodes to process.</param>
    /// <param name="isExpanded">The desired expansion state.</param>
    private void SetNodeExpandedRecursive(IEnumerable<NodeViewModel> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = isExpanded;
            if (node.Children.Any()) // Only recurse if there are children
            {
                SetNodeExpandedRecursive(node.Children, isExpanded);
            }
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

   // --- Helper Methods ---

   /// <summary>
    /// Copies the full payload of the given message to the system clipboard using an Interaction.
    /// </summary>
    /// <param name="messageVm">The view model of the message whose payload should be copied.</param>
    private async Task CopyPayloadToClipboardAsync(MessageViewModel messageVm)
    {
        if (messageVm?.FullMessage?.Payload == null)
        {
            StatusBarText = "Cannot copy: Message or payload is missing.";
            Log.Warning("CopyPayloadCommand failed: MessageViewModel or FullMessage or Payload was null.");
            return;
        }

        string payloadString;
        try
        {
            payloadString = Encoding.UTF8.GetString(messageVm.FullMessage.Payload);
        }
        catch (Exception ex)
        {
            StatusBarText = "Error decoding payload for clipboard.";
            Log.Error(ex, "Failed to decode payload to UTF8 string for clipboard copy.");
            return;
        }

        try
        {
            // Invoke the interaction to request the View to copy the text
            await CopyTextToClipboardInteraction.Handle(payloadString);
            StatusBarText = "Payload copied to clipboard."; // Assume success if Handle doesn't throw
            Log.Information("CopyTextToClipboardInteraction handled for topic '{Topic}'.", messageVm.FullMessage.Topic);
        }
        catch (Exception ex) // Catch potential exceptions from the interaction handler
        {
            StatusBarText = $"Error copying to clipboard: {ex.Message}";
            Log.Error(ex, "Exception occurred during CopyTextToClipboardInteraction handling.");
        }
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
                if (!_cts.IsCancellationRequested)
                {
                    Log.Debug("Requesting cancellation via CancellationTokenSource.");
                    _cts.Cancel(); // Signal cancellation first
                }
                StopTimer();
                _messageHistorySubscription?.Dispose();
                _globalHookSubscription?.Dispose(); // Dispose hook subscription
                _globalHook?.Dispose(); // Dispose the hook itself
                // MqttEngine's Dispose method now handles the final disconnect attempt.
                // We rely on _cts.Cancel() being called first, then _mqttEngine.Dispose() below.
                // Removed explicit synchronous DisconnectAsync call here.
                _mqttEngine?.Dispose(); // Dispose the MqttEngine instance AFTER cancellation is signaled
                // Dispose other managed resources like commands if necessary
                ConnectCommand?.Dispose();
                DisconnectCommand?.Dispose();
                ClearHistoryCommand?.Dispose();
                PauseResumeCommand?.Dispose();
                OpenSettingsCommand?.Dispose();
                SubmitInputCommand?.Dispose();
               FocusCommandBarCommand?.Dispose();
               CopyPayloadCommand?.Dispose(); // Dispose the new command
               // Interactions don't typically need explicit disposal unless they hold heavy resources
               _cts.Dispose(); // Dispose the CancellationTokenSource itself
              }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            _disposedValue = true;
            Log.Debug("MainViewModel disposed.");
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
