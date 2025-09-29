namespace CrowsNestMqtt.BusinessLogic.Exporter;

using System.Text; // Added for Encoding
using System.Text.Json;
using System.IO;
using System.Linq; // Added for LINQ operations on UserProperties
using MQTTnet;
using CrowsNestMqtt.Utils; // For AppLogger
using System.Text.Json.Serialization;

// Define the JsonSerializerContext
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(JsonExporter.MqttMessageExportDto))]
[JsonSerializable(typeof(JsonExporter.MqttUserPropertyDto))]
internal partial class JsonExporterContext : JsonSerializerContext
{
    // Removed GeneratedSerializerOptions override to avoid conflict CS0102
}

public class JsonExporter : IMessageExporter
{
    // Define a fixed set of characters to replace for cross-platform compatibility.
    private static readonly char[] s_charactersToReplace = new char[] { ':', '?', '*', '<', '>', '/', '\\', '|', '"' };

    /// <inheritdoc />
    public ExportTypes ExporterType => ExportTypes.json;

    // Define a DTO to control serialization
    internal record MqttMessageExportDto // Changed from private to internal
    {
        public DateTime Timestamp { get; init; }
        public string Topic { get; init; } = string.Empty;
        public string? ResponseTopic { get; init; }
        public MQTTnet.Protocol.MqttQualityOfServiceLevel QualityOfServiceLevel { get; init; }
        public bool Retain { get; init; }
        public uint MessageExpiryInterval { get; init; }
        public string? CorrelationData { get; init; } // Hexadecimal string to match metadata table display
        public MQTTnet.Protocol.MqttPayloadFormatIndicator PayloadFormatIndicator { get; init; }
        public string? ContentType { get; init; }
        public List<MqttUserPropertyDto>? UserProperties { get; init; }
        public string? Payload { get; init; } // Payload as string
    }

    internal record MqttUserPropertyDto(string Name, string Value); // Changed from private to internal


    public (string content, bool isPayloadValidUtf8, string payloadAsString) GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime)
    {
        string jsonContent = string.Empty;
        string payloadAsString = "[No Payload]"; // Default value
        bool isPayloadValidUtf8 = false; // Default value
        try
        {
            // Decode payload
            if (msg.Payload.Length > 0)
            {
                try
                {
                    payloadAsString = Encoding.UTF8.GetString(msg.Payload);
                    isPayloadValidUtf8 = true;
                }
                catch (Exception decodeEx)
                {
                    payloadAsString = $"[Could not decode payload as UTF-8: {decodeEx.Message}]";
                    isPayloadValidUtf8 = false;
                    AppLogger.Warning(decodeEx, "Could not decode payload as UTF-8 for JSON export (Topic: {Topic}). Representing as error string.", msg.Topic);
                }
            }
            else
            {
                isPayloadValidUtf8 = true; // Empty payload is valid UTF-8
            }

            // Create DTO
            var dto = new MqttMessageExportDto
            {
                Timestamp = receivedTime,
                Topic = msg.Topic,
                ResponseTopic = msg.ResponseTopic,
                QualityOfServiceLevel = msg.QualityOfServiceLevel,
                Retain = msg.Retain,
                MessageExpiryInterval = msg.MessageExpiryInterval,
                CorrelationData = msg.CorrelationData != null && msg.CorrelationData.Length > 0
                    ? BitConverter.ToString(msg.CorrelationData.ToArray()).Replace("-", string.Empty)
                    : null, // Convert to hexadecimal string to match metadata table display
                PayloadFormatIndicator = msg.PayloadFormatIndicator,
                ContentType = msg.ContentType,
                UserProperties = msg.UserProperties?.Select(up => new MqttUserPropertyDto(up.Name, up.Value)).ToList(),
                Payload = isPayloadValidUtf8 ? payloadAsString : null // Only include valid UTF-8 payload in JSON DTO, otherwise null
            };

            // Use the generated context
            jsonContent = JsonSerializer.Serialize(dto, JsonExporterContext.Default.MqttMessageExportDto); // Use specific type info
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error generating JSON from MQTT message DTO (Topic: {Topic})", msg.Topic);
            jsonContent = string.Empty; // Ensure empty string on error
            // Keep payloadAsString and isPayloadValidUtf8 as they were before the serialization error
        }
        return (jsonContent, isPayloadValidUtf8, payloadAsString);
    }

    public string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath)
    {
        try
        {
            var (jsonContent, _, _) = GenerateDetailedTextFromMessage(msg, receivedTime); // Discard bool and payload string
            if (string.IsNullOrEmpty(jsonContent))
            {
                AppLogger.Warning("Skipping export for message due to empty content (topic: {Topic})", msg.Topic);
                return null;
            }

            // Ensure the export directory exists
            Directory.CreateDirectory(exportFolderPath);

            // Create a sanitized filename
            string sanitizedTopic = string.Join("_", msg.Topic.Split(s_charactersToReplace));
            string timestamp = receivedTime.ToString("yyyyMMdd_HHmmssfff");
            string filename = $"{timestamp}_{sanitizedTopic}.json";
            string filePath = Path.Combine(exportFolderPath, filename);

            File.WriteAllText(filePath, jsonContent);
            AppLogger.Information("Exported message (topic: {Topic}) to {FilePath}", msg.Topic, filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error exporting message (topic: {Topic}) to file", msg.Topic);
            return null;
        }
    }
}