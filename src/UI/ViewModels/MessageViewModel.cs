using MQTTnet; // Required for MqttApplicationMessage
using ReactiveUI;
using CrowsNestMqtt.BusinessLogic; // Assuming IMqttService is here
using CrowsNestMqtt.UI.Services; // Assuming IStatusBarService is here

namespace CrowsNestMqtt.UI.ViewModels;

// ViewModel for displaying a message summary in the history list
public class MessageViewModel : ReactiveObject
{
    private readonly IMqttService _mqttService;
    private readonly IStatusBarService _statusBarService;

    public Guid MessageId { get; }
    public string Topic { get; }
    public DateTime Timestamp { get; } // Keep the timestamp when the VM was created
    public string PayloadPreview { get; } // Store the generated preview
    public int Size { get; }

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
        IStatusBarService statusBarService)
    {
        MessageId = messageId;
        Topic = topic ?? throw new ArgumentNullException(nameof(topic));
        Timestamp = timestamp;
        PayloadPreview = payloadPreview ?? string.Empty;
        Size = size;
        _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
        _statusBarService = statusBarService ?? throw new ArgumentNullException(nameof(statusBarService));
    }

    // Method to be called when details are requested (e.g., on selection)
    public MqttApplicationMessage? GetFullMessage()
    {
        System.Diagnostics.Debug.WriteLine($"[DEBUG] GetFullMessage called for Topic='{Topic}', MessageId={MessageId}");
        if (_mqttService.TryGetMessage(Topic, MessageId, out var message))
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Message found in buffer for Topic='{Topic}', MessageId={MessageId}");
            return message;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Message NOT found in buffer for Topic='{Topic}', MessageId={MessageId}");
            _statusBarService.ShowStatus($"Message details for topic '{Topic}' (ID: {MessageId}) are no longer available in the buffer.");
            return null;
    }
}
}
