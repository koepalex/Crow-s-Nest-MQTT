using CrowsNestMqtt.BusinessLogic.Exporter;
using Avalonia;
using Avalonia.Controls; // For TopLevel
using Avalonia.Threading; // Already present
using ReactiveUI;
using Serilog; // Added Serilog
using System.Collections.ObjectModel;
using System.Reactive; // Required for Unit
using System.Reactive.Linq; // Required for Select, ObserveOn, Throttle, DistinctUntilChanged
using System.Buffers;
using System.Text; // For Encoding and StringBuilder
using System.Text.Json; // Added for JSON formatting
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document; // Added for TextDocument
using MQTTnet;
using AvaloniaEdit.Highlighting; // Added for Syntax Highlighting
using CrowsNestMqtt.BusinessLogic; // Required for MqttEngine, MqttConnectionStateChangedEventArgs, IMqttService
using CrowsNestMqtt.BusinessLogic.Commands; // Added for command parsing
using CrowsNestMqtt.BusinessLogic.Services; // Added for command parsing
using CrowsNestMqtt.UI.Services; // Added for IStatusBarService
using DynamicData; // Added for SourceList and reactive filtering
using DynamicData.Binding; // Added for Bind()
using FuzzySharp; // Added for fuzzy search
using SharpHook.Native; // Added SharpHook Native for KeyCode and ModifierMask
using SharpHook.Reactive; // Added SharpHook Reactive

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages the different sections of the UI: Topic List, Message History, Message Details, and Command Bar.
/// </summary>
public class MainViewModel : ReactiveObject, IDisposable, IStatusBarService // Implement IDisposable and IStatusBarService
{
    private readonly IMqttService _mqttService; // Changed to interface
    private readonly ICommandParserService _commandParserService; // Added command parser service
    private Timer? _updateTimer;
    private readonly SynchronizationContext? _syncContext; // To post updates to the UI thread
    private readonly SourceList<MessageViewModel> _messageHistorySource = new(); // Backing source for DynamicData
    private readonly IDisposable _messageHistorySubscription; // To dispose the pipeline
    private readonly ReadOnlyObservableCollection<MessageViewModel> _filteredMessageHistory; // Field for the bound collection
    private string _currentSearchTerm = string.Empty; // Backing field for search term
    private readonly List<string> _availableCommands; // Added list of commands for suggestions
    private readonly IReactiveGlobalHook? _globalHook; // Added SharpHook global hook
    private readonly IDisposable? _globalHookSubscription; // Added subscription for the hook
    private bool _disposedValue; // For IDisposable pattern
    private readonly CancellationTokenSource _cts = new(); // Added cancellation token source for graceful shutdown
    private bool _isWindowFocused; // Added to track window focus for global hook
    private bool _isTopicFilterActive; // Added to track if the topic filter is active
    
    // Batch processing for high-volume message scenarios
    private readonly Queue<IdentifiedMqttApplicationMessageReceivedEventArgs> _pendingMessages = new();
    private readonly object _batchLock = new object();
    private Timer? _batchProcessingTimer;

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

    private ConnectionStatusState _connectionStatus = ConnectionStatusState.Disconnected;
    public ConnectionStatusState ConnectionStatus
    {
        get => _connectionStatus;
        private set => this.RaiseAndSetIfChanged(ref _connectionStatus, value);
    }

    // Computed properties for easy binding in XAML and command `CanExecute`
    public bool IsConnected => ConnectionStatus == ConnectionStatusState.Connected;
    public bool IsConnecting => ConnectionStatus == ConnectionStatusState.Connecting;
    public bool IsDisconnected => ConnectionStatus == ConnectionStatusState.Disconnected;

