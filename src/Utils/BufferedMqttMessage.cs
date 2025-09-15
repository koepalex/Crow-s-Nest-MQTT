// Copyright (c) 2025 Crow's Nest MQTT
using MQTTnet;

namespace CrowsNestMqtt.Utils;

/// <summary>
/// DTO for a buffered MQTT message with its unique ID.
/// </summary>
public class BufferedMqttMessage
{
    public Guid MessageId { get; }
    public MqttApplicationMessage Message { get; }
    public DateTime ReceivedTimestamp { get; }

    public BufferedMqttMessage(Guid messageId, MqttApplicationMessage message, DateTime receivedTimestamp)
    {
        MessageId = messageId;
        Message = message;
        ReceivedTimestamp = receivedTimestamp;
    }

    public BufferedMqttMessage(Guid messageId, MqttApplicationMessage message)
    {
        MessageId = messageId;
        Message = message;
        ReceivedTimestamp = DateTime.Now;
    }
}
