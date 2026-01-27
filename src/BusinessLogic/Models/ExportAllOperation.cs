namespace CrowsNestMqtt.BusinessLogic.Models;

using CrowsNestMqtt.BusinessLogic.Exporter;

/// <summary>
/// Value object representing a bulk export operation for multiple MQTT messages.
/// Encapsulates all state and metadata for an export all operation.
/// </summary>
/// <remarks>
/// T014: Created as part of export all messages feature.
/// This record is immutable and includes validation logic to ensure consistent state.
/// </remarks>
public record ExportAllOperation
{
    /// <summary>
    /// Gets the topic name from which messages are being exported.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// Gets the total number of messages available for export.
    /// </summary>
    public required int MessageCount { get; init; }

    /// <summary>
    /// Gets the actual number of messages exported (may be less than MessageCount due to 100-message limit).
    /// </summary>
    public required int ExportedCount { get; init; }

    /// <summary>
    /// Gets the export format (JSON or TXT).
    /// </summary>
    public required ExportTypes ExportFormat { get; init; }

    /// <summary>
    /// Gets the full path to the output file.
    /// </summary>
    public required string OutputFilePath { get; init; }

    /// <summary>
    /// Gets the timestamp when the export operation was initiated.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether the 100-message limit was enforced.
    /// True if MessageCount > 100, false otherwise.
    /// </summary>
    public bool IsLimitExceeded => MessageCount > 100;

    /// <summary>
    /// Factory method to create an ExportAllOperation with automatic calculation of counts.
    /// </summary>
    /// <param name="topicName">The topic name being exported.</param>
    /// <param name="totalMessages">Total number of messages available.</param>
    /// <param name="exportFormat">The export format.</param>
    /// <param name="outputFilePath">The output file path.</param>
    /// <returns>A new ExportAllOperation instance.</returns>
    /// <exception cref="ArgumentException">Thrown when topicName is null or empty, or totalMessages is negative.</exception>
    public static ExportAllOperation Create(
        string topicName,
        int totalMessages,
        ExportTypes exportFormat,
        string outputFilePath)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            throw new ArgumentException("Topic name cannot be null or empty.", nameof(topicName));

        if (totalMessages < 0)
            throw new ArgumentException("Total messages cannot be negative.", nameof(totalMessages));

        if (string.IsNullOrWhiteSpace(outputFilePath))
            throw new ArgumentException("Output file path cannot be null or empty.", nameof(outputFilePath));

        // Enforce 100-message limit
        int exportedCount = Math.Min(totalMessages, 100);

        return new ExportAllOperation
        {
            TopicName = topicName,
            MessageCount = totalMessages,
            ExportedCount = exportedCount,
            ExportFormat = exportFormat,
            OutputFilePath = outputFilePath,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets a user-friendly status message for this export operation.
    /// </summary>
    /// <returns>A formatted string describing the export result.</returns>
    public string GetStatusMessage()
    {
        var fileName = Path.GetFileName(OutputFilePath);

        if (IsLimitExceeded)
        {
            return $"Exported {ExportedCount} of {MessageCount} messages to {fileName} (limit enforced)";
        }

        return $"Exported {ExportedCount} messages to {fileName}";
    }

    /// <summary>
    /// Validates the internal state of this operation.
    /// </summary>
    /// <returns>True if all validation rules pass.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(TopicName) &&
               MessageCount >= 0 &&
               ExportedCount >= 0 &&
               ExportedCount <= Math.Min(MessageCount, 100) &&
               !string.IsNullOrWhiteSpace(OutputFilePath);
    }
}
