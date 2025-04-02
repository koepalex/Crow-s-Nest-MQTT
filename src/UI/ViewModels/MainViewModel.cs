using ReactiveUI;
using Serilog; // Added Serilog
using System.Collections.ObjectModel;
using System.Threading;
using System;
using System.Linq; // For LINQ operations
using System.Text; // For Encoding and StringBuilder
using System.Reactive; // Required for Unit
using System.Reactive.Linq; // Required for Select, ObserveOn
using Avalonia.Threading; // Required for Dispatcher.UIThread
using CrowsNestMqtt.BusinessLogic; // Required for MqttEngine, MqttConnectionStateChangedEventArgs
using MQTTnet; // Required for MqttApplicationMessage, MqttApplicationMessageReceivedEventArgs
namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for the main application window.
/// Manages the different sections of the UI: Topic List, Message History, Message Details, and Command Bar.
/// </summary>
// Simple ViewModel for displaying a topic in the list
public class TopicViewModel : ReactiveObject
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private int _messageCount;
    public int MessageCount
    {
        get => _messageCount;
        set => this.RaiseAndSetIfChanged(ref _messageCount, value);
    }

    // Override ToString for simpler binding if needed, or use DisplayMemberPath
    public override string ToString() => Name;
}

// Simple ViewModel for displaying a message in the history
public class MessageViewModel : ReactiveObject
{
    public DateTime Timestamp { get; set; }
    public string PayloadPreview { get; set; } = string.Empty;
    public MqttApplicationMessage? FullMessage { get; set; } // Store the full message for details view

    public string DisplayText => $"{Timestamp:HH:mm:ss.fff}: {PayloadPreview}";
}


public class MainViewModel : ReactiveViewModel
{
    private readonly MqttEngine _mqttEngine;
    private Timer? _updateTimer;
    private readonly SynchronizationContext? _syncContext; // To post updates to the UI thread

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
            // Load history when a node is selected
            LoadMessageHistory(_selectedNode?.FullPath); // Pass full path if NodeViewModel has it, or reconstruct
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

