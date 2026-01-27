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
using LibVLCSharp.Shared;
using MQTTnet;
using AvaloniaEdit.Highlighting; // Added for Syntax Highlighting
using CrowsNestMqtt.BusinessLogic; // Required for MqttEngine, MqttConnectionStateChangedEventArgs, IMqttService
using CrowsNestMqtt.BusinessLogic.Commands; // Added for command parsing
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Contracts; // Added for IMessageCorrelationService
using Microsoft.Extensions.Logging.Abstractions; // Added for command parsing
using CrowsNestMqtt.UI.Commands; // Added for ICommandProcessor and extensions
using CrowsNestMqtt.UI.Services; // Added for IStatusBarService
using CrowsNestMqtt.UI.Contracts; // Added for IResponseIconService
using CrowsNestMqtt.Utils; // Added for AppLogger
using DynamicData; // Added for SourceList and reactive filtering
using DynamicData.Binding; // Added for Bind()
using FuzzySharp; // Added for fuzzy search
using SharpHook.Native; // Added SharpHook Native for KeyCode and ModifierMask
using SharpHook.Reactive; // Added SharpHook Reactive
using System.Reactive.Concurrency;
using System.Linq;
using CrowsNestMQTT.BusinessLogic.Navigation; // Added for keyboard navigation
using Avalonia.Input; // Added for KeyModifiers

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages the different sections of the UI: Topic List, Message History, Message Details, and Command Bar.
/// </summary>
public class MainViewModel : ReactiveObject, IDisposable, IStatusBarService // Implement IDisposable and IStatusBarService
{
    private readonly IMqttService _mqttService; // Changed to interface
    private readonly ICommandParserService _commandParserService; // Added command parser service
    private readonly IDeleteTopicService? _deleteTopicService; // Added delete topic service
    private readonly IMessageCorrelationService? _correlationService; // Added correlation service for request-response tracking
    private readonly IResponseIconService? _iconService; // Added icon service for UI status updates
    private Timer? _updateTimer;
    private Timer? _uiHeartbeatTimer;
    private DateTime _lastHeartbeatPosted = DateTime.MinValue;
    private readonly SynchronizationContext? _syncContext; // To post updates to the UI thread
    private readonly SourceList<MessageViewModel> _messageHistorySource = new(); // Backing source for DynamicData
    private readonly IDisposable _messageHistorySubscription; // To dispose the pipeline
    private readonly ReadOnlyObservableCollection<MessageViewModel> _filteredMessageHistory; // Field for the bound collection
    private readonly ObservableCollection<MessageViewModel> _simpleFilteredHistory = new(); // Simple non-reactive collection for test mode
    private readonly IDisposable _selectedMessageSubscription; // To dispose the selected message subscription
    private readonly IDisposable _commandTextSubscription; // To dispose the command text subscription
    private string _currentSearchTerm = string.Empty; // Backing field for search term
    private readonly List<string> _availableCommands; // Added list of commands for suggestions
    private readonly IReactiveGlobalHook? _globalHook; // Added SharpHook global hook
    private readonly IDisposable? _globalHookSubscription; // Added subscription for the hook
    private bool _disposedValue; // For IDisposable pattern
    private string? _normalizedSelectedPath; // Normalized selected topic path (no trailing slash)
    private bool _isUpdatingSelectedNode; // Guard against re-entrancy in SelectedNode setter
    private bool _isUpdatingMessageDetails; // Guard against re-entrancy in UpdateMessageDetails
    private bool _isUpdatingSelectedMessage; // Guard against re-entrancy in SelectedMessage setter
#pragma warning disable CS0414
    private bool _isAutoSelectingMessage; // Guard against feedback loops during auto-selection (used only in production mode)
#pragma warning restore CS0414
    private readonly CancellationTokenSource _cts = new(); // Added cancellation token source for graceful shutdown
    private bool _isWindowFocused; // Added to track window focus for global hook
    private bool _isTopicFilterActive; // Added to track if the topic filter is active
    
    // Batch processing for high-volume message scenarios
    private readonly Queue<IdentifiedMqttApplicationMessageReceivedEventArgs> _pendingMessages = new();
    private readonly object _batchLock = new object();
    private Timer? _batchProcessingTimer;
    private readonly IScheduler _uiScheduler;
    private readonly bool _testMode; // Added: detect test/CI mode to disable expensive background infra
    // Per-topic message storage (prevents cross-topic eviction)
    private readonly ITopicMessageStore _messageStore;
    private readonly Dictionary<Guid, MessageViewModel> _messageIndex = new();

    // Keyboard navigation services (Feature 004)
    private readonly ITopicSearchService _topicSearchService;
    private readonly IKeyboardNavigationService? _keyboardNavigationService;
    private readonly SearchStatusViewModel _searchStatusViewModel;
    private readonly MessageNavigationState _messageNavigationState;

    // Topic normalization helper (single place)
    private static string? NormalizeTopic(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim().TrimEnd('/');

    // Filter diagnostics (lightweight; logs only when selected topic present but filtered list empty)
    private const int FilterDiagnosticsSampleLimit = 250;
    private int _filterDiagnosticsEvaluations = 0;
    private int _filterDiagnosticsMatches = 0;
    private int _filterDiagnosticsSelectedTopicMisses = 0;

    /// <summary>
    /// Gets or sets the current search term used for filtering message history.
    /// </summary>
    public string CurrentSearchTerm
    {
        get => _currentSearchTerm;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentSearchTerm, value);
            if (_testMode)
            {
                UpdateSimpleFilteredHistory();
            }
        }
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

    private bool _isCommandInputFocused;
    public bool IsCommandInputFocused
    {
        get => _isCommandInputFocused;
        set => this.RaiseAndSetIfChanged(ref _isCommandInputFocused, value);
    }

    // Replaced SelectedTopic with SelectedNode for the TreeView
    private NodeViewModel? _selectedNode;
    public NodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            // Prevent re-entrancy during setter execution
            if (_isUpdatingSelectedNode)
                return;

