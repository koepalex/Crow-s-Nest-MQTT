namespace CrowsNestMqtt.BusinessLogic.Exporter;

using System.IO;
using MQTTnet;
using CrowsNestMqtt.Utils;

/// <summary>
/// Base class for message exporters providing common validation and error handling.
/// T033: Created to eliminate code duplication between JsonExporter and TextExporter.
/// </summary>
public abstract class MessageExporterBase : IMessageExporter
{
    /// <inheritdoc />
    public abstract ExportTypes ExporterType { get; }

    /// <inheritdoc />
    public abstract (string content, bool isPayloadValidUtf8, string payloadAsString) GenerateDetailedTextFromMessage(
        MqttApplicationMessage msg,
        DateTime receivedTime);

    /// <inheritdoc />
    public abstract string? ExportToFile(
        MqttApplicationMessage msg,
        DateTime receivedTime,
        string exportFolderPath);

    /// <inheritdoc />
    public string? ExportAllToFile(
        IEnumerable<MqttApplicationMessage> messages,
        IEnumerable<DateTime> timestamps,
        string outputFilePath)
    {
        // Common validation
        var (messageList, timestampList) = ValidateExportAllParameters(messages, timestamps);

        // Early return if validation indicates no export needed
        if (messageList == null || timestampList == null)
        {
            return null;
        }

        // Call derived class implementation
        return ExecuteExportAll(messageList, timestampList, outputFilePath);
    }

    /// <summary>
    /// Validates common parameters for ExportAllToFile.
    /// Returns validated lists or null if export should not proceed.
    /// </summary>
    /// <exception cref="ArgumentNullException">If messages or timestamps are null.</exception>
    /// <exception cref="ArgumentException">If counts don't match.</exception>
    protected (List<MqttApplicationMessage>?, List<DateTime>?) ValidateExportAllParameters(
        IEnumerable<MqttApplicationMessage> messages,
        IEnumerable<DateTime> timestamps)
    {
        // Null checks
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));
        if (timestamps == null)
            throw new ArgumentNullException(nameof(timestamps));

        var messageList = messages.ToList();
        var timestampList = timestamps.ToList();

        // Count mismatch check
        if (messageList.Count != timestampList.Count)
        {
            throw new ArgumentException(
                $"Count mismatch: {messageList.Count} messages but {timestampList.Count} timestamps");
        }

        // Empty collection handling
        if (messageList.Count == 0)
        {
            AppLogger.Warning("ExportAllToFile called with empty message collection");
            return (null, null);
        }

        return (messageList, timestampList);
    }

    /// <summary>
    /// Executes the actual export operation with validated parameters.
    /// Derived classes implement format-specific logic here.
    /// </summary>
    /// <param name="messages">Validated, non-empty list of messages.</param>
    /// <param name="timestamps">Validated list of timestamps matching message count.</param>
    /// <param name="outputFilePath">Path where file should be written.</param>
    /// <returns>File path on success, null on error.</returns>
    protected abstract string? ExecuteExportAll(
        List<MqttApplicationMessage> messages,
        List<DateTime> timestamps,
        string outputFilePath);

    /// <summary>
    /// Wraps file write operations with common error handling.
    /// </summary>
    protected string? SafeWriteToFile(string filePath, Action writeAction, int messageCount)
    {
        try
        {
            writeAction();
            AppLogger.Information("Exported {Count} messages to {FilePath}", messageCount, filePath);
            return filePath;
        }
        catch (IOException ioEx)
        {
            AppLogger.Error(ioEx, "Failed to write export file to {Path}", filePath);
            return null;
        }
        catch (UnauthorizedAccessException authEx)
        {
            AppLogger.Error(authEx, "Access denied writing to {Path}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Unexpected error during ExportAllToFile");
            return null;
        }
    }
}
