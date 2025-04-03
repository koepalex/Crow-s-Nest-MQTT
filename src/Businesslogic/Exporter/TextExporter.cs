namespace CrowsNestMqtt.Businesslogic.Exporter;

using System.IO;
using System.Text;
using MQTTnet;
using Serilog;
public class TextExporter : IMessageExporter
{
    /// <inheritdoc />
    public ExportTypes ExporterType => ExportTypes.Text;

    public (string content, bool isPayloadValidUtf8, string payloadAsString) GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Timestamp: {receivedTime:yyyy-MM-dd HH:mm:ss.fff}"); 
        sb.AppendLine($"Topic: {msg.Topic}");
        sb.AppendLine($"Response Topic: {msg.ResponseTopic}");
        sb.AppendLine($"QoS: {msg.QualityOfServiceLevel}");
        sb.AppendLine($"Message Expiry Interval: {msg.MessageExpiryInterval}");
        sb.AppendLine($"Correlation Data: {msg.CorrelationData}");
        sb.AppendLine($"Payload Format: {msg.PayloadFormatIndicator}");
        sb.AppendLine($"Content Type: {msg.ContentType ?? "N/A"}");
        sb.AppendLine($"Retain: {msg.Retain}");

        // Add User Properties if they exist
        if (msg.UserProperties != null && msg.UserProperties.Count > 0)
        {
            sb.AppendLine("\n--- User Properties ---");
            foreach (var prop in msg.UserProperties)
            {
                sb.AppendLine($"{prop.Name}: {prop.Value}");
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
                sb.AppendLine(payloadAsString);
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

    public string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath)
    {
        try
        {
            var (textContent, _, _) = GenerateDetailedTextFromMessage(msg, receivedTime); // Discard the bool and payload string for file export
            if (string.IsNullOrEmpty(textContent))
            {
                Log.Warning("Skipping export for message due to empty content (topic: {Topic})", msg.Topic);
                return null;
            }

            // Ensure the export directory exists
            Directory.CreateDirectory(exportFolderPath);

            // Create a sanitized filename
            string sanitizedTopic = string.Join("_", msg.Topic.Split(Path.GetInvalidFileNameChars()));
            string timestamp = receivedTime.ToString("yyyyMMdd_HHmmssfff");
            string filename = $"{timestamp}_{sanitizedTopic}.txt"; // Use .txt extension
            string filePath = Path.Combine(exportFolderPath, filename);

            File.WriteAllText(filePath, textContent);
            Log.Information("Exported message (topic: {Topic}) to {FilePath}", msg.Topic, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting message (topic: {Topic}) to file", msg.Topic);
            return null;
        }
    }
}