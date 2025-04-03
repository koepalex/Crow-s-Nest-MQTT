namespace CrowsNestMqtt.Businesslogic.Exporter;

using System.IO;
using System.Text;
using MQTTnet;
using Serilog;
public class TextExporter : IMessageExporter
{
    /// <inheritdoc />
    public ExportTypes ExporterType => ExportTypes.Text;

    public string GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime)
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
        string payloadText = "[No Payload]"; 

        // Attempt to decode payload as UTF-8 text
        try
        {
            if (msg.Payload.Length > 0)
            {
                payloadText = Encoding.UTF8.GetString(msg.Payload); // Use overload for ReadOnlySequence<byte>
                sb.AppendLine(payloadText);
            }
            else
            {
                sb.AppendLine(payloadText); // Append "[No Payload]"
            }
        }
        catch (Exception ex) // Catch potential UTF-8 decoding errors
        {
            sb.AppendLine($"[Could not decode payload as UTF-8: {ex.Message}]");
        }

        return sb.ToString();
    }

    public string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath)
    {
        try
        {
            string textContent = GenerateDetailedTextFromMessage(msg, receivedTime);
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