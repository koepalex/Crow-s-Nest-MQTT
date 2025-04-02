using MQTTnet; // Required for MqttApplicationMessage
using ReactiveUI;

namespace CrowsNestMqtt.UI.ViewModels;

// Simple ViewModel for displaying a message in the history
public class MessageViewModel : ReactiveObject
{
    public DateTime Timestamp { get; set; }
    public string PayloadPreview { get; set; } = string.Empty;
    public MqttApplicationMessage? FullMessage { get; set; } // Store the full message for details view

    public string DisplayText => $"{Timestamp:HH:mm:ss.fff}: {PayloadPreview}";
}