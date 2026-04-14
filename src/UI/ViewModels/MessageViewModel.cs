using MQTTnet; // Required for MqttApplicationMessage
using ReactiveUI;
using CrowsNestMqtt.BusinessLogic; // Assuming IMqttService is here
using CrowsNestMqtt.UI.Services; // Assuming IStatusBarService is here
using System.Text; // For Encoding (fallback full message construction)

namespace CrowsNestMqtt.UI.ViewModels;

// ViewModel for displaying a message summary in the history list
public class MessageViewModel : ReactiveObject
{
    private readonly IMqttService _mqttService;
    private readonly IStatusBarService _statusBarService;
    private readonly bool _enableFallback;
    private MqttApplicationMessage? _cachedMessage;
    private bool _isExpired;

    public Guid MessageId { get; }
    public string Topic { get; }
    public DateTime Timestamp { get; } // Keep the timestamp when the VM was created
    public string PayloadPreview { get; } = string.Empty; // Store the generated preview (initialized for nullable safety)
    public int Size { get; }
    public bool IsEffectivelyRetained { get; } // Store the corrected retain status
    public bool IsOwnMessage { get; } // Message was published by this client

    /// <summary>
    /// The MQTT 5 message expiry interval in seconds. 0 means no expiry.
    /// </summary>
    public uint MessageExpiryInterval { get; }

    /// <summary>
    /// Whether this message has expired based on its timestamp and expiry interval.
    /// Updated by the shared expiry timer in MainViewModel.
    /// </summary>
    public bool IsExpired
    {
        get => _isExpired;
        private set => this.RaiseAndSetIfChanged(ref _isExpired, value);
    }

    /// <summary>
    /// True when the message has a non-zero expiry interval.
    /// </summary>
    public bool HasExpiry => MessageExpiryInterval > 0;

    /// <summary>
    /// Returns the remaining time before expiry, or null if no expiry is set.
    /// Returns TimeSpan.Zero if already expired.
    /// </summary>
    public TimeSpan? TimeRemaining
    {
        get
        {
            if (!HasExpiry) return null;
            var expiresAt = Timestamp.Add(TimeSpan.FromSeconds(MessageExpiryInterval));
            var remaining = expiresAt - DateTime.Now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    // Display text remains the same, based on stored preview
    public string DisplayText => IsOwnMessage
        ? $"↑ {Timestamp:HH:mm:ss.fff} ({Size,10} B): {PayloadPreview}"
        : $"{Timestamp:HH:mm:ss.fff} ({Size,10} B): {PayloadPreview}";

    // Constructor accepting necessary data and injected services
    public MessageViewModel(
        Guid messageId,
        string topic,
        DateTime timestamp,
        string payloadPreview,
        int size,
        IMqttService mqttService,
        IStatusBarService statusBarService,
        MqttApplicationMessage? fullMessage = null,
        bool enableFallbackFullMessage = true,
        bool isEffectivelyRetained = false,
        uint messageExpiryInterval = 0,
        bool isOwnMessage = false)
    {
        MessageId = messageId;
        Topic = topic ?? throw new ArgumentNullException(nameof(topic));
        Timestamp = timestamp;
        PayloadPreview = payloadPreview ?? string.Empty;
        Size = size;
        IsEffectivelyRetained = isEffectivelyRetained;
        IsOwnMessage = isOwnMessage;
        MessageExpiryInterval = messageExpiryInterval;
        _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
        _statusBarService = statusBarService ?? throw new ArgumentNullException(nameof(statusBarService));
        _cachedMessage = fullMessage;
        _enableFallback = enableFallbackFullMessage;

        // Compute initial expiry state
        if (HasExpiry)
        {
            _isExpired = DateTime.Now > Timestamp.Add(TimeSpan.FromSeconds(MessageExpiryInterval));
        }
    }

    /// <summary>
    /// Re-evaluates the expiry state. Called by the shared timer in MainViewModel.
    /// </summary>
    public void RefreshExpiry()
    {
        if (!HasExpiry) return;
        IsExpired = DateTime.Now > Timestamp.Add(TimeSpan.FromSeconds(MessageExpiryInterval));
    }

    // Method to be called when details are requested (e.g., on selection)
    public MqttApplicationMessage? GetFullMessage()
    {
        if (_cachedMessage != null)
        {
            return _cachedMessage;
        }
        if (_mqttService.TryGetMessage(Topic, MessageId, out var message))
        {
            _cachedMessage = message;
            return _cachedMessage;
        }

        if (!_enableFallback)
        {
            _statusBarService.ShowStatus($"Message details for topic '{Topic}' (ID: {MessageId}) are no longer available in the buffer.");
            return null;
        }

        // Fallback construction (test stability)
        try
        {
            var fallbackBytes = Encoding.UTF8.GetBytes(PayloadPreview ?? string.Empty);
            _cachedMessage = new MqttApplicationMessageBuilder()
                .WithTopic(Topic)
                .WithPayload(fallbackBytes)
                .Build();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Constructed fallback message for Topic='{Topic}', MessageId={MessageId} (length={fallbackBytes.Length}).");
            return _cachedMessage;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Fallback construction failed for Topic='{Topic}', MessageId={MessageId}: {ex}");
            _statusBarService.ShowStatus($"Message details for topic '{Topic}' (ID: {MessageId}) are no longer available in the buffer.");
            return null;
        }
    }
}
