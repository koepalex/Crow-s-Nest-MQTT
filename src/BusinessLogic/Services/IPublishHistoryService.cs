using CrowsNestMqtt.BusinessLogic.Models;

namespace CrowsNestMqtt.BusinessLogic.Services;

/// <summary>
/// Entry in the publish history, containing all details needed to re-publish a message.
/// </summary>
public record PublishHistoryEntry
{
    /// <summary>
    /// Unique identifier for the history entry.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The MQTT topic the message was published to.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// The message payload as text (null for binary payloads).
    /// </summary>
    public string? PayloadText { get; init; }

    /// <summary>
    /// The message payload as Base64-encoded binary (null for text payloads).
    /// </summary>
    public string? PayloadBase64 { get; init; }

    /// <summary>
    /// Quality of Service level used.
    /// </summary>
    public int QoS { get; init; } = 1;

    /// <summary>
    /// Whether the message was published with the retain flag.
    /// </summary>
    public bool Retain { get; init; }

    /// <summary>
    /// MQTT V5: Content type (MIME type).
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// MQTT V5: Payload format indicator (0 = unspecified, 1 = UTF-8).
    /// </summary>
    public int PayloadFormatIndicator { get; init; }

    /// <summary>
    /// MQTT V5: Response topic.
    /// </summary>
    public string? ResponseTopic { get; init; }

    /// <summary>
    /// MQTT V5: Correlation data as hex string.
    /// </summary>
    public string? CorrelationDataHex { get; init; }

    /// <summary>
    /// MQTT V5: Message expiry interval in seconds.
    /// </summary>
    public uint MessageExpiryInterval { get; init; }

    /// <summary>
    /// MQTT V5: User properties as key-value pairs.
    /// </summary>
    public Dictionary<string, string> UserProperties { get; init; } = new();

    /// <summary>
    /// When this message was published.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Truncated preview of the payload for display purposes.
    /// </summary>
    public string PayloadPreview =>
        PayloadText != null
            ? (PayloadText.Length > 100 ? PayloadText[..100] + "..." : PayloadText)
            : (PayloadBase64 != null ? $"[Binary: {PayloadBase64.Length * 3 / 4} bytes]" : "[Empty]");

    /// <summary>
    /// Combined display for history list: topic, QoS, retain flag, and payload preview.
    /// </summary>
    public string DisplaySummary
    {
        get
        {
            var retain = Retain ? " [R]" : "";
            var payload = PayloadText != null
                ? (PayloadText.Length > 50 ? PayloadText[..50] + "…" : PayloadText)
                : (PayloadBase64 != null ? $"[Binary]" : "[Empty]");
            return $"{Topic} | QoS {QoS}{retain} | {payload}";
        }
    }
}

/// <summary>
/// Service for managing publish message history with persistence.
/// </summary>
public interface IPublishHistoryService
{
    /// <summary>
    /// Adds a publish request to the history.
    /// </summary>
    void AddEntry(MqttPublishRequest request);

    /// <summary>
    /// Gets all history entries, most recent first.
    /// </summary>
    IReadOnlyList<PublishHistoryEntry> GetHistory();

    /// <summary>
    /// Clears all history entries.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Loads history from the persistent store.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves history to the persistent store.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Waits for any pending background saves to complete.
    /// </summary>
    Task FlushAsync();
}
