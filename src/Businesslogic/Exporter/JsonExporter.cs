namespace CrowsNestMqtt.BusinessLogic.Exporter;

using System.Text; // Added for Encoding
using System.Text.Json;
using System.IO;
using System.Linq; // Added for LINQ operations on UserProperties
using MQTTnet;
using Serilog;
using System.Text.Encodings.Web;

public class JsonExporter : IMessageExporter
{
    /// <inheritdoc />
    public ExportTypes ExporterType => ExportTypes.json;

    // Define a DTO to control serialization
    private record MqttMessageExportDto
    {
        public DateTime Timestamp { get; init; }
        public string Topic { get; init; } = string.Empty;
        public string? ResponseTopic { get; init; }
        public MQTTnet.Protocol.MqttQualityOfServiceLevel QualityOfServiceLevel { get; init; }
        public bool Retain { get; init; }
        public uint MessageExpiryInterval { get; init; }
        public byte[]? CorrelationData { get; init; } // Keep as byte[]? Or Base64 string? Let's try byte[] first.
        public MQTTnet.Protocol.MqttPayloadFormatIndicator PayloadFormatIndicator { get; init; }
        public string? ContentType { get; init; }
        public List<MqttUserPropertyDto>? UserProperties { get; init; }
        public string? Payload { get; init; } // Payload as string
    }

    private record MqttUserPropertyDto(string Name, string Value);


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
                    Log.Warning(decodeEx, "Could not decode payload as UTF-8 for JSON export (Topic: {Topic}). Representing as error string.", msg.Topic);
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
                CorrelationData = msg.CorrelationData?.ToArray(), // Convert ReadOnlySequence<byte> to byte[]
                PayloadFormatIndicator = msg.PayloadFormatIndicator,
                ContentType = msg.ContentType,
                UserProperties = msg.UserProperties?.Select(up => new MqttUserPropertyDto(up.Name, up.Value)).ToList(),
                Payload = isPayloadValidUtf8 ? payloadAsString : null // Only include valid UTF-8 payload in JSON DTO, otherwise null
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            jsonContent = JsonSerializer.Serialize(dto, options);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating JSON from MQTT message DTO (Topic: {Topic})", msg.Topic);
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
                Log.Warning("Skipping export for message due to empty content (topic: {Topic})", msg.Topic);
                return null;
            }

            // Ensure the export directory exists
            Directory.CreateDirectory(exportFolderPath);

            // Create a sanitized filename
            string sanitizedTopic = string.Join("_", msg.Topic.Split(Path.GetInvalidFileNameChars()));
            string timestamp = receivedTime.ToString("yyyyMMdd_HHmmssfff");
            string filename = $"{timestamp}_{sanitizedTopic}.json";
            string filePath = Path.Combine(exportFolderPath, filename);

            File.WriteAllText(filePath, jsonContent);
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