    private bool _isSettingsVisible = true; // Start with settings visible
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        set => this.RaiseAndSetIfChanged(ref _isSettingsVisible, value);
    }

    // --- Collections ---
    // Replaced Topics with TopicTreeNodes for the TreeView
    public ObservableCollection<NodeViewModel> TopicTreeNodes { get; } = new();
    public ObservableCollection<MessageViewModel> MessageHistory { get; } = new();

    // --- Commands ---
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseResumeCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; } // Added Settings Command


    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// Sets up placeholder data and starts the UI update timer.
    /// </summary>
    public MainViewModel()
    {
        _syncContext = SynchronizationContext.Current; // Capture sync context
        Settings = new SettingsViewModel(); // Instantiate settings

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
        // ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, this.WhenAnyValue(x => x.IsConnected).Select(connected => !connected)); // Original with CanExecute
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync, this.WhenAnyValue(x => x.IsConnected));
        ClearHistoryCommand = ReactiveCommand.Create(ClearHistory);
        PauseResumeCommand = ReactiveCommand.Create(TogglePause);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings); // Initialize Settings Command

        // --- Property Change Reactions ---

        // Removed reaction to SelectedTopic, handled in SelectedNode setter now

        // When SelectedMessage changes, update the MessageDetails
        this.WhenAnyValue(x => x.SelectedMessage)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(selected => UpdateMessageDetails(selected));

        // Start connection attempt
        ConnectCommand.Execute().Subscribe();
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
                // Topics.Clear(); // Decide if topics should clear on disconnect
                // MessageHistory.Clear();
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

            // Add message to history if the received topic matches the selected node's path
            // Assumes NodeViewModel has a FullPath property or similar
            if (SelectedNode != null && topic.StartsWith(SelectedNode.FullPath)) // Or exact match depending on desired behavior
            {
                 AddMessageToHistory(e.ApplicationMessage);
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


    // Updated LoadMessageHistory to accept topic path string
    private void LoadMessageHistory(string? topicPath)
    {
        MessageHistory.Clear();
        SelectedMessage = null; // Clear message selection when topic changes

        if (string.IsNullOrEmpty(topicPath)) return;

        // Get messages for the specific path or potentially include sub-topics
        // This might require adjustments in MqttEngine or filtering here
        var messages = _mqttEngine.GetMessagesForTopic(topicPath); // Assuming GetMessagesForTopic works with exact path
        if (messages != null)
        {
            foreach (var msg in messages)
            {
                AddMessageToHistory(msg); // Reuse helper
            }
        }
    }

    private void AddMessageToHistory(MqttApplicationMessage msg)
    {
        // Basic payload preview (handle potential null/empty payload)
        string preview = msg.Payload.Length > 0
            ? Encoding.UTF8.GetString(msg.Payload) // Use overload for ReadOnlySequence<byte>
            : "[No Payload]";

        // Limit preview length
        const int maxPreviewLength = 100;
        if (preview.Length > maxPreviewLength)
        {
            preview = preview.Substring(0, maxPreviewLength) + "...";
        }

        var messageVm = new MessageViewModel
        {
            // Use UtcNow for arrival time, MQTTv5 timestamp might be available in msg.Properties
            Timestamp = DateTime.Now, // Consider using MQTT timestamp if available
            PayloadPreview = preview.Replace(Environment.NewLine, " "), // Remove newlines for preview
            FullMessage = msg // Store the original message
        };
        MessageHistory.Add(messageVm);

        // Optional: Limit history size in UI
        const int maxHistoryDisplay = 500;
        if (MessageHistory.Count > maxHistoryDisplay)
        {
            MessageHistory.RemoveAt(0);
        }
    }

     private void UpdateMessageDetails(MessageViewModel? messageVm)
    {
        if (messageVm?.FullMessage == null)
        {
            MessageDetails = "Select a message to see details.";
            return;
        }

        var msg = messageVm.FullMessage;
        var sb = new StringBuilder();
        sb.AppendLine($"Timestamp: {messageVm.Timestamp:yyyy-MM-dd HH:mm:ss.fff}"); // Display arrival time
        sb.AppendLine($"Topic: {msg.Topic}");
        sb.AppendLine($"QoS: {msg.QualityOfServiceLevel}");
        sb.AppendLine($"Retain: {msg.Retain}");
        sb.AppendLine($"Dup: {msg.Dup}"); // MQTT v3 property
        // V5 Properties (check if they exist)
        sb.AppendLine($"Content Type: {msg.ContentType ?? "N/A"}");
        sb.AppendLine($"Payload Format: {msg.PayloadFormatIndicator}");
        sb.AppendLine($"Message Expiry Interval: {msg.MessageExpiryInterval}");
        // ... add more properties as needed (CorrelationData, UserProperties etc.)

        sb.AppendLine("\n--- Payload ---");
        // Attempt to decode payload as UTF-8 text
        try
        {
            string payloadText = msg.Payload.Length > 0
                ? Encoding.UTF8.GetString(msg.Payload) // Use overload for ReadOnlySequence<byte>
                : "[No Payload]";
            sb.AppendLine(payloadText);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"[Could not decode payload as UTF-8: {ex.Message}]");
            // Optionally add hex view here
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
        // Example: Refresh topic message counts if implemented in TopicViewModel
        // Log.Verbose("UI Update Tick"); // Can be noisy, use Verbose if needed

        // Example of dispatching:
        // Dispatcher.UIThread.Post(() =>
        // {
        //     if (!IsPaused)
        //     {
        //         // Update something periodically, e.g., buffer stats or message counts
        //         foreach(var topicVm in Topics)
        //         {
        //              // topicVm.MessageCount = _mqttEngine.GetMessageCountForTopic(topicVm.Name); // Needs method in engine
        //         }
        //     }
        // });
    }

    // --- Command Methods ---

    private async Task ConnectAsync()
    {
        Log.Information("Connect command executed.");
        // Rebuild connection settings from ViewModel just before connecting
        // This ensures the latest UI values are used if changed since startup.
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
             // _mqttEngine.ClearBufferForTopic(SelectedNode.FullPath); // Assuming FullPath exists
             MessageHistory.Clear();
             SelectedMessage = null;
             MessageDetails = $"History cleared for {SelectedNode.FullPath}.";
        }
        else
        {
             MessageDetails = "Select a topic node to clear its history.";
        }
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
        Log.Information("UI Updates Paused: {IsPaused}", IsPaused);
        // Optionally update UI to show paused state
    }

    private void OpenSettings()
    {
        IsSettingsVisible = !IsSettingsVisible; // Toggle the visibility
        Log.Information("Settings Visible: {IsSettingsVisible}", IsSettingsVisible);
    }

    // TODO: Add methods for other commands (ExecuteCommandText, etc.)

    // --- Helper Methods for TreeView ---

    private void UpdateOrCreateNode(string topic, bool incrementCount = true)
    {
        var segments = topic.Split('/');
        ObservableCollection<NodeViewModel> currentLevel = TopicTreeNodes;
        NodeViewModel? parentNode = null; // Keep track of parent for path building

        string currentPath = ""; // Build path as we traverse

        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            currentPath = i == 0 ? segment : $"{currentPath}/{segment}"; // Update path

            var existingNode = currentLevel.FirstOrDefault(n => n.Name == segment);

            if (existingNode == null)
            {
                var newNode = new NodeViewModel(segment) { Parent = parentNode, FullPath = currentPath };

                // Find the correct index to insert the new node to maintain alphabetical order
                int insertIndex = 0;
                while (insertIndex < currentLevel.Count && string.Compare(newNode.Name, currentLevel[insertIndex].Name, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    insertIndex++;
                }

                // Insert the node at the determined index
                currentLevel.Insert(insertIndex, newNode);
                existingNode = newNode; // Assign the newly created node to existingNode for subsequent logic
            }

            // Increment count for the current node and all its parents
            if (incrementCount)
            {
                 existingNode.MessageCount++;
                 // Optionally increment parent counts as well if desired
                 // var tempParent = parentNode;
                 // while(tempParent != null)
                 // {
                 //     tempParent.MessageCount++;
                 //     tempParent = tempParent.Parent;
                 // }
            }


            // Move to the next level
            parentNode = existingNode; // Current node becomes parent for the next iteration
            currentLevel = existingNode.Children;
        }
    }
}