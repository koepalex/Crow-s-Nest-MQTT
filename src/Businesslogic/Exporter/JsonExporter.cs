namespace CrowsNestMqtt.Businesslogic.Exporter;

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
    public ExportTypes ExporterType => ExportTypes.Json;

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


    public string GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime)
    {
        string json = string.Empty;
        try
        {
            // Decode payload
            string? payloadString = null;
            if (msg.Payload.Length > 0) // Removed the incorrect null check
            {
                try
                {
                    payloadString = Encoding.UTF8.GetString(msg.Payload);
                }
                catch (Exception decodeEx)
                {
                    Log.Warning(decodeEx, "Could not decode payload as UTF-8 for JSON export (Topic: {Topic}). Exporting as null.", msg.Topic);
                    // Optionally represent as Base64 or similar if UTF-8 fails? For now, null.
                }
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
                Payload = payloadString
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            json = JsonSerializer.Serialize(dto, options);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating JSON from MQTT message DTO (Topic: {Topic})", msg.Topic);
            json = string.Empty; // Ensure empty string on error
        }
        return json;
    }

    public string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath)
    {
        try
        {
            string jsonContent = GenerateDetailedTextFromMessage(msg, receivedTime);
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