    private string? _connectionStatusMessage;
    public string? ConnectionStatusMessage
    {
        get => _connectionStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _connectionStatusMessage, value);
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
        private set => this.RaiseAndSetIfChanged(ref _isJsonViewerVisible, value); // Make setter private
    }

    private bool _isRawTextViewerVisible = false; // Added backing field for raw text view
    public bool IsRawTextViewerVisible // Added property for raw text view visibility
    {
        get => _isRawTextViewerVisible;
        private set => this.RaiseAndSetIfChanged(ref _isRawTextViewerVisible, value); // Make setter private
    }

    // Changed from string to TextDocument for binding
    private TextDocument _rawPayloadDocument = new();
    public TextDocument RawPayloadDocument
    {
        get => _rawPayloadDocument;
        private set => this.RaiseAndSetIfChanged(ref _rawPayloadDocument, value);
    }

    private IHighlightingDefinition? _payloadSyntaxHighlighting; // Added backing field for syntax highlighting
    public IHighlightingDefinition? PayloadSyntaxHighlighting // Added property for syntax highlighting binding
    {
        get => _payloadSyntaxHighlighting;
        private set => this.RaiseAndSetIfChanged(ref _payloadSyntaxHighlighting, value); // Make setter private
    }

    // Computed property to show JSON parse error only when neither viewer is active but an error exists
    public bool ShowJsonParseError => !IsJsonViewerVisible && !IsRawTextViewerVisible && !IsImageViewerVisible && !string.IsNullOrEmpty(JsonViewer.JsonParseError);

    private bool _isImageViewerVisible;
    public bool IsImageViewerVisible
    {
        get => _isImageViewerVisible;
        private set => this.RaiseAndSetIfChanged(ref _isImageViewerVisible, value);
    }

    private Bitmap? _imagePayload;
    public Bitmap? ImagePayload
    {
        get => _imagePayload;
        private set => this.RaiseAndSetIfChanged(ref _imagePayload, value);
    }

    // Computed property to control the visibility of the splitter below the payload viewers
    public bool IsAnyPayloadViewerVisible => IsJsonViewerVisible || IsRawTextViewerVisible || IsImageViewerVisible;

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
    public ReactiveCommand<object?, Unit> CopyPayloadCommand { get; } // Added command to copy payload

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
    public MainViewModel(ICommandParserService commandParserService, string? aspireHostname = null, int? aspirePort = null)
    {
        _commandParserService = commandParserService ?? throw new ArgumentNullException(nameof(commandParserService)); // Store injected service
        _syncContext = SynchronizationContext.Current; // Capture sync context
        Settings = new SettingsViewModel(); // Instantiate settings
        JsonViewer = new JsonViewerViewModel(); // Instantiate JSON viewer VM
        CopyTextToClipboardInteraction = new Interaction<string, Unit>(); // Initialize the interaction

        // Populate the list of available commands (using the help dictionary keys)
        _availableCommands = CommandHelpDetails.Keys
                                  .Select(name => ":" + name.ToLowerInvariant()) // Prefix with ':'
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
                    // Use the Topic property directly from MessageViewModel
                    string? msgTopic = message.Topic;
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
            .Subscribe(_ =>
            {
                // The collection is created by the Bind operator, so it shouldn't be null here,
                // but the compiler can't know that. A check is safe.
                if (_filteredMessageHistory is null) return;

                // If the current selection is no longer in the filtered list (e.g., due to topic change),
                // select the first item in the new list automatically.
                if (SelectedMessage == null || !_filteredMessageHistory.Contains(SelectedMessage))
                {
                    SelectedMessage = _filteredMessageHistory.FirstOrDefault();
                }
            }, ex => Log.Error(ex, "Error in MessageHistory DynamicData pipeline"));

        FilteredMessageHistory = _filteredMessageHistory; // Assign the bound collection

        if (!string.IsNullOrEmpty(aspireHostname) && aspirePort.HasValue)
        {
            if (aspirePort.Value > 0 && aspirePort.Value < 65535)
            {
                Log.Information("Using Aspire-provided MQTT configuration. Hostname: {Hostname}, Port: {Port}", aspireHostname, aspirePort.Value);
                Settings.Hostname = aspireHostname; // Example if you want to update the UI settings fields
                Settings.Port = aspirePort.Value;
            }
        }

        var connectionSettings = new MqttConnectionSettings
        {
            Hostname = Settings.Hostname,
            Port = Settings.Port,
            ClientId = Settings.ClientId, // Keep other settings from SettingsViewModel
            KeepAliveInterval = Settings.KeepAliveInterval,
            CleanSession = Settings.CleanSession,
            SessionExpiryInterval = Settings.SessionExpiryInterval,
            TopicSpecificBufferLimits = Settings.Into().TopicSpecificBufferLimits,
            AuthMode = Settings.Into().AuthMode
            // TODO: Map other settings like TLS, Credentials if added
        };

        _mqttService = new MqttEngine(connectionSettings);

        _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
        _mqttService.MessageReceived += OnMessageReceived;
        _mqttService.LogMessage += OnLogMessage;

        // --- Command Implementations ---
        // --- Command Implementations ---
        // Define CanExecute conditions for commands based on connection status
        var canConnect = this.WhenAnyValue(x => x.ConnectionStatus)
                             .Select(status => status == ConnectionStatusState.Disconnected);

        var canDisconnect = this.WhenAnyValue(x => x.ConnectionStatus)
                                .Select(status => status == ConnectionStatusState.Connected || status == ConnectionStatusState.Connecting);

        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, canDisconnect);
        ClearHistoryCommand = ReactiveCommand.Create(ClearHistory);
        PauseResumeCommand = ReactiveCommand.Create(TogglePause);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings); // Initialize Settings Command
        SubmitInputCommand = ReactiveCommand.Create(ExecuteSubmitInput); // Allow execution even when text is empty (handled inside method)
        FocusCommandBarCommand = ReactiveCommand.Create(() => { Log.Debug("FocusCommandBarCommand executed by global hook."); /* Actual focus happens in View code-behind */ });
        CopyPayloadCommand = ReactiveCommand.CreateFromTask<object?>(CopyPayloadToClipboardAsync); // Initialize copy payload command

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
        try
        {
            _globalHook = new SimpleReactiveGlobalHook();
            _globalHookSubscription = _globalHook.KeyPressed
                .Do(e => { })
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
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize or run SharpHook global hook. Hotkey might not work.");
            _globalHook = null; // Ensure hook is null if initialization failed
            _globalHookSubscription = null;
        }
    }

    private void OnLogMessage(object? sender, string log)
    {
        // TODO: Implement proper logging (e.g., to a log panel or file)
        Log.Debug("[MQTT Engine]: {LogMessage}", log);
    }

    // --- MQTT Event Handlers ---

    private void OnConnectionStateChanged(object? sender, MqttConnectionStateChangedEventArgs e)
    {
        // This event is now the single source of truth for connection state.
        // The MqttEngine will fire this event when it is connecting, connected, or disconnected.
        Dispatcher.UIThread.Post(() =>
        {
            // The new event args from the engine will tell us the exact state.
            ConnectionStatus = e.ConnectionStatus;
            ConnectionStatusMessage = e.ReconnectInfo ?? e.Error?.Message;

            // Manually raise property changed for all computed properties that depend on the status
            this.RaisePropertyChanged(nameof(IsConnected));
            this.RaisePropertyChanged(nameof(IsConnecting));
            this.RaisePropertyChanged(nameof(IsDisconnected));

            if (ConnectionStatus == ConnectionStatusState.Connected)
            {
                Log.Information("MQTT Client Connected. Starting UI Timer.");
                StartTimer(TimeSpan.FromSeconds(1));
                LoadInitialTopics();
            }
            else // Disconnected or Connecting
            {
                Log.Warning(e.Error, "MQTT Client is not connected. Stopping UI Timer.");
                StopTimer();
            }
        });
    }

    // Updated signature to use new EventArgs with batching for high-volume scenarios
    private void OnMessageReceived(object? sender, IdentifiedMqttApplicationMessageReceivedEventArgs e)
    {
        if (IsPaused) return; // Don't update UI if paused

        // Add message to batch queue for processing
        lock (_batchLock)
        {
            _pendingMessages.Enqueue(e);
            
            // Start batch processing timer if not already running
            if (_batchProcessingTimer == null)
            {
                // Use very short initial delay for rapid startup scenarios (retained messages)
                var initialDelay = _pendingMessages.Count > 10 ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(25);
                _batchProcessingTimer = new Timer(ProcessMessageBatch, null, initialDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }
    
    /// <summary>
    /// Processes batched messages on the UI thread to improve performance during high-volume scenarios
    /// </summary>
    private void ProcessMessageBatch(object? state)
    {
        var messagesToProcess = new List<IdentifiedMqttApplicationMessageReceivedEventArgs>();
        bool hasMoreMessages = false;
        
        // Extract batch of messages from queue
        lock (_batchLock)
        {
            // Adaptive batch size based on queue length
            int maxBatchSize = Math.Min(100, Math.Max(10, _pendingMessages.Count / 2)); 
            int count = 0;
            while (_pendingMessages.Count > 0 && count < maxBatchSize)
            {
                messagesToProcess.Add(_pendingMessages.Dequeue());
                count++;
            }
            
            hasMoreMessages = _pendingMessages.Count > 0;
            
            // Reset timer if more messages remain
            if (hasMoreMessages)
            {
                // Use shorter delay for high-volume scenarios
                var delay = _pendingMessages.Count > 50 ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(25);
                _batchProcessingTimer?.Change(delay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                // No more messages, dispose timer
                _batchProcessingTimer?.Dispose();
                _batchProcessingTimer = null;
            }
        }
        
        // Process the batch on UI thread if we have messages
        if (messagesToProcess.Count > 0)
        {
            Dispatcher.UIThread.Post(() => ProcessMessageBatchOnUIThread(messagesToProcess));
        }
    }
    
    /// <summary>
    /// Processes a batch of messages on the UI thread
    /// </summary>
    private void ProcessMessageBatchOnUIThread(List<IdentifiedMqttApplicationMessageReceivedEventArgs> messages)
    {
        var messageViewModels = new List<MessageViewModel>();
        var topicCounts = new Dictionary<string, int>(); // Track message count per topic for batch updates
        
        // Process all messages in the batch
        foreach (var e in messages)
        {
            var topic = e.Topic;
            var messageId = e.MessageId;
            
            // Count messages per topic for efficient tree updates
            topicCounts.TryGetValue(topic, out var currentCount);
            topicCounts[topic] = currentCount + 1;

            // Basic payload preview (handle potential null/empty payload)
            string preview = e.ApplicationMessage.Payload.Length > 0
                ? Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
                : "[No Payload]";

            // Limit preview length
            const int maxPreviewLength = 100;
            if (preview.Length > maxPreviewLength)
            {
                preview = preview.Substring(0, maxPreviewLength) + "...";
            }

            // Create MessageViewModel
            var messageVm = new MessageViewModel(
                messageId,
                topic,
                DateTime.Now,
                preview.Replace(Environment.NewLine, " "),
                (int)e.ApplicationMessage.Payload.Length,
                _mqttService,
                this);
            
            messageViewModels.Add(messageVm);
        }
        
        // Batch add messages to source list
        _messageHistorySource.AddRange(messageViewModels);
        
        // Batch update topic tree - update each topic only once with total count
        foreach (var kvp in topicCounts)
        {
            string topic = kvp.Key;
            int messageCount = kvp.Value;
            
            // Update tree structure and increment count by the total for this topic
            UpdateOrCreateNodeWithCount(topic, messageCount);
        }
        
        Log.Verbose("Processed batch of {Count} messages across {TopicCount} topics. Source count: {Total}", 
            messages.Count, topicCounts.Count, _messageHistorySource.Count);
    }

    // --- UI Update Logic ---

    private void LoadInitialTopics()
    {
        var bufferedTopics = _mqttService.GetBufferedTopics(); // Assuming IMqttService exposes this
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
        JsonViewer.LoadJson(string.Empty); // Clear JSON viewer
        IsJsonViewerVisible = false; // Hide JSON viewer
        IsRawTextViewerVisible = false; // Hide Raw Text viewer
        IsImageViewerVisible = false;
        ImagePayload?.Dispose();
        ImagePayload = null;
        // Clear the document content instead of the string property
        RawPayloadDocument.Text = string.Empty;
        PayloadSyntaxHighlighting = null; // Clear syntax highlighting
        this.RaisePropertyChanged(nameof(ShowJsonParseError)); // Notify computed property change
        this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible)); // Notify computed property change

        var msg = messageVm?.GetFullMessage(); // This returns MqttApplicationMessage
        if (msg == null) // Check the result of the method call
        {
            return;
        }

        var timestamp = messageVm?.Timestamp; // Use null-forgiving operator as messageVm is guaranteed non-null here

        // --- Populate Metadata ---
        MessageMetadata.Add(new MetadataItem("Timestamp", timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "unknown"));
        MessageMetadata.Add(new MetadataItem("Topic", msg.Topic ?? "N/A"));
        MessageMetadata.Add(new MetadataItem("QoS", msg.QualityOfServiceLevel.ToString()));
        MessageMetadata.Add(new MetadataItem("Retain", msg.Retain.ToString()));
        MessageMetadata.Add(new MetadataItem("Payload Format", msg.PayloadFormatIndicator.ToString()));
        MessageMetadata.Add(new MetadataItem("Expiry (s)", msg.MessageExpiryInterval.ToString()));
        MessageMetadata.Add(new MetadataItem("ContentType", msg.ContentType));
        MessageMetadata.Add(new MetadataItem("Response Topic", msg.ResponseTopic));

        if (msg.CorrelationData != null && msg.CorrelationData.Length > 0)
        {
            string correlationDisplay;
            try
            {
                correlationDisplay = BitConverter.ToString(msg.CorrelationData).Replace("-", string.Empty);
            }
            catch
            {
                correlationDisplay = $"[{msg.CorrelationData.Length} bytes]";
            }
            MessageMetadata.Add(new MetadataItem("Correlation Data", correlationDisplay));
        }

        // --- Populate User Properties ---
        HasUserProperties = msg.UserProperties?.Count > 0;
        if (HasUserProperties && msg.UserProperties != null)
        {
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
        var payloadBytes = msg.Payload.ToArray();

        if (payloadBytes.Length > 0)
        {
            try
            {
                payloadAsString = Encoding.UTF8.GetString(payloadBytes);
                isPayloadValidUtf8 = true;
            }
            catch (DecoderFallbackException)
            {
                isPayloadValidUtf8 = false;
                StatusBarText = "Payload is not valid UTF-8.";
                Log.Warning("Could not decode MQTT message payload for topic '{Topic}' as UTF-8.", msg.Topic);
            }
            catch (Exception ex)
            {
                isPayloadValidUtf8 = false;
                StatusBarText = $"Error decoding payload: {ex.Message}";
                Log.Error(ex, "Error decoding MQTT message payload for topic '{Topic}'.", msg.Topic);
            }
        }
        else
        {
            payloadAsString = "[No Payload]";
            isPayloadValidUtf8 = true;
        }

        if (isPayloadValidUtf8)
        {
            if (payloadAsString.Trim().StartsWith("{") || payloadAsString.Trim().StartsWith("["))
            {
                try
                {
                    var jsonDoc = JsonDocument.Parse(payloadAsString);
                    RawPayloadDocument.Text = JsonSerializer.Serialize(jsonDoc.RootElement, MainViewModelJsonContext.Default.JsonElement);
                    Log.Debug("Formatted JSON payload for raw text display");
                }
                catch (JsonException)
                {
                    RawPayloadDocument.Text = payloadAsString;
                    Log.Verbose("Payload looks like JSON but could not be parsed, displaying as plain text");
                }
                catch (Exception ex)
                {
                    RawPayloadDocument.Text = payloadAsString;
                    Log.Warning(ex, "Error formatting JSON payload");
                }
            }
            else
            {
                RawPayloadDocument.Text = payloadAsString;
            }
        }
        else
        {
            RawPayloadDocument.Text = $"[Binary Data: {payloadBytes.Length} bytes]";
        }

        // Determine initial view state and syntax highlighting
        if (msg.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var ms = new MemoryStream(payloadBytes);
                ImagePayload = new Bitmap(ms);
                IsImageViewerVisible = true;
                IsJsonViewerVisible = false;
                IsRawTextViewerVisible = false;
                StatusBarText = "Displaying image payload.";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not decode image from payload for content type {ContentType}", msg.ContentType);
                ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
            }
        }
        else if (isPayloadValidUtf8)
        {
            JsonViewer.LoadJson(payloadAsString);
            if (string.IsNullOrEmpty(JsonViewer.JsonParseError))
            {
                IsJsonViewerVisible = true;
                IsRawTextViewerVisible = false;
                IsImageViewerVisible = false;
                PayloadSyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Json");
            }
            else
            {
                ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
                StatusBarText = $"Payload is not valid JSON. Showing raw view. {JsonViewer.JsonParseError}";
            }
        }
        else
        {
            ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
        }
        this.RaisePropertyChanged(nameof(ShowJsonParseError));
        this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
    }

    private void ShowRawPayload(bool isPayloadValidUtf8, string payloadAsString, MqttApplicationMessage? msg)
    {
        IsJsonViewerVisible = false;
        IsRawTextViewerVisible = true;
        IsImageViewerVisible = false;
        if (isPayloadValidUtf8)
        {
            RawPayloadDocument.Text = payloadAsString;
            PayloadSyntaxHighlighting = GuessSyntaxHighlighting(msg?.ContentType ?? string.Empty, payloadAsString);
        }
        else
        {
            RawPayloadDocument.Text = $"[Binary Data: {msg?.Payload.Length ?? -1} bytes]";
            PayloadSyntaxHighlighting = null;
        }
    }

    // Helper to guess syntax highlighting
    private IHighlightingDefinition? GuessSyntaxHighlighting(string? contentType, string payload)
    {
        if (contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            return HighlightingManager.Instance.GetDefinition("Json");
        if (contentType?.Contains("xml", StringComparison.OrdinalIgnoreCase) == true)
            return HighlightingManager.Instance.GetDefinition("XML");
        if (contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            return HighlightingManager.Instance.GetDefinition("HTML");
        if (contentType?.Contains("javascript", StringComparison.OrdinalIgnoreCase) == true)
            return HighlightingManager.Instance.GetDefinition("JavaScript");

        // Simple content-based guessing
        var trimmedPayload = payload.TrimStart();
        if (trimmedPayload.StartsWith("<")) return HighlightingManager.Instance.GetDefinition("XML"); // Could be XML or HTML
        if (trimmedPayload.StartsWith("{") || trimmedPayload.StartsWith("[")) return HighlightingManager.Instance.GetDefinition("Json");

        return null; // Default to no highlighting
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
        ClearHistory();
        Log.Information("Connect command executed.");
        // Rebuild connection settings from ViewModel just before connecting
        var connectionSettings = new MqttConnectionSettings
        {
            Hostname = Settings.Hostname,
            Port = Settings.Port,
            ClientId = Settings.ClientId,
            KeepAliveInterval = Settings.KeepAliveInterval,
            CleanSession = Settings.CleanSession,
            SessionExpiryInterval = Settings.SessionExpiryInterval,
            TopicSpecificBufferLimits = Settings.Into().TopicSpecificBufferLimits,
            AuthMode = Settings.Into().AuthMode,
            UseTls = Settings.UseTls
        };
        
        _mqttService.UpdateSettings(connectionSettings);
        
        // The MqttEngine now manages its own cancellation token.
        // We just need to call the method.
        await _mqttService.ConnectAsync();
    }

    private async Task DisconnectAsync()
    {
        Log.Information("Disconnect/Cancel command executed.");
        // The MqttEngine's DisconnectAsync is now responsible for handling
        // both disconnection and cancellation of an ongoing connection attempt.
        await _mqttService.DisconnectAsync();
    }

    private void ClearHistory()
    {
        Log.Information("Clear history command executed.");
        _messageHistorySource.Clear(); // Clear the source list
        TopicTreeNodes.Clear();        // Clear the topic tree
        SelectedMessage = null;        // Deselect any active message
        SelectedNode = null;           // Deselect any active node
        Log.Information("Message history and topic tree cleared.");
        ShowStatus("Message history and topic tree cleared.");
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
                    DisplayHelpInformation(command.Arguments.FirstOrDefault()); // Pass the potential command name
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
                case CommandType.ViewRaw:
                    SwitchPayloadView(PayloadViewType.Raw);
                    break;
                case CommandType.ViewJson:
                    SwitchPayloadView(PayloadViewType.Json);
                    break;
                case CommandType.ViewImage:
                    SwitchPayloadView(PayloadViewType.Image);
                    break;
                case CommandType.SetUser:
                    if (command.Arguments.Count == 1)
                    {
                        this.Settings.AuthUsername = command.Arguments[0];
                        if (this.Settings.SelectedAuthMode == SettingsViewModel.AuthModeSelection.Anonymous)
                        {
                            this.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
                            StatusBarText = "Username set. Auth mode switched to Username/Password. Settings will be saved.";
                            Log.Information("Username set via command: {Username}. Auth mode switched to UsernamePassword.", command.Arguments[0]);
                        }
                        else
                        {
                            StatusBarText = "Username set. Settings will be saved.";
                            Log.Information("Username set via command: {Username}", command.Arguments[0]);
                        }
                    }
                    else
                    {
                        StatusBarText = "Error: :setuser requires exactly one argument <username>.";
                        Log.Warning("Invalid arguments for SetUser command.");
                    }
                    break;
                case CommandType.SetPassword:
                    if (command.Arguments.Count == 1)
                    {
                        this.Settings.AuthPassword = command.Arguments[0];
                        if (this.Settings.SelectedAuthMode == SettingsViewModel.AuthModeSelection.Anonymous)
                        {
                            this.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
                            StatusBarText = "Password set. Auth mode switched to Username/Password. Settings will be saved.";
                            Log.Information("Password set via command. Auth mode switched to UsernamePassword."); // Avoid logging password
                        }
                        else
                        {
                            StatusBarText = "Password set. Settings will be saved.";
                            Log.Information("Password set via command."); // Avoid logging password
                        }
                    }
                    else
                    {
                        StatusBarText = "Error: :setpass requires exactly one argument <password>.";
                        Log.Warning("Invalid arguments for SetPassword command.");
                    }
                    break;
                case CommandType.SetAuthMode:
                    if (command.Arguments.Count == 1)
                    {
                        string mode = command.Arguments[0].ToLowerInvariant();
                        if (mode == "anonymous")
                        {
                            this.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
                            StatusBarText = "Authentication mode set to Anonymous. Settings will be saved.";
                            Log.Information("Authentication mode set to Anonymous via command.");
                        }
                        else if (mode == "userpass")
                        {
                            this.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
                            StatusBarText = "Authentication mode set to Username/Password. Settings will be saved.";
                            Log.Information("Authentication mode set to Username/Password via command.");
                            if (string.IsNullOrEmpty(this.Settings.AuthUsername))
                            {
                                StatusBarText += " Please set a username using :setuser.";
                                Log.Information("AuthUsername is currently empty.");
                            }
                        }
                        else if (mode == "enhanced")
                        {
                            this.Settings.AuthenticationMethod = "Enhanced Authentication";
                            StatusBarText = "Authentication method set to 'Enhanced Authentication'. Settings will be saved.";
                            Log.Information("Authentication method set to 'Enhanced Authentication' via command.");
                        }
                        else
                        {
                            // This case should ideally be caught by CommandParserService, but good for robustness
                            StatusBarText = "Error: Invalid argument for :setauthmode. Expected <anonymous|userpass|enhanced>.";
                            Log.Warning("Invalid argument for SetAuthMode command: {Argument}", command.Arguments[0]);
                        }
                    }
                    else
                    {
                        StatusBarText = "Error: :setauthmode requires exactly one argument <anonymous|userpass|enhanced>.";
                        Log.Warning("Invalid arguments for SetAuthMode command.");
                    }
                    break;
                case CommandType.Settings:
                    OpenSettings();
                    break;
                case CommandType.SetAuthMethod:
                    if (command.Arguments.Count == 1)
                    {
                        this.Settings.AuthenticationMethod = command.Arguments[0];
                        StatusBarText = $"Authentication method set to '{command.Arguments[0]}'. Settings will be saved.";
                        Log.Information("Authentication method set via command: {Method}", command.Arguments[0]);
                    }
                    else
                    {
                        StatusBarText = "Error: :setauthmethod requires exactly one argument <method>.";
                        Log.Warning("Invalid arguments for SetAuthMethod command.");
                    }
                    break;
                case CommandType.SetAuthData:
                    if (command.Arguments.Count == 1)
                    {
                        this.Settings.AuthenticationData = command.Arguments[0];
                        StatusBarText = $"Authentication data set. Settings will be saved.";
                        Log.Information("Authentication data set via command.");
                    }
                    else
                    {
                        StatusBarText = "Error: :setauthdata requires exactly one argument <data>.";
                        Log.Warning("Invalid arguments for SetAuthData command.");
                    }
                    break;
                case CommandType.SetUseTls:
                    if (command.Arguments.Count == 1)
                    {
                        var arg = command.Arguments[0].ToLowerInvariant();
                        if (arg == "true" || arg == "false")
                        {
                            this.Settings.UseTls = arg == "true";
                            StatusBarText = $"TLS usage set to {arg}. Settings will be saved.";
                            Log.Information("TLS usage set via command: {Value}", arg);
                        }
                        else
                        {
                            StatusBarText = "Error: :setusetls requires argument <true|false>.";
                            Log.Warning("Invalid argument for SetUseTls command: {Argument}", arg);
                        }
                    }
                    else
                    {
                        StatusBarText = "Error: :setusetls requires exactly one argument <true|false>.";
                        Log.Warning("Invalid arguments for SetUseTls command.");
                    }
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

    // Dictionary to store command details (syntax and description)
    private static readonly Dictionary<string, (string Syntax, string Description)> CommandHelpDetails = new()
    {
        { "connect", (":connect [<server:port>]", "Connects to the specified MQTT broker or the one from settings.") },
        { "disconnect", (":disconnect", "Disconnects from the current MQTT broker.") },
        { "export", (":export <json|txt> <filepath>", "Exports the current message history to a file.") },
        { "filter", (":filter [pattern]", "Filters the topic tree by the given pattern. No pattern clears the filter.") },
        { "search", (":search [term]", "Filters the message list by the given term. No term clears the search.") },
        { "copy", (":copy", "Copies details of the selected message to the clipboard.") },
        { "clear", (":clear", "Clears the message history view.") },
        { "help", (":help [command]", "Shows general help or help for a specific command.") },
        { "pause", (":pause", "Pauses receiving new messages in the UI.") },
        { "resume", (":resume", "Resumes receiving new messages in the UI.") },
        { "expand", (":expand", "Expands all nodes in the topic tree.") },
        { "collapse", (":collapse", "Collapses all nodes in the topic tree.") },
        { "view", (":view <raw|json|image>", "Switches the payload view between raw text, JSON tree and image.") },
        { "setuser", (":setuser <username>", "Sets MQTT username. Switches to Username/Password auth if current mode is Anonymous.") },
        { "setpass", (":setpass <password>", "Sets MQTT password. Switches to Username/Password auth if current mode is Anonymous.") },
        { "setauthmode", (":setauthmode <anonymous|userpass|enhanced>", "Sets the MQTT authentication mode.") },
        { "setauthmethod", (":setauthmethod <method>", "Sets the authentication method for enhanced authentication (e.g., SCRAM-SHA-1, K8S-SAT).") },
        { "setauthdata", (":setauthdata <data>", "Sets the authentication data for enhanced authentication (method-specific data).") },
        { "setusetls", (":setusetls <true|false>", "Sets whether to use TLS for MQTT connections. true = enable TLS, false = disable TLS.") },
        { "settings", (":settings", "Toggles the visibility of the settings pane.") }
    };

    private void DisplayHelpInformation(string? commandName = null)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            // General help: List all commands
            var availableCommands = string.Join(", ", CommandHelpDetails.Keys.Select(k => $":{k}"));
            StatusBarText = $"Available commands: {availableCommands}. Type :help <command> for details.";
            Log.Information("Displaying general help information.");
        }
        else
        {
            // Specific command help
            var cleanCommandName = commandName.Trim().ToLowerInvariant().TrimStart(':'); // Clean up input
            if (CommandHelpDetails.TryGetValue(cleanCommandName, out var details))
            {
                StatusBarText = $"Help for :{cleanCommandName}: {details.Syntax} - {details.Description}";
                Log.Information("Displaying help for command: {CommandName}", cleanCommandName);
            }
            else
            {
                StatusBarText = $"Error: Unknown command '{commandName}'. Type :help for a list of commands.";
                Log.Warning("Help requested for unknown command: {CommandName}", commandName);
            }
        }
    }

    private void ClearMessageHistory()
    {
        StatusBarText = "Clearing history...";
        ClearHistoryCommand.Execute().Subscribe();
    }

    private void CopySelectedMessageDetails()
    {
        var selectedMsgVm = SelectedMessage; // Cache the selected message VM
        if (selectedMsgVm == null) // Check if VM itself is null first
        {
            StatusBarText = "No message selected to copy.";
            Log.Information("Copy command executed but no message was selected.");
            return; // Exit early
        }

        var msg = selectedMsgVm.GetFullMessage(); // Now call GetFullMessage on the non-null VM
        if (msg != null) // Check result of GetFullMessage
        {
            var textExporter = new TextExporter();
            // Use the cached selectedMsgVm for Timestamp
            (ClipboardText, _, _) = textExporter.GenerateDetailedTextFromMessage(msg, selectedMsgVm.Timestamp);
            StatusBarText = "Updated system clipboard with selected message";
            Log.Information("Copy command executed.");
        }
        else
        {
            // Handle case where GetFullMessage returned null (e.g., message expired from buffer)
            StatusBarText = "Could not retrieve full message details to copy.";
            Log.Warning("Copy command executed but failed to retrieve full message for {Topic} ID {MessageId}", selectedMsgVm.Topic, selectedMsgVm.MessageId);
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
        var selectedMsgVmNullable = SelectedMessage; // Cache locally
        if (selectedMsgVmNullable == null)
        {
            StatusBarText = "Error: No message selected to export.";
            Log.Warning("Export command failed: No message selected.");
            return;
        }
        var selectedMsgVm = selectedMsgVmNullable;
        var fullMessage = selectedMsgVm.GetFullMessage(); // Use method
        if (fullMessage == null)
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
        else if (format == "txt")
        {
            exporter = new TextExporter();
        }
        else
        {
            StatusBarText = $"Error: Invalid export format '{format}'. Use 'json' or 'txt'.";
            Log.Warning("Invalid export format specified: {Format}", format);
            return;
        }

        try
        {
            // Use the timestamp from the ViewModel as it represents arrival time
            // Use the fetched fullMessage and the cached selectedMsgVm for timestamp
            string? exportedFilePath = exporter.ExportToFile(fullMessage, selectedMsgVm.Timestamp, folderPath);

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
            // Update synchronously for testability and direct command feedback
            int dummyMatchCount = 0; // Need a variable for the ref parameter
            SetNodeVisibilityRecursive(TopicTreeNodes, isVisible: true, clearFilter: true, ref dummyMatchCount); // Pass the ref parameter
            StatusBarText = "Topic filter cleared.";
            IsTopicFilterActive = false; // Update filter state
            Log.Information("Topic filter cleared.");
            return;
        }

        Log.Information("Applying topic filter: '{Filter}'", filter);
        // Update synchronously for testability and direct command feedback
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
                nodeMatches = segments.Any(segment => !string.IsNullOrEmpty(segment) && Fuzz.PartialRatio(segment.ToLowerInvariant(), filter.ToLowerInvariant()) > 80); // Increased threshold
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
                Log.Verbose("Creating new node '{Part}' under parent '{ParentName}' with path '{FullPath}'", part, parentNode?.Name ?? "[Root]", currentPath);
                existingNode = new NodeViewModel(part, parentNode) { FullPath = currentPath }; // Pass parent and set full path

                // Insert the new node in sorted order instead of rebuilding the collection
                int insertIndex = 0;
                while (insertIndex < currentLevel.Count && string.Compare(currentLevel[insertIndex].Name, existingNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertIndex++;
                }
                currentLevel.Insert(insertIndex, existingNode);
            }
            else
            {
                Log.Verbose("Found existing node '{Part}' under parent '{ParentName}'", part, parentNode?.Name ?? "[Root]");
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
    /// Updates or creates a node in the topic tree structure and increments the message count by a specific amount.
    /// This is optimized for batch processing where multiple messages for the same topic are processed together.
    /// </summary>
    /// <param name="topic">The full MQTT topic string.</param>
    /// <param name="incrementBy">The number to increment the message count by.</param>
    private void UpdateOrCreateNodeWithCount(string topic, int incrementBy)
    {
        var parts = topic.Split('/');
        ObservableCollection<NodeViewModel> currentLevel = TopicTreeNodes;
        NodeViewModel? parentNode = null;

        string currentPath = "";

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part)) continue;

            currentPath = (i == 0) ? part : $"{currentPath}/{part}";

            var existingNode = currentLevel.FirstOrDefault(n => n.Name == part);

            if (existingNode == null)
            {
                Log.Verbose("Creating new node '{Part}' under parent '{ParentName}' with path '{FullPath}'", part, parentNode?.Name ?? "[Root]", currentPath);
                existingNode = new NodeViewModel(part, parentNode) { FullPath = currentPath };

                int insertIndex = 0;
                while (insertIndex < currentLevel.Count && string.Compare(currentLevel[insertIndex].Name, existingNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertIndex++;
                }
                currentLevel.Insert(insertIndex, existingNode);
            }

            // Increment count by specified amount only for the final node in the path
            if (i == parts.Length - 1 && incrementBy > 0)
            {
                for (int j = 0; j < incrementBy; j++)
                {
                    existingNode.IncrementMessageCount();
                }
            }

            currentLevel = existingNode.Children;
            parentNode = existingNode;
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

    private enum PayloadViewType { Raw, Json, Image }

    /// <summary>
    /// Switches the active payload viewer between JSON and Raw Text.
    /// </summary>
    /// <param name="viewType">The type of view to switch to.</param>
    private void SwitchPayloadView(PayloadViewType viewType)
    {
        if (SelectedMessage == null)
        {
            StatusBarText = "No message selected to view.";
            return;
        }

        IsRawTextViewerVisible = false;
        IsJsonViewerVisible = false;
        IsImageViewerVisible = false;

        switch (viewType)
        {
            case PayloadViewType.Raw:
                IsRawTextViewerVisible = true;
                StatusBarText = "Switched to Raw Text view.";
                Log.Information("Switched payload view to Raw Text.");
                break;
            case PayloadViewType.Json:
                if (string.IsNullOrEmpty(JsonViewer.JsonParseError))
                {
                    IsJsonViewerVisible = true;
                    StatusBarText = "Switched to JSON Tree view.";
                    Log.Information("Switched payload view to JSON Tree.");
                }
                else
                {
                    IsRawTextViewerVisible = true; // Fallback to raw view
                    StatusBarText = $"Cannot switch to JSON view: {JsonViewer.JsonParseError}";
                    Log.Warning("Attempted to switch to JSON view, but JSON parsing failed.");
                }
                break;
            case PayloadViewType.Image:
                if (ImagePayload != null)
                {
                    IsImageViewerVisible = true;
                    StatusBarText = "Switched to Image view.";
                    Log.Information("Switched payload view to Image.");
                }
                else
                {
                    IsRawTextViewerVisible = true; // Fallback to raw view
                    StatusBarText = "Cannot switch to Image view: No valid image loaded for this message.";
                    Log.Warning("Attempted to switch to Image view, but no image is loaded.");
                }
                break;
        }

        this.RaisePropertyChanged(nameof(ShowJsonParseError)); // Notify computed property change
        this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible)); // Notify computed property change
    }

    // --- Helper Methods ---

    /// <summary>
    /// Copies the full payload of the given message to the system clipboard using an Interaction.
    /// </summary>
    private async Task CopyPayloadToClipboardAsync(object? param) // Parameter changed to object?
    {
        MessageViewModel? messageVm = param as MessageViewModel;

        if (messageVm == null)
        {
            Log.Debug("CopyPayloadToClipboardAsync called with null or invalid parameter. Param: {@Param}", param);
            StatusBarText = "Error: No message selected or invalid parameter to copy payload from.";
            return;
        }

        var msg = messageVm.GetFullMessage(); // Use method, no longer nullable due to check above
        if (msg?.Payload == null)
        {
            StatusBarText = "Cannot copy: Message or payload is missing.";
            Log.Warning("CopyPayloadCommand failed: FullMessage or Payload was null for MessageId {MessageId}.", messageVm.MessageId);
            return;
        }

        string payloadString;
        try
        {
            // Use the fetched fullMessage
            payloadString = Encoding.UTF8.GetString(msg.Payload);
        }
        catch (Exception ex)
        {
            StatusBarText = "Error decoding payload for clipboard.";
            Log.Error(ex, "Failed to decode payload to UTF8 string for clipboard copy for MessageId {MessageId}.", messageVm.MessageId);
            return;
        }

        try
        {
            // Invoke the interaction to request the View to copy the text
            await CopyTextToClipboardInteraction.Handle(payloadString);
            StatusBarText = "Payload copied to clipboard."; // Assume success if Handle doesn't throw
            Log.Information("CopyTextToClipboardInteraction handled for topic '{Topic}' (MessageId {MessageId}).", msg.Topic, messageVm.MessageId);
        }
        catch (Exception ex) // Catch potential exceptions from the interaction handler
        {
            StatusBarText = $"Error copying to clipboard: {ex.Message}";
            Log.Error(ex, "Exception occurred during CopyTextToClipboardInteraction handling for MessageId {MessageId}.", messageVm.MessageId);
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
                
                // Dispose batch processing timer and clear pending messages
                lock (_batchLock)
                {
                    _batchProcessingTimer?.Dispose();
                    _batchProcessingTimer = null;
                    _pendingMessages.Clear();
                }
                
                _messageHistorySubscription?.Dispose();
                _globalHookSubscription?.Dispose(); // Dispose hook subscription
                try
                {
                    _globalHook?.Dispose(); // Dispose the hook itself
                }
                catch
                {
                    
                }
                // MqttEngine's Dispose method now handles the final disconnect attempt.
                // We rely on _cts.Cancel() being called first, then _mqttEngine.Dispose() below.
                // Removed explicit synchronous DisconnectAsync call here.
                // Dispose the MqttService instance if it implements IDisposable
                    if (_mqttService is IDisposable disposableMqttService)
                {
                    disposableMqttService.Dispose();
                }
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

    // --- IStatusBarService Implementation ---

    private CancellationTokenSource? _statusClearCts;

    /// <summary>
    /// Shows a status message in the status bar, optionally clearing it after a duration.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="duration">Optional duration after which the message will be cleared. If null, the message persists.</param>
    public void ShowStatus(string message, TimeSpan? duration = null)
    {
        // Ensure execution on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            StatusBarText = message;
            Log.Debug("Status Bar Updated: {StatusMessage}", message);

            // Cancel any previous timer
            _statusClearCts?.Cancel();
            _statusClearCts?.Dispose();
            _statusClearCts = null;

            if (duration.HasValue)
            {
                _statusClearCts = new CancellationTokenSource();
                var token = _statusClearCts.Token;

                Task.Delay(duration.Value, token).ContinueWith(t =>
                {
                    // Check if cancellation was requested or if the current text is still the one we set
                    if (!t.IsCanceled && StatusBarText == message)
                    {
                        Dispatcher.UIThread.Post(() => StatusBarText = "Ready"); // Reset to default
                    }
                }, TaskScheduler.Default); // Continue on a background thread is fine, the action posts back to UI thread
            }
        });
    }
} // Closing brace for MainViewModel class moved here
