using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace CrowsNestMqtt.BusinessLogic.Models;

/// <summary>
/// Request model for publishing an MQTT message with full MQTT V5 property support.
/// </summary>
public record MqttPublishRequest
{
    /// <summary>
    /// The MQTT topic to publish to. Required.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The message payload as a byte array.
    /// If null, PayloadText is used (encoded as UTF-8).
    /// </summary>
    public byte[]? Payload { get; init; }

    /// <summary>
    /// The message payload as text. Used when Payload is null.
    /// </summary>
    public string? PayloadText { get; init; }

    /// <summary>
    /// Quality of Service level. Default: AtLeastOnce (QoS 1).
    /// </summary>
    public MqttQualityOfServiceLevel QoS { get; init; } = MqttQualityOfServiceLevel.AtLeastOnce;

    /// <summary>
    /// Whether the message should be retained by the broker. Default: false.
    /// </summary>
    public bool Retain { get; init; } = false;

    /// <summary>
    /// MQTT V5: MIME type of the payload (e.g., "application/json").
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// MQTT V5: Indicates whether the payload is UTF-8 text or binary data.
    /// </summary>
    public MqttPayloadFormatIndicator PayloadFormatIndicator { get; init; } = MqttPayloadFormatIndicator.Unspecified;

    /// <summary>
    /// MQTT V5: Topic for the response message in request-response patterns.
    /// </summary>
    public string? ResponseTopic { get; init; }

    /// <summary>
    /// MQTT V5: Correlation data linking request to response.
    /// </summary>
    public byte[]? CorrelationData { get; init; }

    /// <summary>
    /// MQTT V5: Message lifetime in seconds. 0 means no expiry (default).
    /// </summary>
    public uint MessageExpiryInterval { get; init; } = 0;

    /// <summary>
    /// MQTT V5: Custom user properties as key-value pairs.
    /// </summary>
    public List<MqttUserProperty> UserProperties { get; init; } = new();

    /// <summary>
    /// Gets the effective payload bytes (from Payload or UTF-8 encoded PayloadText).
    /// </summary>
    public byte[] GetEffectivePayload()
    {
        if (Payload != null)
            return Payload;
        if (PayloadText != null)
            return System.Text.Encoding.UTF8.GetBytes(PayloadText);
        return Array.Empty<byte>();
    }

    /// <summary>
    /// Timestamp when the publish request was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
