namespace CrowsNestMqtt.BusinessLogic.Exporter;

using MQTTnet;

/// <summary>
/// Interface for exporting MQTT message details.
/// </summary>
public interface IMessageExporter
{
    /// <summary>
    /// Gets the type of the exporter.
    /// </summary>
    ExportTypes ExporterType { get; }

    /// <summary>
    /// Generates a detailed text representation of an MQTT message.
    /// </summary>
    /// <param name="msg">The MQTT application message.</param>
    /// <param name="receivedTime">The timestamp when the message was received.</param>
    /// <returns>A string containing the detailed message information.</returns>
    (string content, bool isPayloadValidUtf8, string payloadAsString) GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime);

    /// <summary>
    /// Exports the detailed representation of an MQTT message to a file.
    /// </summary>
    /// <param name="msg">The MQTT application message.</param>
    /// <param name="receivedTime">The timestamp when the message was received.</param>
    /// <param name="exportFolderPath">The path to the folder where the file should be saved.</param>
    /// <returns>The full path to the exported file, or null if export failed or was skipped.</returns>
    string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath);

    /// <summary>
    /// Exports multiple MQTT messages to a single file in aggregated format.
    /// T017: Added as part of export all messages feature.
    /// </summary>
    /// <param name="messages">Collection of MQTT messages to export. Must not be null.</param>
    /// <param name="timestamps">Corresponding receive timestamps. Count must match messages.</param>
    /// <param name="outputFilePath">Full path to output file. Will overwrite if exists.</param>
    /// <returns>
    ///   The output file path if export succeeds.
    ///   null if export fails (I/O error, validation error, or empty collection).
    /// </returns>
    /// <exception cref="ArgumentNullException">If messages or timestamps is null.</exception>
    /// <exception cref="ArgumentException">If messages/timestamps count mismatch.</exception>
    string? ExportAllToFile(
        IEnumerable<MqttApplicationMessage> messages,
        IEnumerable<DateTime> timestamps,
        string outputFilePath);
}