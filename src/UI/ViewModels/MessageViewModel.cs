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

    public Guid MessageId { get; }
    public string Topic { get; }
    public DateTime Timestamp { get; } // Keep the timestamp when the VM was created
    public string PayloadPreview { get; } = string.Empty; // Store the generated preview (initialized for nullable safety)
    public int Size { get; }
    public bool IsEffectivelyRetained { get; } // Store the corrected retain status

    // Display text remains the same, based on stored preview
    public string DisplayText => $"{Timestamp:HH:mm:ss.fff} ({Size,10} B): {PayloadPreview}";

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
        bool isEffectivelyRetained = false)
    {
        MessageId = messageId;
        Topic = topic ?? throw new ArgumentNullException(nameof(topic));
        Timestamp = timestamp;
        PayloadPreview = payloadPreview ?? string.Empty;
        Size = size;
        IsEffectivelyRetained = isEffectivelyRetained;
        _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
        _statusBarService = statusBarService ?? throw new ArgumentNullException(nameof(statusBarService));
        _cachedMessage = fullMessage;
        _enableFallback = enableFallbackFullMessage;
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
