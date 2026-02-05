using System.Text;
using CrowsNestMqtt.Utils;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Packets;

namespace CrowsNestMqtt.Tests.TestData;

/// <summary>
/// Generates test MQTT messages with various correlation data formats for testing export/copy functionality.
/// </summary>
public static class MqttTestDataGenerator
{
    /// <summary>
    /// Helper method to create MqttUserProperty with UTF-8 encoded value.
    /// </summary>
    private static MqttUserProperty CreateUserProperty(string name, string value) 
        => new(name, Encoding.UTF8.GetBytes(value));

    /// <summary>
    /// Creates test messages with various correlation data formats for comprehensive testing.
    /// </summary>
    public static IEnumerable<BufferedMqttMessage> GetCorrelationDataTestMessages()
    {
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var messages = new List<BufferedMqttMessage>();

        // Simple correlation ID
        messages.Add(CreateTestMessage(
            topic: "sensor/temperature/simple",
            payload: "{\"value\": 23.5, \"unit\": \"celsius\"}",
            correlationData: Encoding.UTF8.GetBytes("correlation-id-12345"),
            timestamp: timestamp.AddSeconds(1)
        ));

        // Binary correlation data
        messages.Add(CreateTestMessage(
            topic: "sensor/pressure/binary",
            payload: "{\"value\": 1013.25, \"unit\": \"hPa\"}",
            correlationData: new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD },
            timestamp: timestamp.AddSeconds(2)
        ));

        // UUID as correlation data
        var guidBytes = Guid.Parse("550e8400-e29b-41d4-a716-446655440000").ToByteArray();
        messages.Add(CreateTestMessage(
            topic: "device/status/uuid",
            payload: "{\"status\": \"online\", \"battery\": 85}",
            correlationData: guidBytes,
            timestamp: timestamp.AddSeconds(3)
        ));

        // Unicode/International characters
        messages.Add(CreateTestMessage(
            topic: "sensor/location/unicode",
            payload: "{\"location\": \"München, Deutschland\"}",
            correlationData: Encoding.UTF8.GetBytes("корреляция-测试-🔗"),
            timestamp: timestamp.AddSeconds(4)
        ));

        // Special characters and symbols
        messages.Add(CreateTestMessage(
            topic: "alert/emergency/special",
            payload: "{\"alert\": \"Fire detected!\", \"severity\": \"HIGH\"}",
            correlationData: Encoding.UTF8.GetBytes("!@#$%^&*()_+-={}|[]\\:\";'<>?,./"),
            timestamp: timestamp.AddSeconds(5)
        ));

        // Empty correlation data
        messages.Add(CreateTestMessage(
            topic: "heartbeat/empty",
            payload: "{\"heartbeat\": true}",
            correlationData: Array.Empty<byte>(),
            timestamp: timestamp.AddSeconds(6)
        ));

        // Null correlation data
        messages.Add(CreateTestMessage(
            topic: "heartbeat/null",
            payload: "{\"heartbeat\": false}",
            correlationData: null,
            timestamp: timestamp.AddSeconds(7)
        ));

        // Very long correlation data
        var longCorrelationData = Encoding.UTF8.GetBytes(new string('A', 1000) + "END");
        messages.Add(CreateTestMessage(
            topic: "bulk/data/long",
            payload: "{\"bulk\": true, \"size\": \"large\"}",
            correlationData: longCorrelationData,
            timestamp: timestamp.AddSeconds(8)
        ));

        // JSON in correlation data
        messages.Add(CreateTestMessage(
            topic: "config/update/json",
            payload: "{\"config\": \"updated\"}",
            correlationData: Encoding.UTF8.GetBytes("{\"requestId\":\"req-001\",\"user\":\"admin\"}"),
            timestamp: timestamp.AddSeconds(9)
        ));

        // Base64 encoded data in correlation
        messages.Add(CreateTestMessage(
            topic: "image/thumbnail/base64",
            payload: "{\"image\": \"thumbnail.jpg\", \"size\": 1024}",
            correlationData: Convert.FromBase64String("SGVsbG8gV29ybGQ="), // "Hello World"
            timestamp: timestamp.AddSeconds(10)
        ));

