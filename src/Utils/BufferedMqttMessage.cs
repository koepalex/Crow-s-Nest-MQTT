using MQTTnet;

namespace CrowsNestMqtt.Utils;

/// <summary>
/// Represents a message stored in the ring buffer, tracking its size.
/// </summary>
public class BufferedMqttMessage
{
    public MqttApplicationMessage Message { get; }
    public int Size { get; } // Size in bytes (approximated by payload size)
    public DateTimeOffset ReceivedTimestamp { get; } // Timestamp when the message was received/buffered

    public BufferedMqttMessage(MqttApplicationMessage message, DateTimeOffset receivedTimestamp)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        ReceivedTimestamp = receivedTimestamp;
        // Approximate size using payload length. A more accurate calculation
        // might include topic length and metadata, but payload is dominant.
        // Use Payload.Length directly as it's a ReadOnlySequence<byte>.
        Size = (int)message.Payload.Length; // Cast to int if needed, ReadOnlySequence.Length is long
    }
}