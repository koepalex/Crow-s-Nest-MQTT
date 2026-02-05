namespace CrowsNestMqtt.BusinessLogic.Exporter;

using System.IO;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Packets;
using CrowsNestMqtt.Utils; // For AppLogger

public class TextExporter : MessageExporterBase
{
    // Define a fixed set of characters to replace for cross-platform compatibility.
    // This set includes characters that are commonly invalid in filenames on various OS
    // and specifically those causing issues in the failing unit test (:, ?, *, <, >).
    private static readonly char[] s_charactersToReplace = new char[] { ':', '?', '*', '<', '>', '/', '\\', '|', '"' };

    /// <inheritdoc />
    public override ExportTypes ExporterType => ExportTypes.txt;

    public override (string content, bool isPayloadValidUtf8, string payloadAsString) GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime)
    {
        var sb = new StringBuilder();
        var correlationData = msg.CorrelationData?.ToArray() ?? Array.Empty<byte>();
        sb.AppendLine($"Timestamp: {receivedTime:yyyy-MM-dd HH:mm:ss.fff}"); 
        sb.AppendLine($"Topic: {msg.Topic}");
        sb.AppendLine($"Response Topic: {msg.ResponseTopic}");
        sb.AppendLine($"QoS: {msg.QualityOfServiceLevel}");
        sb.AppendLine($"Message Expiry Interval: {msg.MessageExpiryInterval}");
        if (correlationData.Length > 0)
        {
            sb.AppendLine($"Correlation Data: {BitConverter.ToString(correlationData).Replace("-", string.Empty)}");
        }
        sb.AppendLine($"Payload Format: {msg.PayloadFormatIndicator}");
        sb.AppendLine($"Content Type: {msg.ContentType ?? "N/A"}");
        sb.AppendLine($"Retain: {msg.Retain}");

        // Add User Properties if they exist
        if (msg.UserProperties != null && msg.UserProperties.Count > 0)
        {
            sb.AppendLine("\n--- User Properties ---");
            foreach (var prop in msg.UserProperties)
            {
                sb.AppendLine($"{prop.Name}: {prop.ReadValueAsString()}");
            }
        }

        sb.AppendLine("\n--- Payload ---");
        string payloadAsString = "[No Payload]";
        bool isPayloadValidUtf8 = false;

        // Attempt to decode payload as UTF-8 text
        try
        {
            if (msg.Payload.Length > 0)
            {
                payloadAsString = Encoding.UTF8.GetString(msg.Payload); // Use overload for ReadOnlySequence<byte>
                isPayloadValidUtf8 = true;
                
                // Check if content type is application/json and attempt to pretty-print
                if (string.Equals(msg.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // Parse and pretty-print JSON
                        using var document = JsonDocument.Parse(payloadAsString);
                        var prettyJson = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions 
                        { 
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        });
                        sb.AppendLine(prettyJson);
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, fall back to raw payload
                        sb.AppendLine(payloadAsString);
                    }
                }
                else
                {
                    // Not JSON content type, use raw payload
                    sb.AppendLine(payloadAsString);
                }
            }
            else
            {
                sb.AppendLine(payloadAsString); // Append "[No Payload]"
                isPayloadValidUtf8 = true; // Empty payload is valid UTF-8
            }
        }
        catch (Exception ex) // Catch potential UTF-8 decoding errors
        {
            payloadAsString = $"[Could not decode payload as UTF-8: {ex.Message}]";
            isPayloadValidUtf8 = false;
            sb.AppendLine(payloadAsString);
        }

        return (sb.ToString(), isPayloadValidUtf8, payloadAsString);
    }

    public override string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath)
    {
        try
        {
            var (textContent, _, _) = GenerateDetailedTextFromMessage(msg, receivedTime); // Discard the bool and payload string for file export
            if (string.IsNullOrEmpty(textContent))
            {
                AppLogger.Warning("Skipping export for message due to empty content (topic: {Topic})", msg.Topic);
                return null;
            }

            // Ensure the export directory exists
            Directory.CreateDirectory(exportFolderPath);

            // Create a sanitized filename
            string sanitizedTopic = string.Join("_", msg.Topic.Split(s_charactersToReplace));
            string timestamp = receivedTime.ToString("yyyyMMdd_HHmmssfff"); // Use Windows-compatible format
            string filename = $"{timestamp}_{sanitizedTopic}.txt"; // Use .txt extension
            string filePath = Path.Combine(exportFolderPath, filename);

            File.WriteAllText(filePath, textContent);
            AppLogger.Information("Exported message (topic: {Topic}) to {FilePath}", msg.Topic, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error exporting message (topic: {Topic}) to file", msg.Topic);
            return null;
        }
    }

    /// <summary>
    /// T033: Refactored to use base class validation and error handling.
    /// </summary>
    protected override string? ExecuteExportAll(
        List<MqttApplicationMessage> messages,
        List<DateTime> timestamps,
        string outputFilePath)
    {
        var sb = new StringBuilder();
        const string delimiter = "\n" + "================================================================================\n\n";

        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            var receivedTime = timestamps[i];

            // Generate detailed text for this message
            var (messageContent, _, _) = GenerateDetailedTextFromMessage(msg, receivedTime);
            sb.Append(messageContent);

            // Add delimiter between messages (but not after the last one)
            if (i < messages.Count - 1)
            {
                sb.Append(delimiter);
            }
        }

        // Write using base class error handling
        return SafeWriteToFile(outputFilePath, () =>
        {
            File.WriteAllText(outputFilePath, sb.ToString());
        }, messages.Count);
    }
}