        return messages;
    }

    /// <summary>
    /// Creates test messages with various user properties for testing export functionality.
    /// </summary>
    public static IEnumerable<BufferedMqttMessage> GetUserPropertiesTestMessages()
    {
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var messages = new List<BufferedMqttMessage>();

        // Simple user properties
        var simpleProps = new List<MqttUserProperty>
        {
            CreateUserProperty("source", "sensor-001"),
            CreateUserProperty("version", "1.0.0")
        };
        messages.Add(CreateTestMessage(
            topic: "sensor/data/props",
            payload: "{\"value\": 42}",
            userProperties: simpleProps,
            correlationData: Encoding.UTF8.GetBytes("simple-props-test"),
            timestamp: timestamp.AddSeconds(11)
        ));

        // Unicode user properties
        var unicodeProps = new List<MqttUserProperty>
        {
            CreateUserProperty("位置", "北京"),
            CreateUserProperty("température", "25°C"),
            CreateUserProperty("emoji", "🌡️📊")
        };
        messages.Add(CreateTestMessage(
            topic: "sensor/international/props",
            payload: "{\"international\": true}",
            userProperties: unicodeProps,
            correlationData: Encoding.UTF8.GetBytes("unicode-props-test"),
            timestamp: timestamp.AddSeconds(12)
        ));

        return messages;
    }

    /// <summary>
    /// Creates test messages for edge cases and error scenarios.
    /// </summary>
    public static IEnumerable<BufferedMqttMessage> GetEdgeCaseTestMessages()
    {
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var messages = new List<BufferedMqttMessage>();

        // Binary payload with correlation data
        var binaryPayload = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
        messages.Add(CreateTestMessage(
            topic: "binary/image/png",
            payloadBytes: binaryPayload,
            correlationData: Encoding.UTF8.GetBytes("binary-image-test"),
            contentType: "image/png",
            timestamp: timestamp.AddSeconds(13)
        ));

        // Very large topic name
        var longTopic = "very/long/topic/path/" + string.Join("/", Enumerable.Repeat("segment", 50));
        messages.Add(CreateTestMessage(
            topic: longTopic,
            payload: "{\"longTopic\": true}",
            correlationData: Encoding.UTF8.GetBytes("long-topic-test"),
            timestamp: timestamp.AddSeconds(14)
        ));

        // All MQTT 5.0 properties filled
        messages.Add(CreateTestMessage(
            topic: "complete/mqtt5/message",
            payload: "{\"complete\": true}",
            qos: MqttQualityOfServiceLevel.ExactlyOnce,
            retain: true,
            correlationData: Encoding.UTF8.GetBytes("mqtt5-complete-test"),
            responseTopic: "response/complete/mqtt5",
            contentType: "application/json",
            messageExpiryInterval: 3600,
            payloadFormatIndicator: MqttPayloadFormatIndicator.CharacterData,
            userProperties: new List<MqttUserProperty>
            {
                CreateUserProperty("test-type", "complete"),
                CreateUserProperty("version", "5.0")
            },
            timestamp: timestamp.AddSeconds(15)
        ));

        return messages;
    }

    /// <summary>
    /// Creates all test messages for comprehensive testing.
    /// </summary>
    public static IEnumerable<BufferedMqttMessage> GetAllTestMessages()
    {
        return GetCorrelationDataTestMessages()
            .Concat(GetUserPropertiesTestMessages())
            .Concat(GetEdgeCaseTestMessages());
    }

    private static BufferedMqttMessage CreateTestMessage(
        string topic,
        string? payload = null,
        byte[]? payloadBytes = null,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        bool retain = false,
        List<MqttUserProperty>? userProperties = null,
        byte[]? correlationData = null,
        string? responseTopic = null,
        string? contentType = null,
        uint messageExpiryInterval = 0,
        MqttPayloadFormatIndicator payloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
        DateTime? timestamp = null)
    {
        var payloadData = payloadBytes ?? (payload != null ? Encoding.UTF8.GetBytes(payload) : Array.Empty<byte>());

        var message = new MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(payloadData),
            QualityOfServiceLevel = qos,
            Retain = retain,
            UserProperties = userProperties,
            CorrelationData = correlationData,
            ResponseTopic = responseTopic,
            ContentType = contentType,
            MessageExpiryInterval = messageExpiryInterval,
            PayloadFormatIndicator = payloadFormatIndicator
        };

        var msgTimestamp = timestamp ?? DateTime.UtcNow;
        return new BufferedMqttMessage(Guid.NewGuid(), message, msgTimestamp);
    }
}