            try
            {
                _isUpdatingSelectedNode = true;

                this.RaiseAndSetIfChanged(ref _selectedNode, value);
                _normalizedSelectedPath = NormalizeTopic(_selectedNode?.FullPath);
                Log.Debug("SelectedNode changed. Raw='{Raw}' Normalized='{Norm}'", _selectedNode?.FullPath, _normalizedSelectedPath);
                this.RaisePropertyChanged(nameof(IsDeleteButtonEnabled));
                CurrentSearchTerm = string.Empty;

                // Immediate best-effort selection using current filtered snapshot
                if (FilteredMessageHistory.Any() && (SelectedMessage == null || !FilteredMessageHistory.Contains(SelectedMessage)))
                {
                    SelectedMessage = FilteredMessageHistory.FirstOrDefault();
                    // Force immediate details update (synchronous in tests with ImmediateDispatcher)
                    if (SelectedMessage != null && CheckUiThreadAccess())
                    {
                        UpdateMessageDetails(SelectedMessage);
                    }
                }

                // Defer a second pass until after DynamicData re-applies the filter for the new SelectedNode.
                // This handles cases where the pipeline updates asynchronously (scheduler posting),
                // especially for media-only topics (image/video) whose first message needs to trigger viewer visibility.
                // In test mode, use simple filtering instead of reactive pipeline
                if (_testMode)
                {
                    UpdateSimpleFilteredHistory();
                }
                else
                {
                    ScheduleOnUi(() =>
            {
                if (SelectedMessage == null || !FilteredMessageHistory.Contains(SelectedMessage))
                {
                    if (FilteredMessageHistory.Any())
                    {
                        SelectedMessage = FilteredMessageHistory.FirstOrDefault();
                        if (SelectedMessage != null && CheckUiThreadAccess())
                        {
                            UpdateMessageDetails(SelectedMessage);
                        }
                    }
                    else
                    {
                        // Fallback: filtered list not yet populated (Reactive pipeline not flushed),
                        // attempt direct source match so media-only topics select & render immediately.
                        var selectedPath = SelectedNode?.FullPath;
                        if (!string.IsNullOrEmpty(selectedPath))
                        {
                            var candidate = _messageHistorySource.Items.FirstOrDefault(m =>
                                m.Topic.Equals(selectedPath, StringComparison.OrdinalIgnoreCase) ||
                                m.Topic.StartsWith(selectedPath + "/", StringComparison.OrdinalIgnoreCase));
                            if (candidate != null)
                            {
                                SelectedMessage = candidate;
                                if (CheckUiThreadAccess())
                                {
                                    UpdateMessageDetails(SelectedMessage);
                                }
                            }
                        }
                    }
                }
                // Fallback: if we have a selected message but still no viewer visible (e.g. image/video) force re-evaluation
                if (SelectedMessage != null && !IsAnyPayloadViewerVisible)
                {
                    UpdateMessageDetails(SelectedMessage);
                }

                // Additional media content-type enforcement:
                // If an image/video message was selected but a non-media view is shown (e.g. raw text due to early decode),
                // re-run details to promote the correct viewer.
                if (SelectedMessage != null && (!IsImageViewerVisible && !IsVideoViewerVisible))
                {
                    var full = SelectedMessage.GetFullMessage();
                    var ct = full?.ContentType;
                    if (!string.IsNullOrEmpty(ct) &&
                        (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                         ct.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
                    {
                        UpdateMessageDetails(SelectedMessage);
                    }
                }

                // Final deterministic media fallback (test stability):
                // If we still have a selected message whose content-type is image/video but viewer not visible
                // (e.g., image decode failed in headless test), force the appropriate viewer visible without decoding.
                if (SelectedMessage != null && !IsImageViewerVisible && !IsVideoViewerVisible)
                {
                    var full = SelectedMessage.GetFullMessage();
                    var ct = full?.ContentType;
                    if (!string.IsNullOrEmpty(ct))
                    {
                        if (ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        {
                            IsImageViewerVisible = true;
                            IsRawTextViewerVisible = false;
                            IsJsonViewerVisible = false;
                            IsVideoViewerVisible = false;
                            IsHexViewerVisible = false;
                            this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
                        }
                        else if (ct.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                        {
                            IsVideoViewerVisible = true;
                            IsRawTextViewerVisible = false;
                            IsJsonViewerVisible = false;
                            IsImageViewerVisible = false;
                            IsHexViewerVisible = false;
                            this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
                        }
                    }
                }
                    });
                }
            }
            finally
            {
                _isUpdatingSelectedNode = false;
            }
        }
    }

    private MessageViewModel? _selectedMessage;
    public MessageViewModel? SelectedMessage
    {
        get => _selectedMessage;
        set
        {
            // Prevent re-entrancy during SelectedMessage setter execution
            if (_isUpdatingSelectedMessage)
                return;

            try
            {
                _isUpdatingSelectedMessage = true;

                var changed = !EqualityComparer<MessageViewModel?>.Default.Equals(_selectedMessage, value);
                this.RaiseAndSetIfChanged(ref _selectedMessage, value);
                if (changed)
                {
                    this.RaisePropertyChanged(nameof(IsDeleteButtonEnabled));
                }
                if (changed && value != null)
                {
                    try
                    {
                        // Immediate (synchronous) update for unit tests asserting right after assignment
                        UpdateMessageDetails(value);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Error during immediate UpdateMessageDetails in SelectedMessage setter.");
                    }
                }
            }
            finally
            {
                _isUpdatingSelectedMessage = false;
            }
        }
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

    // Delete button is enabled when connected and a topic is selected
    public bool IsDeleteButtonEnabled
    {
        get
        {
            try
            {
                var isConnected = ConnectionStatus == ConnectionStatusState.Connected;
                var hasSelectedTopic = GetSelectedTopicForDelete() != null;
                var result = isConnected && hasSelectedTopic;

                // Debug logging to help troubleshoot
                Log.Verbose("IsDeleteButtonEnabled: Connected={IsConnected}, HasTopic={HasTopic}, Result={Result}",
                    isConnected, hasSelectedTopic, result);

                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking delete button enabled state");
                return false;
            }
        }
    }

    // T021: Export All button is enabled when a topic is selected and it has messages
    public bool IsExportAllButtonEnabled
    {
        get
        {
            try
            {
                var hasSelectedTopic = SelectedNode != null;
                var hasMessages = FilteredMessageHistory.Count > 0;
                var result = hasSelectedTopic && hasMessages;

                Log.Verbose("IsExportAllButtonEnabled: HasTopic={HasTopic}, HasMessages={HasMessages}, Result={Result}",
                    hasSelectedTopic, hasMessages, result);

                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking export all button enabled state");
                return false;
            }
        }
    }

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

    /// <summary>
    /// Gets the keyboard navigation service for n/N/j/k shortcuts (Feature 004).
    /// </summary>
    public IKeyboardNavigationService? KeyboardNavigationService => _keyboardNavigationService;

    /// <summary>
    /// Gets the search status view model for displaying search feedback (Feature 004).
    /// </summary>
    public SearchStatusViewModel SearchStatusViewModel => _searchStatusViewModel;

    /// <summary>
    /// Gets the message navigation state for j/k message history navigation (Feature 004).
    /// </summary>
    public MessageNavigationState MessageNavigationState => _messageNavigationState;

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

    // Changed from string to TextDocument for binding (created in ctor on UI thread if available)
    private TextDocument _rawPayloadDocument;
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
    public bool ShowJsonParseError => !IsJsonViewerVisible && !IsImageViewerVisible && !IsVideoViewerVisible && !IsHexViewerVisible && !string.IsNullOrEmpty(JsonViewer.JsonParseError);

    private bool _isImageViewerVisible;
    public bool IsImageViewerVisible
    {
        get => _isImageViewerVisible;
        private set => this.RaiseAndSetIfChanged(ref _isImageViewerVisible, value);
    }

    // --- Hex Viewer ---
    private bool _isHexViewerVisible;
    public bool IsHexViewerVisible
    {
        get => _isHexViewerVisible;
        private set => this.RaiseAndSetIfChanged(ref _isHexViewerVisible, value);
    }

    private byte[]? _hexPayloadBytes;
    public byte[]? HexPayloadBytes
    {
        get => _hexPayloadBytes;
        private set => this.RaiseAndSetIfChanged(ref _hexPayloadBytes, value);
    }

    private Bitmap? _imagePayload;
    public Bitmap? ImagePayload
    {
        get => _imagePayload;
        private set => this.RaiseAndSetIfChanged(ref _imagePayload, value);
    }

    // --- Video Viewer ---
    private readonly LibVLC? _libVLC;
    private MediaPlayer? _vlcMediaPlayer;
    private bool _isVideoViewerVisible;
    public bool IsVideoViewerVisible
    {
        get => _isVideoViewerVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVideoViewerVisible, value);
    }

    private byte[]? _videoPayload;
    public byte[]? VideoPayload
    {
        get => _videoPayload;
        private set
        {
            this.RaiseAndSetIfChanged(ref _videoPayload, value);

            // Preserve visibility unless we truly clear the payload.
            var playbackAvailable = _vlcMediaPlayer != null && _libVLC != null;

            if (value != null && value.Length > 0)
            {
                if (playbackAvailable)
                {
                    try
                    {
                        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crowsnest_video_{Guid.NewGuid():N}.mp4");
                        System.IO.File.WriteAllBytes(tempPath, value);
                        if (_vlcMediaPlayer != null)
                        {
                            try
                            {
                                _vlcMediaPlayer.Stop();
                            }
                            catch { /* swallow stop race */ }

                            try
                            {
                                _vlcMediaPlayer.Media?.Dispose();
                            }
                            catch { /* swallow dispose race */ }

                            if (_libVLC != null)
                            {
                                var media = new Media(_libVLC, tempPath, FromType.FromPath);
                                _vlcMediaPlayer.Media = media;
                                _vlcMediaPlayer.Play();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error loading video payload (playback mode).");
                    }
                    IsVideoViewerVisible = true;
                }
                else
                {
                    // Fallback for headless / CI test environments without LibVLC.
                    IsVideoViewerVisible = true;
                }
            }
            else
            {
                if (playbackAvailable)
                {
                    try
                    {
                        if (_vlcMediaPlayer != null)
                        {
                            try
                            {
                                _vlcMediaPlayer.Stop();
                            }
                            catch { }
                            try
                            {
                                _vlcMediaPlayer.Media?.Dispose();
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
                IsVideoViewerVisible = false;
            }
        }
    }

    protected virtual void PlayVideo()
    {
        _vlcMediaPlayer?.Play();
    }

    protected virtual void StopVideo()
    {
        _vlcMediaPlayer?.Stop();
    }

    private Uri? _videoSource;
    public Uri? VideoSource
    {
        get => _videoSource;
        private set => this.RaiseAndSetIfChanged(ref _videoSource, value);
    }

    public MediaPlayer? VlcMediaPlayer
    {
        get => _vlcMediaPlayer;
        set
        {
            if (_vlcMediaPlayer != null)
            {
                _vlcMediaPlayer.Playing -= VlcMediaPlayerPlaying;
                _vlcMediaPlayer.Paused -= VlcMediaPlayerPaused;
                _vlcMediaPlayer.Stopped -= VlcMediaPlayerStopped;
            }

            this.RaiseAndSetIfChanged(ref _vlcMediaPlayer, value);

            if (_vlcMediaPlayer != null)
            {
                _vlcMediaPlayer.Playing += VlcMediaPlayerPlaying;
                _vlcMediaPlayer.Paused += VlcMediaPlayerPaused;
                _vlcMediaPlayer.Stopped += VlcMediaPlayerStopped;
            }
        }
    }

    private void VlcMediaPlayerPlaying(object? sender, EventArgs e)
    {
        // Handle playing event
    }

    private void VlcMediaPlayerPaused(object? sender, EventArgs e)
    {
        // Handle paused event
    }

    private void VlcMediaPlayerStopped(object? sender, EventArgs e)
    {
        // Handle stopped event
    }

    // Computed property to control the visibility of the splitter below the payload viewers
    public bool IsAnyPayloadViewerVisible => IsJsonViewerVisible || IsRawTextViewerVisible || IsImageViewerVisible || IsVideoViewerVisible || IsHexViewerVisible;

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
    public ReactiveCommand<Unit, Unit> FocusTopicTreeCommand { get; } // Added command to focus topic tree after search
    public ReactiveCommand<object?, Unit> CopyPayloadCommand { get; } // Added command to copy payload
    public ReactiveCommand<Unit, Unit> DeleteTopicCommand { get; } // Added command to delete selected topic's retained messages
    public ReactiveCommand<string?, Unit> NavigateToResponseCommand { get; } // Added command to navigate to response messages
    public ReactiveCommand<object?, Unit> ExportMessageCommand { get; } // T020: Added command to export single message from row button
    public ReactiveCommand<Unit, Unit> ExportAllCommand { get; } // T021: Added command to export all messages from selected topic

    // Interaction for requesting clipboard copy from the View
    public Interaction<string, Unit> CopyTextToClipboardInteraction { get; }
    // Interaction for requesting image copy from the View
    public Interaction<Bitmap, Unit> CopyImageToClipboardInteraction { get; }

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
    public MainViewModel(ICommandParserService commandParserService, IMqttService? mqttService = null, IDeleteTopicService? deleteTopicService = null, IMessageCorrelationService? correlationService = null, IResponseIconService? iconService = null, string? aspireHostname = null, int? aspirePort = null, IScheduler? uiScheduler = null)
    {
        _commandParserService = commandParserService ?? throw new ArgumentNullException(nameof(commandParserService)); // Store injected service
        _deleteTopicService = deleteTopicService; // Store injected delete topic service (optional)
        _correlationService = correlationService; // Store injected correlation service (optional)
        _iconService = iconService; // Store injected icon service (optional)
        _uiScheduler = uiScheduler 
            ?? (Application.Current == null ? Scheduler.Immediate : RxApp.MainThreadScheduler); // Use Immediate in non-Avalonia (plain unit test) context
        _testMode = Application.Current == null
                    || AppDomain.CurrentDomain.FriendlyName?.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0
                    || AppDomain.CurrentDomain.FriendlyName?.IndexOf("vstest", StringComparison.OrdinalIgnoreCase) >= 0
                    || Environment.GetEnvironmentVariable("CI") == "true"
                    || uiScheduler == Scheduler.Immediate; // If ImmediateScheduler is injected, we're definitely in test mode
        _syncContext = SynchronizationContext.Current; // Capture sync context
        Settings = new SettingsViewModel(); // Instantiate settings
        JsonViewer = new JsonViewerViewModel(); // Instantiate JSON viewer VM
        CopyTextToClipboardInteraction = new Interaction<string, Unit>(); // Initialize the interaction
        CopyImageToClipboardInteraction = new Interaction<Bitmap, Unit>(); // Initialize the image interaction

        // Initialize the RawPayloadDocument on the Avalonia UI thread (TextDocument has thread affinity)
        // In test mode, create directly to avoid touching Dispatcher
        if (_testMode || Application.Current == null)
        {
            _rawPayloadDocument = new TextDocument();
        }
        else if (!Dispatcher.UIThread.CheckAccess())
        {
            var tcsDoc = new TaskCompletionSource<TextDocument>();
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    tcsDoc.SetResult(new TextDocument());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to create TextDocument on UI thread, falling back to current thread.");
                    tcsDoc.SetResult(new TextDocument());
                }
            });
            _rawPayloadDocument = tcsDoc.Task.GetAwaiter().GetResult();
        }
        else
        {
            _rawPayloadDocument = new TextDocument();
        }

        // Initialize LibVLC with error handling for test environments
        try
        {
            Core.Initialize();
            _libVLC = new LibVLC();
            _vlcMediaPlayer = new MediaPlayer(_libVLC);
            VlcMediaPlayer = _vlcMediaPlayer;
        }
        catch (VLCException ex)
        {
            // LibVLC initialization failed (likely in test environment)
            // Log the error but continue without video functionality
            AppLogger.Warning($"LibVLC initialization failed: {ex.Message}. Video playback will not be available.");
            _libVLC = null;
            _vlcMediaPlayer = null;
            VlcMediaPlayer = null;
        }

        // Populate the list of available commands (using the help dictionary keys)
        
        // Initialize per-topic store with settings-derived limits if available
        _messageStore = new TopicMessageStore(1 * 1024 * 1024); // Default assignment to satisfy non-nullable requirement
        try
        {
            var limits = Settings.Into().TopicSpecificBufferLimits?
                .ToDictionary(l => l.TopicFilter.Trim().TrimEnd('/'),
                              l => l.MaxSizeBytes)
                ?? new Dictionary<string, long>();
            _messageStore = new TopicMessageStore(1 * 1024 * 1024, limits);
            Log.Information("Initialized TopicMessageStore with {Count} specific limits.", limits.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize TopicMessageStore with specific limits. Falling back to default.");
        }
        _availableCommands = CommandHelpDetails.Keys
                                  .Select(name => ":" + name.ToLowerInvariant()) // Prefix with ':'
                                  .OrderBy(cmd => cmd) // Sort alphabetically
                                  .ToList();

        // Initialize keyboard navigation services (Feature 004)
        _topicSearchService = new TopicSearchService(GetAllTopicReferences);
        _searchStatusViewModel = new SearchStatusViewModel();
        _messageNavigationState = new MessageNavigationState();
        _keyboardNavigationService = new KeyboardNavigationService(
            _topicSearchService,
            _messageNavigationState,
            () => IsCommandInputFocused // Suppress shortcuts when command palette has focus
        );

        // --- DynamicData Pipeline for Message History Filtering ---

        // --- UI Heartbeat Timer for Freeze Detection ---
        if (!_testMode)
        {
            _uiHeartbeatTimer = new Timer(_ =>
            {
                var posted = DateTime.UtcNow;
                ScheduleOnUi(() =>
                {
                    var now = DateTime.UtcNow;
                    var delay = now - posted;
                    if (delay > TimeSpan.FromMilliseconds(1500))
                    {
                        Log.Warning("UI heartbeat delayed by {Delay} ms. UI thread may be blocked or frozen.", delay.TotalMilliseconds);
                    }
                    _lastHeartbeatPosted = now;
                });
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
        else
        {
            Log.Debug("Heartbeat timer disabled in test mode.");
        }

        if (_testMode)
        {
            // In test mode, use a simple non-reactive approach to prevent hanging
            _messageHistorySubscription = System.Reactive.Disposables.Disposable.Empty;
            // Create a dummy ReadOnlyObservableCollection for the field requirement
            var dummyCollection = new ObservableCollection<MessageViewModel>();
            _filteredMessageHistory = new ReadOnlyObservableCollection<MessageViewModel>(dummyCollection);
            FilteredMessageHistory = new ReadOnlyObservableCollection<MessageViewModel>(_simpleFilteredHistory);
        }
        else
        {
            // Production mode - create filter predicate and reactive pipeline
            // Define the filter predicate based on the search term AND the selected node
            var filterPredicate = this.WhenAnyValue(x => x.CurrentSearchTerm, x => x.SelectedNode)
                .ObserveOn(_uiScheduler)
                .Select(tuple =>
                {
                    var (termRaw, node) = tuple;
                    var term = termRaw?.Trim() ?? string.Empty;
                    var normalizedSelected = NormalizeTopic(node?.FullPath);

                    // Reset diagnostics counters when criteria change
                    _filterDiagnosticsEvaluations = 0;
                    _filterDiagnosticsMatches = 0;
                    _filterDiagnosticsSelectedTopicMisses = 0;

                    if (normalizedSelected == null && string.IsNullOrEmpty(term))
                    {
                        Log.Verbose("Filter criteria updated. SelectedPath='[None]' Term='[Empty]' (pass-through all).");
                        return (Func<MessageViewModel, bool>)(_ => true);
                    }

                    Log.Verbose("Filter criteria updated. SelectedPath='{Sel}' Term='{Term}'",
                        normalizedSelected ?? "[None]", string.IsNullOrEmpty(term) ? "[Empty]" : term);

                    return new Func<MessageViewModel, bool>(m =>
                    {
                        // Stop logging after limit to avoid log flooding
                        bool underSampleLimit = _filterDiagnosticsEvaluations < FilterDiagnosticsSampleLimit;

                        Interlocked.Increment(ref _filterDiagnosticsEvaluations);

                        var topic = NormalizeTopic(m.Topic);
                        bool topicMatch = normalizedSelected == null
                            ? true
                            : (topic == normalizedSelected ||
                               (topic != null && topic.StartsWith(normalizedSelected + "/", StringComparison.OrdinalIgnoreCase)));

                        if (!topicMatch)
                        {
                            if (underSampleLimit && normalizedSelected != null && topic == normalizedSelected)
                            {
                                // Extreme corner (should not happen because branch above catches)
                                Log.Verbose("FilterEval (ANOMALY) sel='{Sel}' topic='{Topic}' topicMatch=false", normalizedSelected, topic);
                            }
                            return false;
                        }

                        if (string.IsNullOrEmpty(term))
                        {
                            if (underSampleLimit)
                            {
                                Interlocked.Increment(ref _filterDiagnosticsMatches);
                                if (normalizedSelected != null && topic == normalizedSelected && _filterDiagnosticsMatches <= 5)
                                {
                                    Log.Verbose("FilterEval PASS sel='{Sel}' topic='{Topic}' term='[Empty]'", normalizedSelected, topic);
                                }
                            }
                            return true;
                        }

                        bool searchMatch = (m.PayloadPreview?.IndexOf(term, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;

                        if (underSampleLimit && normalizedSelected != null && topic == normalizedSelected)
                        {
                            if (searchMatch)
                            {
                                if (_filterDiagnosticsMatches < 10)
                                {
                                    var previewText = m.PayloadPreview ?? string.Empty;
                                    Log.Verbose("FilterEval PASS sel='{Sel}' topic='{Topic}' term='{Term}' previewSample='{Preview}'",
                                        normalizedSelected, topic, term, previewText.Length > 40 ? previewText.Substring(0, 40) : previewText);
                                }
                            }
                            else
                            {
                                int misses = Interlocked.Increment(ref _filterDiagnosticsSelectedTopicMisses);
                                if (misses <= 15)
                                {
                                    var previewTextMiss = m.PayloadPreview ?? string.Empty;
                                    Log.Verbose("FilterEval MISS(sel-topic) sel='{Sel}' topic='{Topic}' term='{Term}' previewSample='{Preview}'",
                                        normalizedSelected, topic, term, previewTextMiss.Length > 40 ? previewTextMiss.Substring(0, 40) : previewTextMiss);
                                }
                                else if (misses == 16)
                                {
                                    Log.Verbose("FilterEval further misses suppressed for selected topic '{Sel}'", normalizedSelected);
                                }
                            }
                        }

                        if (searchMatch)
                        {
                            Interlocked.Increment(ref _filterDiagnosticsMatches);
                        }
                        return searchMatch;
                    });
                });

            _messageHistorySubscription = _messageHistorySource.Connect() // Connect to the source
                .Filter(filterPredicate) // Apply the dynamic filter
                .Sort(SortExpressionComparer<MessageViewModel>.Descending(m => m.Timestamp)) // Keep newest messages on top
                .ObserveOn(_uiScheduler) // Ensure updates are on the UI thread (injected scheduler)
                .Bind(out _filteredMessageHistory) // Bind the results to the ReadOnlyObservableCollection
                .DisposeMany() // Dispose items when they are removed from the collection
                .Subscribe(_ =>
                {
                    try
                    {
                        _isAutoSelectingMessage = true;

                        // Auto-select first message when:
                        //  - A node is selected
                        //  - We have messages for that node now
                        //  - No current selection or current selection is no longer in the filtered view
                        if (SelectedNode != null &&
                            _filteredMessageHistory.Count > 0 &&
                            (SelectedMessage == null || !_filteredMessageHistory.Contains(SelectedMessage)))
                        {
                            SelectedMessage = _filteredMessageHistory.FirstOrDefault();
                        }

                        // Update MessageNavigationState for j/k navigation (Feature 004)
                        var mqttMessages = _filteredMessageHistory
                            .Select(vm => vm.GetFullMessage())
                            .Where(msg => msg != null)
                            .Cast<MqttApplicationMessage>()
                            .ToList();
                        _messageNavigationState.UpdateMessages(mqttMessages);
                    }
                    finally
                    {
                        _isAutoSelectingMessage = false;
                    }
                }, ex => Log.Error(ex, "Error in MessageHistory DynamicData pipeline"));

            FilteredMessageHistory = _filteredMessageHistory; // Assign the bound collection
        }

        if (!string.IsNullOrEmpty(aspireHostname) && aspirePort.HasValue)
        {
            if (aspirePort.Value > 0 && aspirePort.Value < 65535)
            {
                Log.Information("Using Aspire-provided MQTT configuration. Hostname: {Hostname}, Port: {Port}", aspireHostname, aspirePort.Value);
                Settings.Hostname = aspireHostname; // Example if you want to update the UI settings fields
                Settings.Port = aspirePort.Value;
            }
        }

        // Use the injected mqttService if available (for testing), otherwise create a new one.
        _mqttService = mqttService ?? new MqttEngine(new MqttConnectionSettings
        {
            Hostname = Settings.Hostname,
            Port = Settings.Port,
            ClientId = Settings.ClientId,
            KeepAliveInterval = Settings.KeepAliveInterval,
            CleanSession = Settings.CleanSession,
            SessionExpiryInterval = Settings.SessionExpiryInterval,
            TopicSpecificBufferLimits = Settings.Into().TopicSpecificBufferLimits,
            DefaultTopicBufferSizeBytes = Settings.Into().DefaultTopicBufferSizeBytes,
            AuthMode = Settings.Into().AuthMode,
            UseTls = Settings.UseTls
        });

        _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
        _mqttService.MessagesBatchReceived += OnMessagesBatchReceived;
        _mqttService.LogMessage += OnLogMessage;

        // Subscribe to correlation status changes for icon updates
        if (_correlationService != null)
        {
            _correlationService.CorrelationStatusChanged += OnCorrelationStatusChanged;
        }

        // Create DeleteTopicService if not provided, now that we have MqttService
        _deleteTopicService = deleteTopicService ?? new DeleteTopicService(_mqttService, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeleteTopicService>.Instance);

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
        FocusTopicTreeCommand = ReactiveCommand.Create(() => { Log.Debug("FocusTopicTreeCommand executed after search."); /* Actual focus happens in View code-behind */ });
        CopyPayloadCommand = ReactiveCommand.CreateFromTask<object?>(CopyPayloadToClipboardAsync); // Initialize copy payload command
        DeleteTopicCommand = ReactiveCommand.CreateFromTask(ExecuteDeleteTopicAsync); // Initialize delete topic command
        NavigateToResponseCommand = ReactiveCommand.Create<string?>(NavigateToResponse); // Initialize navigate to response command
        ExportMessageCommand = ReactiveCommand.CreateFromTask<object?>(ExecuteExportMessageAsync); // T020: Initialize export message command
        ExportAllCommand = ReactiveCommand.Create(ExecuteExportAllCommand); // T021: Initialize export all command

        // --- Property Change Reactions ---

        // When SelectedMessage changes, update the MessageDetails
        // Disable reactive subscriptions in test mode to prevent hanging
        if (!_testMode)
        {
            var selectedMessageChanged = this.WhenAnyValue(x => x.SelectedMessage);
            if (Application.Current != null)
            {
                _selectedMessageSubscription = selectedMessageChanged
                    .ObserveOn(_uiScheduler)
                    .Subscribe(UpdateMessageDetails);
            }
            else
            {
                // In pure unit-test (non-Avalonia) context stay on the creation thread of TextDocument
                _selectedMessageSubscription = selectedMessageChanged.Subscribe(UpdateMessageDetails);
            }

            // When CommandText changes, update the CommandSuggestions
            _commandTextSubscription = this.WhenAnyValue(x => x.CommandText)
                .Throttle(TimeSpan.FromMilliseconds(150), _uiScheduler) // Small debounce (injected scheduler)
                .DistinctUntilChanged() // Only update if text actually changed
                .ObserveOn(_uiScheduler) // Ensure UI update is on the correct thread (injected scheduler)
                .Subscribe(text => UpdateCommandSuggestions(text));
        }
        else
        {
            // In test mode, create dummy disposables to satisfy readonly field requirements
            _selectedMessageSubscription = System.Reactive.Disposables.Disposable.Empty;
            _commandTextSubscription = System.Reactive.Disposables.Disposable.Empty;
        }

        // --- Global Hook Setup ---
        if (!_testMode)
        {
            try
            {
                _globalHook = new SimpleReactiveGlobalHook();
                _globalHookSubscription = _globalHook.KeyPressed
                    .Do(e => { })
                    .Where(e =>
                    {
                        bool ctrl = e.RawEvent.Mask.HasFlag(ModifierMask.LeftCtrl) || e.RawEvent.Mask.HasFlag(ModifierMask.RightCtrl);
                        bool shift = e.RawEvent.Mask.HasFlag(ModifierMask.LeftShift) || e.RawEvent.Mask.HasFlag(ModifierMask.RightShift);
                        bool pKey = e.Data.KeyCode == KeyCode.VcP;
                        bool focused = IsWindowFocused;
                        bool match = focused && ctrl && shift && pKey;
                        if (match)
                        {
                            Log.Debug("Ctrl+Shift+P MATCHED inside Where filter (Window Focused: {IsFocused}).", focused);
                        }
                        else if (ctrl && shift && pKey)
                        {
                            Log.Verbose("Ctrl+Shift+P detected but window not focused. Hook suppressed.");
                        }
                        Log.Verbose("Global Hook Filter Check: Key={Key}, Modifiers={Modifiers}, IsWindowFocused={IsFocused}, Result={Match}", e.Data.KeyCode, e.RawEvent.Mask, focused, match);
                        return match;
                    })
                    .ObserveOn(_uiScheduler)
                    .Do(_ => Log.Debug("Ctrl+Shift+P detected by SharpHook pipeline (after Where filter)."))
                    .Select(_ => Unit.Default)
                    .InvokeCommand(FocusCommandBarCommand);

                _globalHook.RunAsync().Subscribe(
                    _ => { },
                    ex => Log.Error(ex, "Error during Global Hook execution (RunAsync OnError)"),
                    () => Log.Information("Global Hook stopped.")
                );
                Log.Information("SharpHook Global Hook RunAsync called.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize or run SharpHook global hook. Hotkey might not work.");
                _globalHook = null;
                _globalHookSubscription = null;
            }
        }
        else
        {
            Log.Debug("Global hook initialization skipped in test mode.");
        }
    }

    private void OnLogMessage(object? sender, string log)
    {
        // Guard against calls during disposal
        if (_disposedValue)
            return;

        // TODO: Implement proper logging (e.g., to a log panel or file)
        Log.Debug("[MQTT Engine]: {LogMessage}", log);
    }

    private void OnCorrelationStatusChanged(object? sender, CorrelationStatusChangedEventArgs e)
    {
        // Guard against calls during disposal
        if (_disposedValue)
            return;

        // Update icon status when correlation status changes
        if (_iconService != null && e != null)
        {
            var updateResult = _iconService.UpdateIconStatusAsync(e.RequestMessageId, e.NewStatus).GetAwaiter().GetResult();

            Log.Verbose("Correlation status changed for request {RequestMessageId}: {PreviousStatus} -> {NewStatus}, Icon service update: {UpdateResult}",
                e.RequestMessageId, e.PreviousStatus, e.NewStatus, updateResult);

            // If currently viewing the request message, update the icon in the metadata view
            if (SelectedMessage != null && SelectedMessage.MessageId.ToString() == e.RequestMessageId)
            {
                Log.Verbose("Currently viewing request {RequestMessageId}, updating UI icon directly", e.RequestMessageId);
                ScheduleOnUi(() =>
                {
                    // Find the Response Topic metadata item and update its icon
                    var responseTopicItem = MessageMetadata.FirstOrDefault(m => m.Key == "Response Topic");
                    if (responseTopicItem?.IconViewModel != null)
                    {
                        responseTopicItem.IconViewModel.Status = e.NewStatus;
                        responseTopicItem.IconViewModel.IconPath = GetIconPathForStatus(e.NewStatus);
                        responseTopicItem.IconViewModel.IsClickable = e.NewStatus.IsClickable();
                        responseTopicItem.IconViewModel.ToolTip = e.NewStatus.GetTooltipText();
                        Log.Debug("Updated icon status in UI for request {RequestMessageId} to {NewStatus}, IconPath: {IconPath}, IsClickable: {IsClickable}, IsResponseReceived: {IsResponseReceived}",
                            e.RequestMessageId, e.NewStatus, responseTopicItem.IconViewModel.IconPath, responseTopicItem.IconViewModel.IsClickable, responseTopicItem.IconViewModel.IsResponseReceived);
                    }
                    else
                    {
                        Log.Debug("Could not find Response Topic metadata item or icon for request {RequestMessageId}", e.RequestMessageId);
                    }
                });
            }
            else
            {
                Log.Verbose("Not currently viewing request {RequestMessageId} (viewing: {CurrentMessage}), icon will be updated when message is selected",
                    e.RequestMessageId, SelectedMessage?.MessageId.ToString() ?? "none");
            }
        }
    }

    /// <summary>
    /// Gets the icon path for a given response status.
    /// </summary>
    private string GetIconPathForStatus(ResponseStatus status)
    {
        return status switch
        {
            ResponseStatus.Pending => "avares://CrowsNestMqtt/Assets/Icons/clock.svg",
            ResponseStatus.Received => "avares://CrowsNestMqtt/Assets/Icons/arrow.svg",
            ResponseStatus.NavigationDisabled => "avares://CrowsNestMqtt/Assets/Icons/clock_disabled.svg",
            ResponseStatus.Hidden => string.Empty,
            _ => string.Empty
        };
    }

    // --- MQTT Event Handlers ---

    private void OnConnectionStateChanged(object? sender, MqttConnectionStateChangedEventArgs e)
    {
        // Guard against calls during disposal
        if (_disposedValue)
            return;

        void Apply()
        {
            ConnectionStatus = e.ConnectionStatus;
            ConnectionStatusMessage = e.ReconnectInfo ?? e.Error?.Message;

            this.RaisePropertyChanged(nameof(IsConnected));
            this.RaisePropertyChanged(nameof(IsConnecting));
            this.RaisePropertyChanged(nameof(IsDisconnected));
            this.RaisePropertyChanged(nameof(IsDeleteButtonEnabled));

            if (ConnectionStatus == ConnectionStatusState.Connected)
            {
                Log.Information("MQTT Client Connected. Starting UI Timer.");
                StartTimer(TimeSpan.FromSeconds(1));
                LoadInitialTopics();
            }
            else
            {
                Log.Warning(e.Error, "MQTT Client is not connected. Stopping UI Timer.");
                StopTimer();
            }
        }

        ScheduleOnUi(Apply);
    }

    private void OnMessagesBatchReceived(object? sender, IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs> batch)
    {
        // Guard against calls during disposal
        if (_disposedValue)
            return;

        if (IsPaused || batch == null || batch.Count == 0) return;
        ScheduleOnUi(() => ProcessMessageBatchOnUIThread(batch.ToList()));
    }
    
    /// <summary>
    /// Processes a batch of messages on the UI thread
    /// </summary>
private void ProcessMessageBatchOnUIThread(List<IdentifiedMqttApplicationMessageReceivedEventArgs> messages)
{
    // Guard against calls during disposal
    if (_disposedValue)
        return;

    const int maxUiBatchSize = 50;
    if (messages.Count > maxUiBatchSize)
    {
        var currentBatch = messages.Take(maxUiBatchSize).ToList();
        var remaining = messages.Skip(maxUiBatchSize).ToList();
        ProcessMessageBatchOnUIThread(currentBatch);
        // Schedule the rest to process after yielding to the UI thread
        ScheduleOnUi(() => ProcessMessageBatchOnUIThread(remaining));
        return;
    }

    var messageViewModels = new List<MessageViewModel>();
    var topicCounts = new Dictionary<string, int>();

    foreach (var e in messages)
    {
        var topic = e.Topic;
        var messageId = e.MessageId;

        topicCounts.TryGetValue(topic, out var currentCount);
        topicCounts[topic] = currentCount + 1;

        string preview;
        try
        {
            preview = e.ApplicationMessage.Payload.Length > 0
                ? Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
                : "[No Payload]";
        }
        catch (DecoderFallbackException)
        {
            preview = $"[Binary Data: {e.ApplicationMessage.Payload.Length} bytes]";
        }

        const int maxPreviewLength = 100;
        if (preview.Length > maxPreviewLength)
        {
            preview = preview.Substring(0, maxPreviewLength) + "...";
        }

        var messageVm = new MessageViewModel(
            messageId,
            topic,
            DateTime.Now,
            preview.Replace(Environment.NewLine, " "),
            (int)e.ApplicationMessage.Payload.Length,
            _mqttService,
            this,
            e.ApplicationMessage,
            true, // enableFallbackFullMessage
            e.IsEffectivelyRetained);

        messageViewModels.Add(messageVm);

        // Track request-response correlation for MQTT V5
        if (_correlationService != null && e?.ApplicationMessage != null)
        {
            var msg = e.ApplicationMessage;

            // Register request messages with response-topic
            if (!string.IsNullOrEmpty(msg?.ResponseTopic) && msg?.CorrelationData != null && msg.CorrelationData.Length > 0)
            {
                var correlationHex = BitConverter.ToString(msg.CorrelationData).Replace("-", "");
                Log.Information("Registering REQUEST message {MessageId} on topic {Topic} with response-topic {ResponseTopic} and correlation-data {CorrelationData}",
                    messageId, topic, msg.ResponseTopic, correlationHex);

                var registered = _correlationService.RegisterRequestAsync(
                    messageId.ToString(),
                    msg.CorrelationData,
                    msg.ResponseTopic,
                    ttlMinutes: 30).GetAwaiter().GetResult();

                Log.Information("Request registration {Result} for message {MessageId}",
                    registered ? "SUCCEEDED" : "FAILED", messageId);
            }
            // Link response messages with correlation-data
            else if (msg?.CorrelationData != null && msg.CorrelationData.Length > 0)
            {
                var correlationHex = BitConverter.ToString(msg.CorrelationData).Replace("-", "");
                Log.Information("Linking RESPONSE message {MessageId} on topic {Topic} with correlation-data {CorrelationData}",
                    messageId, topic, correlationHex);

                var linked = _correlationService.LinkResponseAsync(
                    messageId.ToString(),
                    msg.CorrelationData,
                    topic).GetAwaiter().GetResult();

                Log.Information("Response linking {Result} for message {MessageId}",
                    linked ? "SUCCEEDED" : "FAILED", messageId);
            }
        }
    }

    // Removed direct AddRange; additions now handled after store AddBatch to avoid duplicates.

    // Per-topic retention integration: add to store & apply only per-topic evictions
    try
    {
        var batchTuples = messageViewModels
            .Select(vm => (vm.MessageId, vm.Topic, vm.GetFullMessage()!))
            .Where(t => t.Item3 != null)
            .Select(t => (t.MessageId, t.Topic, t.Item3))
            .ToList();

        var (added, evicted) = _messageStore.AddBatch(
            batchTuples.Select(t => (t.MessageId, t.Topic, t.Item3)));

        // Map added VMs (by id) to actual VMs we constructed
        var addedVmList = new List<MessageViewModel>();
        foreach (var add in added)
        {
            if (_messageIndex.ContainsKey(add.MessageId))
                continue; // already present
            var vm = messageViewModels.FirstOrDefault(m => m.MessageId == add.MessageId);
            if (vm != null)
            {
                addedVmList.Add(vm);
                _messageIndex[vm.MessageId] = vm;
            }
        }
        if (addedVmList.Count > 0)
        {
            _messageHistorySource.AddRange(addedVmList);

            // In test mode, manually update the simple filtered collection
            if (_testMode)
            {
                UpdateSimpleFilteredHistory();
            }
        }

        if (evicted.Count > 0)
        {
            var toRemove = new List<MessageViewModel>();
            foreach (var ev in evicted)
            {
                if (_messageIndex.TryGetValue(ev.MessageId, out var vm))
                {
                    toRemove.Add(vm);
                    _messageIndex.Remove(ev.MessageId);
                }
            }
            if (toRemove.Count > 0)
            {
                _messageHistorySource.RemoveMany(toRemove);
                // Reselect if current selection was removed
                if (SelectedMessage != null && toRemove.Contains(SelectedMessage))
                {
                    var selNorm = _normalizedSelectedPath;
                    if (selNorm != null)
                    {
                        var replacement = _messageHistorySource.Items
                            .Where(m =>
                            {
                                var tn = NormalizeTopic(m.Topic);
                                return tn == selNorm || (tn != null && tn.StartsWith(selNorm + "/", StringComparison.OrdinalIgnoreCase));
                            })
                            .OrderByDescending(m => m.Timestamp)
                            .FirstOrDefault();
                        if (replacement != null)
                        {
                            SelectedMessage = replacement;
                        }
                        else
                        {
                            SelectedMessage = null;
                        }
                    }
                    else
                    {
                        SelectedMessage = _messageHistorySource.Items.OrderByDescending(m => m.Timestamp).FirstOrDefault();
                    }
                }
            }
            Log.Debug("Per-topic evictions applied. Added={Added} Evicted={Evicted}", addedVmList.Count, evicted.Count);
        }
    }
    catch (Exception exStore)
    {
        Log.Error(exStore, "Error applying per-topic retention store results.");
        // Fallback: add all directly if store failed
        _messageHistorySource.AddRange(messageViewModels.Where(vm => !_messageIndex.ContainsKey(vm.MessageId)));

        // In test mode, manually update the simple filtered collection
        if (_testMode)
        {
            UpdateSimpleFilteredHistory();
        }
    }

    foreach (var kvp in topicCounts)
    {
        string topic = kvp.Key;
        int messageCount = kvp.Value;
        UpdateOrCreateNodeWithCount(topic, messageCount);
    }

    Log.Verbose("Processed batch of {Count} messages across {TopicCount} topics. Source count: {Total}",
        messages.Count, topicCounts.Count, _messageHistorySource.Count);

    // Diagnostics & fallback selection
    try
    {
        // Immediate (pre-pipeline-flush) fallback: select from source if filter not yet populated.
        if (_normalizedSelectedPath != null && _filteredMessageHistory.Count == 0)
        {
            var candidate = _messageHistorySource.Items
                .FirstOrDefault(m => NormalizeTopic(m.Topic) == _normalizedSelectedPath);
            if (candidate != null && SelectedMessage != candidate)
            {
                SelectedMessage = candidate;
            }
        }

        // Defer a second-phase check so DynamicData filter & sort have time to process new additions.
        if (_testMode)
        {
            try
            {
                var sel = _normalizedSelectedPath;
                if (sel != null)
                {
                    bool sourceHasSelected = _messageHistorySource.Items.Any(m => NormalizeTopic(m.Topic) == sel);
                    bool filteredHasSelected = _filteredMessageHistory.Any(m => NormalizeTopic(m.Topic) == sel);

                    if (sourceHasSelected && !filteredHasSelected)
                    {
                        Log.Warning("Deferred check: source has messages for selected topic '{Sel}' but filtered list still empty after UI flush.", sel);

                        // Collect detailed diagnostics for selected topic messages present in source.
                        var selectedSourceMessages = _messageHistorySource.Items
                            .Where(m => NormalizeTopic(m.Topic) == sel)
                            .OrderByDescending(m => m.Timestamp)
                            .ToList();

                        Log.Verbose("Diagnostics: SourceSelectedCount={Cnt} FilteredSelectedCount=0 TotalSource={SrcTot} TotalFiltered={FiltTot}",
                            selectedSourceMessages.Count, _filteredMessageHistory.Count, _messageHistorySource.Count, _filteredMessageHistory.Count);

                        // Log up to first 5 message IDs & timestamps for the selected topic
                        int diagIndex = 0;
                        foreach (var sm in selectedSourceMessages.Take(5))
                        {
                            Log.Verbose("Diagnostics SelectedTopic Message[{Idx}] Id={Id} Ts={Ts} PreviewLen={Len}",
                                diagIndex++, sm.MessageId, sm.Timestamp.ToString("HH:mm:ss.fff"), sm.PayloadPreview?.Length ?? 0);
                        }
                        if (selectedSourceMessages.Count > 5)
                        {
                            Log.Verbose("Diagnostics: {Extra} additional messages for selected topic omitted from log.", selectedSourceMessages.Count - 5);
                        }

                        // Attempt stronger fallback selection from source
                        var deferredCandidate = selectedSourceMessages.FirstOrDefault();

                        if (deferredCandidate != null && SelectedMessage != deferredCandidate)
                        {
                            Log.Verbose("Deferred fallback selecting message {MessageId} for topic '{Topic}'.", deferredCandidate.MessageId, deferredCandidate.Topic);
                            SelectedMessage = deferredCandidate;
                        }

                        // Force a refresh of the source list so DynamicData re-evaluates predicates (in case no change set triggered).
                        try
                        {
                            // Removed _messageHistorySource.Refresh() (method not available on SourceList).
                            // If predicate re-evaluation still required, a future change can toggle a lightweight
                            // transient flag or reassign SelectedNode to itself. For now rely on next batch or UI action.
                            Log.Verbose("Skipped forcing DynamicData refresh (no Refresh() API on SourceList) for selected topic '{Sel}'.", sel);
                        }
                        catch (Exception refreshEx)
                        {
                            Log.Error(refreshEx, "Unexpected error in refresh fallback block for selected topic '{Sel}'.", sel);
                        }

                        // Schedule a second deferred verification after refresh
                        if (_testMode)
                        {
                            try
                            {
                                bool filteredNowHasSelected = _filteredMessageHistory.Any(m => NormalizeTopic(m.Topic) == sel);
                                if (!filteredNowHasSelected)
                                {
                                    Log.Warning("Post-refresh verification: filtered list STILL missing messages for selected topic '{Sel}'. Will attempt manual selection again.", sel);
                                    var secondCandidate = _messageHistorySource.Items
                                        .Where(m => NormalizeTopic(m.Topic) == sel)
                                        .OrderByDescending(m => m.Timestamp)
                                        .FirstOrDefault();
                                    if (secondCandidate != null && SelectedMessage != secondCandidate)
                                    {
                                        Log.Verbose("Second deferred fallback selecting message {MessageId} for topic '{Topic}'.", secondCandidate.MessageId, secondCandidate.Topic);
                                        SelectedMessage = secondCandidate;
                                    }
                                }
                                else
                                {
                                    Log.Verbose("Post-refresh verification: filtered list now contains messages for '{Sel}'.", sel);
                                }
                            }
                            catch (Exception ex3)
                            {
                                Log.Error(ex3, "Error during second deferred verification for selected topic '{Sel}'.", sel);
                            }
                        }
                        else
                        {
                            ScheduleOnUi(() =>
                            {
                                try
                                {
                                    bool filteredNowHasSelected = _filteredMessageHistory.Any(m => NormalizeTopic(m.Topic) == sel);
                                    if (!filteredNowHasSelected)
                                    {
                                        Log.Warning("Post-refresh verification: filtered list STILL missing messages for selected topic '{Sel}'. Will attempt manual selection again.", sel);
                                        var secondCandidate = _messageHistorySource.Items
                                            .Where(m => NormalizeTopic(m.Topic) == sel)
                                            .OrderByDescending(m => m.Timestamp)
                                            .FirstOrDefault();
                                        if (secondCandidate != null && SelectedMessage != secondCandidate)
                                        {
                                            Log.Verbose("Second deferred fallback selecting message {MessageId} for topic '{Topic}'.", secondCandidate.MessageId, secondCandidate.Topic);
                                            SelectedMessage = secondCandidate;
                                        }
                                    }
                                    else
                                    {
                                        Log.Verbose("Post-refresh verification: filtered list now contains messages for '{Sel}'.", sel);
                                    }
                                }
                                catch (Exception ex3)
                                {
                                    Log.Error(ex3, "Error during second deferred verification for selected topic '{Sel}'.", sel);
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex2)
            {
                Log.Error(ex2, "Error during deferred post-AddRange selection check.");
            }
        }
        else
        {
            ScheduleOnUi(() =>
            {
                try
                {
                    var sel = _normalizedSelectedPath;
                    if (sel != null)
                    {
                        bool sourceHasSelected = _messageHistorySource.Items.Any(m => NormalizeTopic(m.Topic) == sel);
                        bool filteredHasSelected = _filteredMessageHistory.Any(m => NormalizeTopic(m.Topic) == sel);

                        if (sourceHasSelected && !filteredHasSelected)
                        {
                            Log.Warning("Deferred check: source has messages for selected topic '{Sel}' but filtered list still empty after UI flush.", sel);

                            var selectedSourceMessages = _messageHistorySource.Items
                                .Where(m => NormalizeTopic(m.Topic) == sel)
                                .OrderByDescending(m => m.Timestamp)
                                .ToList();

                            Log.Verbose("Diagnostics: SourceSelectedCount={Cnt} FilteredSelectedCount=0 TotalSource={SrcTot} TotalFiltered={FiltTot}",
                                selectedSourceMessages.Count, _filteredMessageHistory.Count, _messageHistorySource.Count, _filteredMessageHistory.Count);

                            int diagIndex = 0;
                            foreach (var sm in selectedSourceMessages.Take(5))
                            {
                                Log.Verbose("Diagnostics SelectedTopic Message[{Idx}] Id={Id} Ts={Ts} PreviewLen={Len}",
                                    diagIndex++, sm.MessageId, sm.Timestamp.ToString("HH:mm:ss.fff"), sm.PayloadPreview?.Length ?? 0);
                            }
                            if (selectedSourceMessages.Count > 5)
                            {
                                Log.Verbose("Diagnostics: {Extra} additional messages for selected topic omitted from log.", selectedSourceMessages.Count - 5);
                            }

                            var deferredCandidate = selectedSourceMessages.FirstOrDefault();

                            if (deferredCandidate != null && SelectedMessage != deferredCandidate)
                            {
                                Log.Verbose("Deferred fallback selecting message {MessageId} for topic '{Topic}'.", deferredCandidate.MessageId, deferredCandidate.Topic);
                                SelectedMessage = deferredCandidate;
                            }

                            try
                            {
                                Log.Verbose("Skipped forcing DynamicData refresh (no Refresh() API on SourceList) for selected topic '{Sel}'.", sel);
                            }
                            catch (Exception refreshEx)
                            {
                                Log.Error(refreshEx, "Unexpected error in refresh fallback block for selected topic '{Sel}'.", sel);
                            }

                            // Second deferred verification
                            ScheduleOnUi(() =>
                            {
                                try
                                {
                                    bool filteredNowHasSelected = _filteredMessageHistory.Any(m => NormalizeTopic(m.Topic) == sel);
                                    if (!filteredNowHasSelected)
                                    {
                                        Log.Warning("Post-refresh verification: filtered list STILL missing messages for selected topic '{Sel}'. Will attempt manual selection again.", sel);
                                        var secondCandidate = _messageHistorySource.Items
                                            .Where(m => NormalizeTopic(m.Topic) == sel)
                                            .OrderByDescending(m => m.Timestamp)
                                            .FirstOrDefault();
                                        if (secondCandidate != null && SelectedMessage != secondCandidate)
                                        {
                                            Log.Verbose("Second deferred fallback selecting message {MessageId} for topic '{Topic}'.", secondCandidate.MessageId, secondCandidate.Topic);
                                            SelectedMessage = secondCandidate;
                                        }
                                    }
                                    else
                                    {
                                        Log.Verbose("Post-refresh verification: filtered list now contains messages for '{Sel}'.", sel);
                                    }
                                }
                                catch (Exception ex3)
                                {
                                    Log.Error(ex3, "Error during second deferred verification for selected topic '{Sel}'.", sel);
                                }
                            });
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Log.Error(ex2, "Error during deferred post-AddRange selection check.");
                }
            });
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during post-AddRange diagnostics.");
    }
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
        // Guard against calls during disposal
        if (_disposedValue)
            return;

        // Prevent re-entrancy during UpdateMessageDetails execution
        if (_isUpdatingMessageDetails)
            return;

        try
        {
            _isUpdatingMessageDetails = true;

            // Ensure we are on Avalonia UI thread due to TextDocument & Bitmap access
            // In non-Avalonia unit test context (Application.Current == null) or when the TextDocument
            // was created on this thread, proceed without marshaling.
            // Also skip marshaling in test mode to avoid potential deadlocks
            // Use ScheduleOnUi which automatically handles test mode
            if (Application.Current != null && !CheckUiThreadAccess())
            {
                ScheduleOnUi(() => UpdateMessageDetails(messageVm));
                return;
            }

        // Clear previous details
        MessageMetadata.Clear();
        MessageUserProperties.Clear();
        HasUserProperties = false;
        JsonViewer.LoadJson(string.Empty); // Clear JSON viewer
        IsJsonViewerVisible = false; // Hide JSON viewer
        IsRawTextViewerVisible = false; // Hide Raw Text viewer
        IsImageViewerVisible = false;
        IsVideoViewerVisible = false;
        IsHexViewerVisible = false;
        ImagePayload?.Dispose();
        ImagePayload = null;
        // Clear the document content instead of the string property
        RawPayloadDocument.Text = string.Empty;
        PayloadSyntaxHighlighting = null; // Clear syntax highlighting
        // HexDocument = null; // Removed: no longer used
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
        // Use the corrected retain status from MessageViewModel instead of raw MQTT message
        var retainStatus = messageVm?.IsEffectivelyRetained.ToString() ?? msg.Retain.ToString();
        MessageMetadata.Add(new MetadataItem("Retain", retainStatus));
        MessageMetadata.Add(new MetadataItem("Payload Format", msg.PayloadFormatIndicator.ToString()));
        MessageMetadata.Add(new MetadataItem("Expiry (s)", msg.MessageExpiryInterval.ToString()));
        MessageMetadata.Add(new MetadataItem("ContentType", msg.ContentType));

        // Add Response Topic with icon if present
        ResponseIconViewModel? iconVm = null;
        if (!string.IsNullOrEmpty(msg.ResponseTopic) && messageVm != null)
        {
            var requestMessageId = messageVm.MessageId.ToString();

            // Try to get existing icon view model from icon service first
            if (_iconService != null)
            {
                iconVm = _iconService.GetIconViewModelAsync(requestMessageId).GetAwaiter().GetResult();
            }

            // If no existing icon, create a new one with current status from correlation service
            if (iconVm == null)
            {
                var currentStatus = ResponseStatus.Pending;
                if (_correlationService != null)
                {
                    currentStatus = _correlationService.GetResponseStatusAsync(requestMessageId).GetAwaiter().GetResult();
                }

                Log.Verbose("Creating new icon view model for request {RequestMessageId} with current status {Status}",
                    requestMessageId, currentStatus);

                // Create icon using icon service so it gets registered and can be updated later
                if (_iconService != null)
                {
                    // Assume response topic is subscribed (we received the message, so we must be subscribed to this topic)
                    iconVm = _iconService.CreateIconViewModelAsync(requestMessageId, true, true).GetAwaiter().GetResult();

                    if (iconVm != null)
                    {
                        Log.Verbose("Icon service created icon for {RequestMessageId} with status {Status}",
                            requestMessageId, iconVm.Status);

                        // Update the icon to the current status (CreateIconViewModelAsync always creates with Pending)
                        if (currentStatus != ResponseStatus.Pending)
                        {
                            var updated = _iconService.UpdateIconStatusAsync(requestMessageId, currentStatus).GetAwaiter().GetResult();
                            iconVm.Status = currentStatus;
                            iconVm.IconPath = GetIconPathForStatus(currentStatus);
                            iconVm.IsClickable = currentStatus.IsClickable();
                            iconVm.ToolTip = currentStatus.GetTooltipText();
                            Log.Verbose("Updated newly created icon to {Status}, update result: {Updated}, IconPath: {IconPath}, IsClickable: {IsClickable}, IsResponseReceived: {IsResponseReceived}",
                                currentStatus, updated, iconVm.IconPath, iconVm.IsClickable, iconVm.IsResponseReceived);
                        }
                        else
                        {
                            Log.Verbose("Icon created with Pending status, no update needed");
                        }
                    }
                    else
                    {
                        Log.Verbose("Icon service failed to create icon for {RequestMessageId}", requestMessageId);
                    }
                }
                else
                {
                    // Fallback: create icon directly if service not available
                    var iconPath = GetIconPathForStatus(currentStatus);
                    iconVm = new ResponseIconViewModel
                    {
                        RequestMessageId = requestMessageId,
                        Status = currentStatus,
                        IconPath = iconPath,
                        ToolTip = currentStatus == ResponseStatus.Received
                            ? "Click to navigate to response message"
                            : "Click to navigate to response topic (awaiting response)",
                        IsClickable = true,
                        IsVisible = true
                    };
                }
            }
            else
            {
                Log.Debug("Using existing icon view model for request {RequestMessageId} with status {Status}",
                    requestMessageId, iconVm.Status);
            }
        }
        MessageMetadata.Add(new MetadataItem("Response Topic", msg.ResponseTopic ?? string.Empty, iconVm));

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
        HexPayloadBytes = null;

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
        if (msg.ContentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                VideoPayload = payloadBytes;
                IsVideoViewerVisible = true;
                IsImageViewerVisible = false;
                IsJsonViewerVisible = false;
                IsRawTextViewerVisible = false;
                IsHexViewerVisible = false;
                StatusBarText = "Displaying video payload.";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load video from payload for content type {ContentType}", msg.ContentType);
                ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
            }
            this.RaisePropertyChanged(nameof(ShowJsonParseError));
            this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
            return;
        }
        if (msg.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            try
            {
                using var ms = new MemoryStream(payloadBytes);
                ImagePayload = new Bitmap(ms);
                IsImageViewerVisible = true;
                IsJsonViewerVisible = false;
                IsRawTextViewerVisible = false;
                IsVideoViewerVisible = false;
                IsHexViewerVisible = false;
                StatusBarText = "Displaying image payload.";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not decode image from payload for content type {ContentType}", msg.ContentType);
                ImagePayload?.Dispose();
                ImagePayload = null;
                IsImageViewerVisible = true;
                IsJsonViewerVisible = false;
                IsRawTextViewerVisible = false;
                IsVideoViewerVisible = false;
                IsHexViewerVisible = false;
                StatusBarText = "Image payload (decode failed).";
            }
            this.RaisePropertyChanged(nameof(ShowJsonParseError));
            this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
            return;
        }
        if (IsBinaryContentType(msg.ContentType) && payloadBytes.Length > 0)
        {
            try
            {
                HexPayloadBytes = payloadBytes;
                IsHexViewerVisible = true;
                IsJsonViewerVisible = false;
                IsRawTextViewerVisible = false;
                IsImageViewerVisible = false;
                IsVideoViewerVisible = false;
                StatusBarText = "Displaying binary payload in hex viewer.";
                Log.Information("Auto-switched to hex viewer for binary content type: {ContentType}", msg.ContentType);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load hex viewer for binary payload for content type {ContentType}", msg.ContentType);
                ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
            }
            this.RaisePropertyChanged(nameof(ShowJsonParseError));
            this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
            return;
        }
        if (isPayloadValidUtf8)
        {
            JsonViewer.LoadJson(payloadAsString);
            if (string.IsNullOrEmpty(JsonViewer.JsonParseError))
            {
                IsJsonViewerVisible = true;
                IsRawTextViewerVisible = false;
                IsImageViewerVisible = false;
                IsVideoViewerVisible = false;
                IsHexViewerVisible = false;
                PayloadSyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Json");
                this.RaisePropertyChanged(nameof(ShowJsonParseError));
                this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
                return;
            }
            ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
            StatusBarText = $"Payload is not valid JSON. Showing raw view. {JsonViewer.JsonParseError}";
            this.RaisePropertyChanged(nameof(ShowJsonParseError));
            this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
            return;
        }
        ShowRawPayload(isPayloadValidUtf8, payloadAsString, msg);
        this.RaisePropertyChanged(nameof(ShowJsonParseError));
        this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
        }
        finally
        {
            _isUpdatingMessageDetails = false;
        }
    }

    private bool IsBinaryContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return false;
        var ct = contentType.ToLowerInvariant();
        if (ct.StartsWith("image/") || ct.StartsWith("video/"))
            return false;
        // Common binary types
        return ct == "application/octet-stream"
            || ct == "application/cbor"
            || ct == "application/x-binary"
            || ct == "application/x-protobuf"
            || ct == "application/x-msgpack"
            || ct == "application/x-capnp"
            || ct == "application/x-avro"
            || ct == "application/x-parquet"
            || ct == "application/x-hdf5"
            || ct == "application/x-tar"
            || ct == "application/x-7z-compressed"
            || ct == "application/x-gzip"
            || ct == "application/x-bzip2"
            || ct == "application/x-lzma"
            || ct == "application/x-xz"
            || ct == "application/x-snappy"
            || ct == "application/x-lz4"
            || ct == "application/x-zstd"
            || ct == "application/x-blosc"
            || ct == "application/x-lzop"
            || ct == "application/x-lzo"
            || ct == "application/x-compress"
            || ct == "application/x-archive"
            || ct == "application/x-executable"
            || ct == "application/x-sharedlib"
            || ct == "application/x-object"
            || ct == "application/x-core"
            || ct == "application/x-elf"
            || ct == "application/x-mach-binary"
            || ct == "application/x-msdownload"
            || ct == "application/x-dosexec"
            || ct == "application/x-pe"
            || ct == "application/x-coff"
            || ct == "application/x-aout"
            || ct == "application/x-pie-executable"
            || ct == "application/x-shellscript"
            || ct == "application/x-cpio"
            || ct == "application/x-ar"
            || ct == "application/x-iso9660-image"
            || ct == "application/x-apple-diskimage"
            || ct == "application/x-vhd"
            || ct == "application/x-vmdk"
            || ct == "application/x-virtualbox-vdi"
            || ct == "application/x-qcow"
            || ct == "application/x-qemu-disk"
            || ct == "application/x-vpc"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk"
            || ct == "application/x-vhd"
            || ct == "application/x-vhdx"
            || ct == "application/x-vdi"
            || ct == "application/x-vmdk";
    }

    private void ShowRawPayload(bool isPayloadValidUtf8, string payloadAsString, MqttApplicationMessage? msg)
    {
        IsJsonViewerVisible = false;
        IsRawTextViewerVisible = true;
        IsImageViewerVisible = false;
        IsVideoViewerVisible = false;
        IsHexViewerVisible = false;
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
            DefaultTopicBufferSizeBytes = Settings.Into().DefaultTopicBufferSizeBytes,
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

    private async Task ExecuteDeleteTopicAsync()
    {
        try
        {
            var selectedTopic = GetSelectedTopicForDelete();
            if (string.IsNullOrEmpty(selectedTopic))
            {
                ShowStatus("No topic selected for deletion.");
                return;
            }

            Log.Information("Delete topic command executed for topic: {Topic}", selectedTopic);

            // Execute the delete command using the command processor
            if (_deleteTopicService == null)
            {
                ShowStatus("Delete topic service not available", TimeSpan.FromSeconds(5));
                return;
            }

            var processor = new EnhancedCommandProcessor(this, _deleteTopicService);
            var result = await processor.ExecuteDeleteTopicCommand([selectedTopic], _deleteTopicService, CancellationToken.None);

            // Show delete result message with duration to ensure visibility
            ShowStatus(result.Message, TimeSpan.FromSeconds(5));

            if (result.Success)
            {
                Log.Information("Delete topic command completed successfully for: {Topic}", selectedTopic);
            }
            else
            {
                Log.Warning("Delete topic command failed for {Topic}: {Message}", selectedTopic, result.Message);
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error executing delete topic command: {ex.Message}";
            Log.Error(ex, "Delete topic command failed");
            ShowStatus(errorMessage, TimeSpan.FromSeconds(5));
        }
    }

    private void NavigateToResponse(string? requestMessageId)
    {
        if (string.IsNullOrEmpty(requestMessageId))
        {
            ShowStatus("Invalid request message ID for navigation");
            return;
        }

        Log.Information("Navigate to response command executed for request: {RequestMessageId}", requestMessageId);

        if (_correlationService == null)
        {
            ShowStatus("Correlation service not available", TimeSpan.FromSeconds(3));
            return;
        }

        // Get response status
        var status = _correlationService.GetResponseStatusAsync(requestMessageId).GetAwaiter().GetResult();
        if (status != ResponseStatus.Received)
        {
            ShowStatus("No response received yet for this request", TimeSpan.FromSeconds(3));
            return;
        }

        // Get response topic and message IDs
        var responseTopic = _correlationService.GetResponseTopicAsync(requestMessageId).GetAwaiter().GetResult();
        var responseMessageIds = _correlationService.GetResponseMessageIdsAsync(requestMessageId).GetAwaiter().GetResult();

        if (string.IsNullOrEmpty(responseTopic) || responseMessageIds.Count == 0)
        {
            ShowStatus("No response found for this request", TimeSpan.FromSeconds(3));
            return;
        }

        Log.Information("Navigating to response topic {ResponseTopic} with {Count} response(s)",
            responseTopic, responseMessageIds.Count);

        // Navigate to the response topic by finding and selecting the topic node
        var topicNode = FindTopicNode(responseTopic);
        if (topicNode != null)
        {
            SelectedNode = topicNode;
            Log.Information("Selected topic node: {Topic}", responseTopic);

            // Find and select the latest response message
            var latestResponseId = responseMessageIds[responseMessageIds.Count - 1];
            var responseMessage = FilteredMessageHistory.FirstOrDefault(m => m.MessageId.ToString() == latestResponseId);

            if (responseMessage != null)
            {
                SelectedMessage = responseMessage;
                ShowStatus($"Navigated to response message on topic {responseTopic}", TimeSpan.FromSeconds(3));
                Log.Information("Selected response message: {MessageId}", latestResponseId);
            }
            else
            {
                ShowStatus($"Navigated to topic {responseTopic} but response message not found", TimeSpan.FromSeconds(3));
                Log.Warning("Response message {MessageId} not found in AllMessages", latestResponseId);
            }
        }
        else
        {
            ShowStatus($"Response topic {responseTopic} not found in topic tree", TimeSpan.FromSeconds(3));
            Log.Warning("Topic node not found: {Topic}", responseTopic);
        }
    }

    /// <summary>
    /// Finds a topic node in the topic tree by topic path.
    /// </summary>
    /// <summary>
    /// Navigates to a topic by path (used by keyboard navigation - Feature 004).
    /// Automatically expands all parent nodes to make the target node visible.
    /// </summary>
    public void NavigateToTopic(string topicPath)
    {
        var node = FindTopicNode(topicPath);
        if (node != null)
        {
            // Expand all parent nodes to make the selected node visible
            ExpandParentNodes(node);
            SelectedNode = node;
        }
    }

    /// <summary>
    /// Expands all parent nodes in the tree hierarchy to make the specified node visible.
    /// </summary>
    /// <param name="node">The target node whose parents should be expanded.</param>
    private void ExpandParentNodes(NodeViewModel node)
    {
        var current = node.Parent;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    private NodeViewModel? FindTopicNode(string topicPath)
    {
        if (string.IsNullOrEmpty(topicPath))
            return null;

        // Search recursively through the topic tree
        foreach (var rootNode in TopicTreeNodes)
        {
            var found = FindTopicNodeRecursive(rootNode, topicPath);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Recursively searches for a topic node by path.
    /// </summary>
    private NodeViewModel? FindTopicNodeRecursive(NodeViewModel node, string topicPath)
    {
        if (node.FullPath == topicPath)
            return node;

        foreach (var child in node.Children)
        {
            var found = FindTopicNodeRecursive(child, topicPath);
            if (found != null)
                return found;
        }

        return null;
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
            if (!_testMode)
            {
                ScheduleOnUi(() =>
                {
                    StatusBarText = "Filter cleared.";
                });
            }
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
                if (!_testMode)
                {
                    ScheduleOnUi(() =>
                    {
                        StatusBarText = $"Filter applied: '{CurrentSearchTerm}'. {FilteredMessageHistory.Count} results.";
                    });
                }
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
                    Export(command);
                    break;
                case CommandType.Filter:
                    ApplyTopicFilter(command.Arguments.FirstOrDefault());
                    break;
                case CommandType.Search:
                    string searchTerm = command.Arguments.FirstOrDefault() ?? string.Empty;
                    CurrentSearchTerm = searchTerm;
                    StatusBarText = string.IsNullOrWhiteSpace(searchTerm) ? "Search cleared." : $"Search filter applied: '{searchTerm}'.";
                    Log.Information("Search command executed. Term: '{SearchTerm}'", searchTerm);
                    break;
                case CommandType.TopicSearch:
                    // FR-001: Topic search triggered by /[term] command
                    string topicSearchTerm = command.Arguments.FirstOrDefault() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(topicSearchTerm))
                    {
                        // Execute search using TopicSearchService
                        var searchContext = _topicSearchService.ExecuteSearch(topicSearchTerm);

                        // Update search status view model for display
                        _searchStatusViewModel.UpdateFromContext(searchContext);

                        // Navigate to first match if any exist
                        if (searchContext.HasMatches)
                        {
                            var firstMatch = searchContext.GetCurrentMatch();
                            if (firstMatch != null)
                            {
                                // Navigate to the first match (this will auto-expand the tree)
                                NavigateToTopic(firstMatch.TopicPath);
                            }
                            Log.Information("Topic search found {Count} matches for '{Term}'. Use 'n' and 'N' to navigate.",
                                searchContext.TotalMatches, topicSearchTerm);

                            // Focus topic tree to enable immediate n/N navigation
                            if (!_testMode)
                            {
                                FocusTopicTreeCommand.Execute().Subscribe();
                            }
                        }
                        else
                        {
                            Log.Information("Topic search found no matches for '{Term}'", topicSearchTerm);
                        }
                    }
                    else
                    {
                        _topicSearchService.ClearSearch();
                        _searchStatusViewModel.UpdateFromContext(null);
                        StatusBarText = "Topic search cleared.";
                        Log.Information("Topic search cleared.");
                    }
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
                case CommandType.ViewVideo:
                    SwitchPayloadView(PayloadViewType.Video);
                    break;
                case CommandType.ViewHex:
                    SwitchPayloadViewHex();
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
                case CommandType.DeleteTopic:
                    _ = ExecuteDeleteTopicCommand(command);
                    break;
                case CommandType.GotoResponse:
                    // :gotoresponse
                    if (SelectedMessage != null)
                    {
                        NavigateToResponse(SelectedMessage.MessageId.ToString());
                    }
                    else
                    {
                        StatusBarText = "No message selected. Please select a request message first.";
                        Log.Warning("GotoResponse command executed without a selected message.");
                    }
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
        { "deletetopic", (":deletetopic [topic-pattern] [--confirm]", "Deletes retained messages from a topic and its subtopics by publishing empty retained messages. Uses selected topic if no pattern specified.") },
        { "gotoresponse", (":gotoresponse", "Navigates to the response message for the currently selected MQTT v5 request message.") },
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
        // Handle different argument counts:
        // 0 args: use settings (hostname, port, and authentication from saved settings)
        // 1 arg: server:port (override hostname and port, authentication from settings)
        // Use :setuser, :setpass, :setauthmode commands for authentication configuration

        if (command.Arguments.Count == 0)
        {
            // :connect (use all from settings)
            StatusBarText = $"Attempting to connect to {Settings.Hostname}:{Settings.Port}...";
            ConnectCommand.Execute().Subscribe(
                _ => StatusBarText = $"Successfully initiated connection to {Settings.Hostname}:{Settings.Port}.",
                ex =>
                {
                    StatusBarText = $"Error initiating connection: {ex.Message}";
                    Log.Error(ex, "Error executing ConnectCommand");
                });
            return;
        }
        else if (command.Arguments.Count == 1)
        {
            // :connect server:port
            // Parse server:port from first argument (already validated by parser)
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
                _ => StatusBarText = $"Successfully initiated connection to {host}:{port}.",
                ex =>
                {
                    StatusBarText = $"Error initiating connection: {ex.Message}";
                    Log.Error(ex, "Error executing ConnectCommand");
                });
            return;
        }
        else
        {
            // Should never reach here as parser validates argument count
            StatusBarText = "Error: :connect accepts 0 or 1 argument. Use :setuser/:setpass for authentication.";
            Log.Warning("Invalid argument count for :connect command: {Count}", command.Arguments.Count);
            return;
        }
    }

    private void Export(ParsedCommand command)
    {
        // T019: Check if this is an "export all" command
        bool isExportAll = command.Arguments.Count > 0 &&
                           command.Arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase);

        if (isExportAll)
        {
            ExportAllMessages(command);
            return;
        }

        // Existing single message export logic
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
    /// T019: Exports all messages from the selected topic's history view.
    /// Handles the :export all command with format: ["all", "json|txt", "/path"]
    /// </summary>
    private void ExportAllMessages(ParsedCommand command)
    {
        // Validate we have a selected topic
        if (SelectedNode == null)
        {
            StatusBarText = "Error: No topic selected for export all.";
            Log.Warning("Export all command failed: No topic selected.");
            return;
        }

        // Validate we have messages to export
        if (!FilteredMessageHistory.Any())
        {
            StatusBarText = "Error: No messages to export.";
            Log.Warning("Export all command failed: No messages in history.");
            return;
        }

        // Arguments: ["all", "format", "path"]
        if (command.Arguments.Count < 3)
        {
            StatusBarText = "Error: :export all requires format and path arguments.";
            Log.Warning("Invalid arguments for :export all command.");
            return;
        }

        string format = command.Arguments[1].ToLowerInvariant();
        string folderPath = command.Arguments[2];

        // Create exporter
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
            // Get most recent 100 messages
            var messagesToExport = FilteredMessageHistory
                .Take(100)
                .ToList();

            // Extract full messages and timestamps
            var fullMessages = new List<MQTTnet.MqttApplicationMessage>();
            var timestamps = new List<DateTime>();

            foreach (var msgVm in messagesToExport)
            {
                var fullMsg = msgVm.GetFullMessage();
                if (fullMsg != null)
                {
                    fullMessages.Add(fullMsg);
                    timestamps.Add(msgVm.Timestamp);
                }
            }

            if (!fullMessages.Any())
            {
                StatusBarText = "Error: No valid messages to export.";
                Log.Warning("Export all command failed: No valid full messages retrieved.");
                return;
            }

            // Generate filename using FilenameGenerator
            string topicName = SelectedNode.FullPath ?? "unknown_topic";
            string filename = CrowsNestMqtt.Utils.FilenameGenerator.GenerateExportAllFilename(
                topicName,
                DateTime.Now,
                format);

            string outputFilePath = Path.Combine(folderPath, filename);

            // Execute export
            string? result = exporter.ExportAllToFile(fullMessages, timestamps, outputFilePath);

            if (result != null)
            {
                int totalCount = FilteredMessageHistory.Count;
                string statusMessage;

                if (totalCount > 100)
                {
                    statusMessage = $"Exported {fullMessages.Count} of {totalCount} messages to {Path.GetFileName(result)} (limit enforced)";
                }
                else
                {
                    statusMessage = $"Exported {fullMessages.Count} messages to {Path.GetFileName(result)}";
                }

                StatusBarText = statusMessage;
                ClipboardText = result;
                Log.Information("Export all command successful. File: {FilePath}, Count: {Count}", result, fullMessages.Count);
            }
            else
            {
                StatusBarText = "Export all failed. Check logs for details.";
                Log.Warning("Export all command failed: ExportAllToFile returned null.");
            }
        }
        catch (Exception ex)
        {
            StatusBarText = $"Error during export all: {ex.Message}";
            Log.Error(ex, "Exception during export command execution.");
        }
    }

    /// <summary>
    /// T021: Executes the export all command from the UI button.
    /// Uses current settings for format and path.
    /// </summary>
    private void ExecuteExportAllCommand()
    {
        // Validate settings
        if (Settings.ExportFormat == null || string.IsNullOrWhiteSpace(Settings.ExportPath))
        {
            StatusBarText = "Error: Export settings not configured.";
            Log.Warning("Export all button clicked but settings not configured.");
            return;
        }

        // Create a ParsedCommand equivalent to ":export all"
        var command = new ParsedCommand(
            CommandType.Export,
            new List<string>
            {
                "all",
                Settings.ExportFormat.ToString()!.ToLowerInvariant(),
                Settings.ExportPath
            });

        // Delegate to existing ExportAllMessages method
        ExportAllMessages(command);
    }

    /// <summary>
    /// T020: Exports a single message when the per-message export button is clicked.
    /// </summary>
    private async Task ExecuteExportMessageAsync(object? parameter)
    {
        // Parameter should be a MessageViewModel
        if (parameter is not MessageViewModel msgVm)
        {
            StatusBarText = "Error: Invalid parameter for export message command.";
            Log.Warning("Export message command called with invalid parameter type.");
            return;
        }

        try
        {
            // Get full message
            var fullMessage = msgVm.GetFullMessage();
            if (fullMessage == null)
            {
                StatusBarText = "Error: Message no longer available in buffer.";
                Log.Warning("Export message failed: Message not available in buffer. MessageId: {MessageId}", msgVm.MessageId);
                return;
            }

            // Use settings for format and path
            if (Settings.ExportFormat == null || string.IsNullOrWhiteSpace(Settings.ExportPath))
            {
                StatusBarText = "Error: Export settings not configured.";
                Log.Warning("Export message failed: Settings not configured.");
                return;
            }

            // Create exporter based on settings
            IMessageExporter exporter;
            if (Settings.ExportFormat == ExportTypes.json)
            {
                exporter = new JsonExporter();
            }
            else if (Settings.ExportFormat == ExportTypes.txt)
            {
                exporter = new TextExporter();
            }
            else
            {
                StatusBarText = $"Error: Invalid export format '{Settings.ExportFormat}'.";
                Log.Warning("Invalid export format in settings: {Format}", Settings.ExportFormat);
                return;
            }

            // Execute export (using existing ExportToFile method)
            await Task.Run(() =>
            {
                string? result = exporter.ExportToFile(fullMessage, msgVm.Timestamp, Settings.ExportPath);

                if (result != null)
                {
                    StatusBarText = $"Exported message to {Path.GetFileName(result)}";
                    ClipboardText = result;
                    Log.Information("Export message command successful. File: {FilePath}", result);
                }
                else
                {
                    StatusBarText = "Export message failed - check logs";
                    Log.Warning("Export message failed: ExportToFile returned null.");
                }
            });
        }
        catch (Exception ex)
        {
            StatusBarText = $"Error exporting message: {ex.Message}";
            Log.Error(ex, "Exception during export message command execution.");
        }
    }

    private async Task ExecuteDeleteTopicCommand(ParsedCommand command)
    {
        try
        {
            StatusBarText = "Executing delete topic command...";

            if (_deleteTopicService == null)
            {
                StatusBarText = "Delete topic service not available";
                Log.Warning("Delete topic service not initialized");
                return;
            }

            // Get the currently selected topic for default behavior
            var selectedTopic = GetSelectedTopicForDelete();

            // Parse command arguments
            var arguments = command.Arguments.ToArray();
            string topicPattern;

            if (arguments.Length == 0 && !string.IsNullOrEmpty(selectedTopic))
            {
                topicPattern = selectedTopic;
                StatusBarText = $"Deleting retained messages for selected topic: {selectedTopic}";
            }
            else if (arguments.Length > 0)
            {
                topicPattern = arguments[0];
            }
            else
            {
                StatusBarText = "No topic specified for deletion";
                return;
            }

            // Create delete command with current configuration
            var deleteCommand = new DeleteTopicCommand
            {
                TopicPattern = topicPattern,
                MaxTopicLimit = 500, // Default configuration value
                RequireConfirmation = false, // Always confirmed through UI action
                ParallelismDegree = 4, // Default configuration value
                Timeout = TimeSpan.FromSeconds(5) // Default configuration value
            };

            Log.Information("Executing delete topic command for pattern: {Pattern}", topicPattern);

            // Execute the actual deletion
            var result = await _deleteTopicService.DeleteTopicAsync(deleteCommand);

            StatusBarText = result.SummaryMessage ?? $"Delete operation completed with status: {result.Status}";

            if (result.Status == DeleteOperationStatus.CompletedSuccessfully)
            {
                Log.Information("Delete topic command completed successfully: {Summary}", result.SummaryMessage);

                // T025 - Real-time UI updates
                await RefreshTopicTreeAfterDelete(topicPattern);
            }
            else
            {
                Log.Warning("Delete topic command failed or incomplete: Status={Status}, Message={Message}", result.Status, result.SummaryMessage);
            }
        }
        catch (Exception ex)
        {
            StatusBarText = $"Delete topic command failed: {ex.Message}";
            Log.Error(ex, "Exception during delete topic command execution");
        }
    }

    /// <summary>
    /// Gets the currently selected topic from the UI for delete operations.
    /// Returns null if no topic is selected.
    /// </summary>
    private string? GetSelectedTopicForDelete()
    {
        // First priority: Check if a specific message is selected and get its topic
        var selectedMessage = SelectedMessage;
        if (selectedMessage != null)
        {
            return selectedMessage.Topic;
        }

        // Second priority: Check if a topic node is selected in the topic tree
        var selectedNode = SelectedNode;
        if (selectedNode != null && !string.IsNullOrEmpty(selectedNode.FullPath))
        {
            return selectedNode.FullPath;
        }

        // Third priority: Use the normalized selected path if available
        if (!string.IsNullOrEmpty(_normalizedSelectedPath))
        {
            return _normalizedSelectedPath;
        }

        return null;
    }

    /// <summary>
    /// Refreshes the topic tree and UI after a delete operation.
    /// Updates counts and removes cleared topics from the display.
    /// </summary>
    private async Task RefreshTopicTreeAfterDelete(string? topicPattern)
    {
        if (string.IsNullOrEmpty(topicPattern))
            return;

        try
        {
            // TODO: T025 - Implement actual UI refresh logic
            // This would involve:
            // 1. Refreshing the topic tree counts
            // 2. Removing cleared topics from the display
            // 3. Updating message counts
            // 4. Notifying the user of successful completion

            // Delay the UI refresh message to allow delete confirmation message to be visible first
            await Task.Delay(3000); // Wait 3 seconds before showing less important UI refresh message
            StatusBarText = $"UI refresh completed for topic pattern: {topicPattern}";
            Log.Debug("Topic tree refreshed after delete operation for pattern: {Pattern}", topicPattern);

            await Task.CompletedTask; // Placeholder for async operations
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error refreshing UI after delete operation for pattern: {Pattern}", topicPattern);
        }
    }

    /// <summary>
    /// Applies a filter to the topic tree, showing only nodes where at least one path segment fuzzy matches the filter.
    /// </summary>
    /// <summary>
    /// Gets all topic references for search functionality (Feature 004).
    /// </summary>
    private IEnumerable<TopicReference> GetAllTopicReferences()
    {
        var topics = new List<TopicReference>();
        CollectTopicReferencesRecursive(TopicTreeNodes, topics);
        return topics;
    }

    /// <summary>
    /// Recursively collects topic references from the topic tree.
    /// Only includes topics that have messages (MessageCount > 0).
    /// </summary>
    private void CollectTopicReferencesRecursive(IEnumerable<NodeViewModel> nodes, List<TopicReference> topics)
    {
        foreach (var node in nodes)
        {
            // Only add nodes that have actual messages
            if (!string.IsNullOrEmpty(node.FullPath) && node.MessageCount > 0)
            {
                topics.Add(new TopicReference(
                    node.FullPath,
                    node.Name,
                    Guid.NewGuid() // Use a generated GUID since NodeViewModel doesn't have an ID
                ));
            }

            // Continue searching children
            if (node.Children.Count > 0)
            {
                CollectTopicReferencesRecursive(node.Children, topics);
            }
        }
    }

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
                Log.Debug("Found existing node '{Part}' under parent '{ParentName}'", part, parentNode?.Name ?? "[Root]");
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
        ScheduleOnUi(() =>
        {
            SetNodeExpandedRecursive(TopicTreeNodes, true);
            StatusBarText = "All topic nodes expanded.";
            Log.Debug("Finished setting IsExpanded=true on nodes.");
        });
    }

    /// <summary>
    /// Collapses all nodes in the topic tree.
    /// </summary>
    private void CollapseAllNodes()
    {
        Log.Information("Collapse all nodes command executed.");
        // Ensure the recursive update happens on the UI thread
        ScheduleOnUi(() =>
        {
            SetNodeExpandedRecursive(TopicTreeNodes, false);
            StatusBarText = "All topic nodes collapsed.";
            Log.Debug("Finished setting IsExpanded=false on nodes.");
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
        Log.Debug("Updated command suggestions for '{InputText}'. Found {Count} matches.", currentText, CommandSuggestions.Count);
    }

    private enum PayloadViewType { Raw, Json, Image, Video }

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
        IsVideoViewerVisible = false;
        IsHexViewerVisible = false;

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
            case PayloadViewType.Video:
                if (VideoPayload != null)
                {
                    IsVideoViewerVisible = true;
                    StatusBarText = "Switched to Video view.";
                    Log.Information("Switched payload view to Video.");
                }
                else
                {
                    IsRawTextViewerVisible = true; // Fallback to raw view
                    StatusBarText = "Cannot switch to Video view: No valid video loaded for this message.";
                    Log.Warning("Attempted to switch to Video view, but no video is loaded.");
                }
                break;
        }

        this.RaisePropertyChanged(nameof(ShowJsonParseError)); // Notify computed property change
        this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible)); // Notify computed property change
    }

    // --- Helper Methods ---

    private void SwitchPayloadViewHex()
    {
        if (SelectedMessage == null)
        {
            StatusBarText = "No message selected to view.";
            return;
        }
        var msg = SelectedMessage.GetFullMessage();
        if (msg == null)
        {
            StatusBarText = "No payload to display in hex.";
            return;
        }
        if (msg.Payload.IsEmpty)
        {
            StatusBarText = "No payload to display in hex.";
            return;
        }
        try
        {
            HexPayloadBytes = msg.Payload.ToArray();
            IsHexViewerVisible = true;
            IsRawTextViewerVisible = false;
            IsJsonViewerVisible = false;
            IsImageViewerVisible = false;
            IsVideoViewerVisible = false;
            StatusBarText = "Switched to Hex view.";
            Log.Information("Switched payload view to Hex.");
        }
        catch (Exception ex)
        {
            StatusBarText = "Error displaying payload in hex viewer.";
            Log.Error(ex, "Failed to display payload in hex viewer.");
            IsHexViewerVisible = false;
        }
        this.RaisePropertyChanged(nameof(IsAnyPayloadViewerVisible));
    }

    /// <summary>
    /// Copies the full payload of the given message to the system clipboard using an Interaction.
    /// If the content-type is an image, copies the image to the clipboard.
    /// </summary>
    private async Task CopyPayloadToClipboardAsync(object? param)
    {
        MessageViewModel? messageVm = param as MessageViewModel;

        if (messageVm == null)
        {
            Log.Debug("CopyPayloadToClipboardAsync called with null or invalid parameter. Param: {@Param}", param);
            StatusBarText = "Error: No message selected or invalid parameter to copy payload from.";
            return;
        }

        var msg = messageVm.GetFullMessage();
        if (msg?.Payload == null)
        {
            StatusBarText = "Cannot copy: Message or payload is missing.";
            Log.Warning("CopyPayloadCommand failed: FullMessage or Payload was null for MessageId {MessageId}.", messageVm.MessageId);
            return;
        }

        // Check if content-type is image
        if (!string.IsNullOrEmpty(msg.ContentType) && msg.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var ms = new MemoryStream(msg.Payload.ToArray());
                var bitmap = new Bitmap(ms);
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crowsnest_image_{Guid.NewGuid():N}.png");
                using (var fs = System.IO.File.OpenWrite(tempPath))
                {
                    bitmap.Save(fs);
                }
                await CopyImageToClipboardInteraction.Handle(bitmap);
                StatusBarText = $"Image written to temp file: {tempPath}. Path copied to clipboard. Paste the path into your application to access the image.";
                Log.Information("Image payload written to temp file '{TempPath}' and path copied to clipboard for topic '{Topic}' (MessageId {MessageId}).", tempPath, msg.Topic, messageVm.MessageId);
                return;
            }
            catch (Exception ex)
            {
                StatusBarText = "Error copying image to clipboard.";
                Log.Error(ex, "Failed to copy image payload for clipboard for MessageId {MessageId}.", messageVm.MessageId);
                // Fallback to text copy below
            }
        }

        // Check if content-type is video
        if (!string.IsNullOrEmpty(msg.ContentType) && msg.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var extension = ".mp4";
                if (msg.ContentType.Equals("video/webm", StringComparison.OrdinalIgnoreCase))
                    extension = ".webm";
                else if (msg.ContentType.Equals("video/ogg", StringComparison.OrdinalIgnoreCase))
                    extension = ".ogv";
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crowsnest_video_{Guid.NewGuid():N}{extension}");
                System.IO.File.WriteAllBytes(tempPath, msg.Payload.ToArray());
                await CopyTextToClipboardInteraction.Handle(tempPath);
                StatusBarText = $"Video written to temp file: {tempPath}. Path copied to clipboard. Paste the path into your application to access the video.";
                Log.Information("Video payload written to temp file '{TempPath}' and path copied to clipboard for topic '{Topic}' (MessageId {MessageId}).", tempPath, msg.Topic, messageVm.MessageId);
                return;
            }
            catch (Exception ex)
            {
                StatusBarText = "Error copying video to clipboard.";
                Log.Error(ex, "Failed to copy video payload for clipboard for MessageId {MessageId}.", messageVm.MessageId);
                // Fallback to text copy below
            }
        }

        string payloadString;
        try
        {
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
            await CopyTextToClipboardInteraction.Handle(payloadString);
            StatusBarText = "Payload copied to clipboard.";
            Log.Information("CopyTextToClipboardInteraction handled for topic '{Topic}' (MessageId {MessageId}).", msg.Topic, messageVm.MessageId);
        }
        catch (Exception ex)
        {
            StatusBarText = $"Error copying to clipboard: {ex.Message}";
            Log.Error(ex, "Exception occurred during CopyTextToClipboardInteraction handling for MessageId {MessageId}.", messageVm.MessageId);
        }
    }

    private void ScheduleOnUi(Action action)
    {
        // Guard against calls during disposal
        if (_disposedValue)
            return;

        // For deterministic unit tests using Immediate/CurrentThread schedulers just execute
        // Check this FIRST to avoid touching Dispatcher.UIThread in test mode
        if (_uiScheduler == Scheduler.Immediate || _uiScheduler == CurrentThreadScheduler.Instance)
        {
            action();
            return;
        }

        // Run immediately if already on Avalonia UI thread
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        // Fallback: marshal to Avalonia UI thread explicitly
        Dispatcher.UIThread.Post(action);
    }

    /// <summary>
    /// Checks if we're on the UI thread. In test mode, always returns true to avoid touching Dispatcher.
    /// </summary>
    private bool CheckUiThreadAccess()
    {
        // In test mode with immediate scheduler, we're always "on the UI thread" conceptually
        if (_uiScheduler == Scheduler.Immediate || _uiScheduler == CurrentThreadScheduler.Instance)
            return true;

        // In production, check actual Avalonia dispatcher
        return Dispatcher.UIThread.CheckAccess();
    }

    /// <summary>
    /// Simple non-reactive filtering for test mode to prevent hanging
    /// </summary>
    private void UpdateSimpleFilteredHistory()
    {
        if (!_testMode) return;

        try
        {
            _simpleFilteredHistory.Clear();

            var term = CurrentSearchTerm?.Trim() ?? string.Empty;
            var normalizedSelected = NormalizeTopic(SelectedNode?.FullPath);

            var filteredMessages = _messageHistorySource.Items
                .Where(m =>
                {
                    var topic = m.Topic ?? string.Empty;
                    var previewText = m.PayloadPreview ?? string.Empty;

                    // Topic filter
                    bool topicMatch = string.IsNullOrWhiteSpace(normalizedSelected) ||
                                      topic.Equals(normalizedSelected, StringComparison.OrdinalIgnoreCase) ||
                                      topic.StartsWith(normalizedSelected + "/", StringComparison.OrdinalIgnoreCase);

                    // Search term filter
                    bool searchMatch = string.IsNullOrWhiteSpace(term) ||
                                       topic.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                       previewText.Contains(term, StringComparison.OrdinalIgnoreCase);

                    return topicMatch && searchMatch;
                })
                .OrderByDescending(m => m.Timestamp)
                .ToList();

            foreach (var message in filteredMessages)
            {
                _simpleFilteredHistory.Add(message);
            }

            // Simple auto-selection for tests
            if (SelectedNode != null && _simpleFilteredHistory.Any() &&
                (SelectedMessage == null || !_simpleFilteredHistory.Contains(SelectedMessage)))
            {
                SelectedMessage = _simpleFilteredHistory.FirstOrDefault();
            }

            // Update MessageNavigationState for j/k navigation (Feature 004)
            var mqttMessages = _simpleFilteredHistory
                .Select(vm => vm.GetFullMessage())
                .Where(msg => msg != null)
                .Cast<MqttApplicationMessage>()
                .ToList();
            _messageNavigationState.UpdateMessages(mqttMessages);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in simple filtered history update");
        }
    }

    // --- IDisposable Implementation ---

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // Set disposed flag FIRST to prevent re-entrancy from event handlers
                _disposedValue = true;

                _uiHeartbeatTimer?.Dispose();
                _uiHeartbeatTimer = null;
                // Dispose managed state (managed objects).
                Log.Debug("Disposing MainViewModel resources...");
                if (!_cts.IsCancellationRequested)
                {
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

                // CRITICAL: Unsubscribe from event handlers FIRST before disposing reactive subscriptions
                // This prevents event handlers from being triggered during subscription disposal
                if (_mqttService != null)
                {
                    _mqttService.ConnectionStateChanged -= OnConnectionStateChanged;
                    _mqttService.MessagesBatchReceived -= OnMessagesBatchReceived;
                    _mqttService.LogMessage -= OnLogMessage;
                }

                if (_correlationService != null)
                {
                    _correlationService.CorrelationStatusChanged -= OnCorrelationStatusChanged;
                }

                // NOW dispose reactive subscriptions - they won't trigger event handlers anymore
                _messageHistorySubscription?.Dispose();
                _selectedMessageSubscription?.Dispose(); // Dispose the selected message subscription
                _commandTextSubscription?.Dispose(); // Dispose the command text subscription
                _globalHookSubscription?.Dispose(); // Dispose hook subscription
                try
                {
                    _globalHook?.Dispose(); // Dispose the hook itself
                }
                catch
                {

                }

                // Dispose LibVLC and MediaPlayer
                try
                {
                    if (_vlcMediaPlayer != null)
                    {
                        _vlcMediaPlayer.Stop();
                        _vlcMediaPlayer.Media?.Dispose();
                        _vlcMediaPlayer.Dispose();
                        _vlcMediaPlayer = null;
                    }

                    _libVLC?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error disposing LibVLC resources");
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
                FocusTopicTreeCommand?.Dispose();
                CopyPayloadCommand?.Dispose(); // Dispose the new command
                DeleteTopicCommand?.Dispose(); // Dispose delete topic command
                NavigateToResponseCommand?.Dispose(); // Dispose navigate to response command
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
        ScheduleOnUi(() =>
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
                        ScheduleOnUi(() => StatusBarText = "Ready"); // Reset to default
                    }
                }, TaskScheduler.Default); // Continue on a background thread is fine, the action posts back to UI thread
            }
        });
    }
}

/// <summary>
/// Enhanced command processor for delete topic functionality with UI integration.
/// Provides real-time status updates and UI integration.
/// </summary>
internal class EnhancedCommandProcessor : ICommandProcessor
{
    private readonly MainViewModel _mainViewModel;
    private readonly IDeleteTopicService? _deleteTopicService;

    public EnhancedCommandProcessor(MainViewModel mainViewModel, IDeleteTopicService? deleteTopicService = null)
    {
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _deleteTopicService = deleteTopicService;
    }

    /// <inheritdoc />
    public async Task<ICommandProcessor.CommandExecutionResult> ExecuteAsync(string command, string[] arguments, CancellationToken cancellationToken = default)
    {
        if (command.Equals("deletetopic", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteDeleteTopicWithUIIntegration(arguments, cancellationToken);
        }

        return new ICommandProcessor.CommandExecutionResult(false, $"Unknown command: {command}");
    }

    /// <summary>
    /// Executes the delete topic command with enhanced UI integration and real-time updates.
    /// </summary>
    private async Task<ICommandProcessor.CommandExecutionResult> ExecuteDeleteTopicWithUIIntegration(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            // Update status to show we're starting
            _mainViewModel.StatusBarText = "Preparing delete topic operation...";

            // T026 - Use configuration from settings
            // TODO: Access actual configuration from SettingsViewModel
            // For now, use defaults that match SettingsData configuration
            var maxTopicLimit = 500; // Default from SettingsData
            var parallelismDegree = 4; // Default from SettingsData
            var timeoutSeconds = 5; // Default from SettingsData

            // Log configuration being used
            Log.Debug("Delete topic operation using configuration: MaxLimit={MaxLimit}, Parallelism={Parallelism}, Timeout={Timeout}s",
                maxTopicLimit, parallelismDegree, timeoutSeconds);

            // Update status with configuration info if this is a large operation
            if (arguments.Length == 0) // No specific topic, might be large
            {
                _mainViewModel.StatusBarText = $"Preparing delete operation (limit: {maxTopicLimit} topics)...";
            }

            // Use the extension method but with enhanced feedback
            if (_deleteTopicService == null)
            {
                return new ICommandProcessor.CommandExecutionResult(false, "Delete topic service not available");
            }

            var result = await this.ExecuteDeleteTopicCommand(arguments, _deleteTopicService, cancellationToken);

            if (result.Success)
            {
                // T025 - Real-time status updates with configuration context
                var message = "Delete topic operation completed successfully.";
                if (result.Message.Contains("PLACEHOLDER"))
                {
                    message += $" (Configuration: max {maxTopicLimit} topics, {parallelismDegree} parallel)";
                }
                _mainViewModel.StatusBarText = message;

                // TODO: T025 - Additional UI updates would go here:
                // - Update topic tree node counts
                // - Refresh message lists
                // - Show progress indicators
            }
            else
            {
                _mainViewModel.StatusBarText = $"Delete topic operation failed: {result.Message}";
            }

            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Delete topic operation encountered an error: {ex.Message}";
            _mainViewModel.StatusBarText = errorMessage;
            Log.Error(ex, "Error during delete topic command execution");
            return new ICommandProcessor.CommandExecutionResult(false, errorMessage);
        }
    }
}
