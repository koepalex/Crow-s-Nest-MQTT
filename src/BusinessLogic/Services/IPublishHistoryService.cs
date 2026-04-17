using CrowsNestMqtt.BusinessLogic.Models;

namespace CrowsNestMqtt.BusinessLogic.Services;

/// <summary>
/// Entry in the publish history, containing all details needed to re-publish a message.
/// </summary>
public record PublishHistoryEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Topic { get; init; }
    public string? PayloadText { get; init; }
    public string? PayloadBase64 { get; init; }

    /// <summary>
    /// Original file path when the message was published from a file.
    /// </summary>
    public string? FilePath { get; init; }

    public int QoS { get; init; } = 1;
    public bool Retain { get; init; }
    public string? ContentType { get; init; }
    public int PayloadFormatIndicator { get; init; }
    public string? ResponseTopic { get; init; }
    public string? CorrelationDataHex { get; init; }
    public uint MessageExpiryInterval { get; init; }
    public Dictionary<string, string> UserProperties { get; init; } = new();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public string PayloadPreview =>
        FilePath != null
            ? $"[File: {Path.GetFileName(FilePath)}]"
            : PayloadText != null
                ? (PayloadText.Length > 100 ? PayloadText[..100] + "..." : PayloadText)
                : (PayloadBase64 != null ? $"[Binary: {PayloadBase64.Length * 3 / 4} bytes]" : "[Empty]");

    public string DisplaySummary
    {
        get
        {
            var retain = Retain ? " [R]" : "";
            var payload = FilePath != null
                ? $"[File: {Path.GetFileName(FilePath)}]"
                : PayloadText != null
                    ? (PayloadText.Length > 50 ? PayloadText[..50] + "…" : PayloadText)
                    : (PayloadBase64 != null ? "[Binary]" : "[Empty]");
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
    /// <param name="request">The publish request.</param>
    /// <param name="filePath">Optional file path when publishing from a file.</param>
    void AddEntry(MqttPublishRequest request, string? filePath = null);

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
