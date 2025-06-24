using System.Text;

using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Utils;

using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

using Xunit;

namespace CrowsNestMqtt.UnitTests;

public class TextExporterTests : IDisposable
{
    private readonly string _testDirectory;

    public TextExporterTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_TextExporterTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Warning: Could not delete test directory {_testDirectory}. Reason: {ex.Message}");
            }
        }
        GC.SuppressFinalize(this);
    }

    // Helper to create BufferedMqttMessage
    private BufferedMqttMessage CreateTestBufferedMessage(
        string topic = "test/topic",
        string payload = "Hello MQTT",
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
        bool retain = false,
        List<MqttUserProperty>? userProperties = null,
        byte[]? correlationData = null,
        string? responseTopic = null,
        string? contentType = null,
        uint messageExpiryInterval = 0,
        MqttPayloadFormatIndicator payloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
        DateTimeOffset? timestamp = null)
    {
        var message = new MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), // Use PayloadSegment for constructor
            QualityOfServiceLevel = qos,
            Retain = retain,
            UserProperties = userProperties,
            CorrelationData = correlationData,
            ResponseTopic = responseTopic,
            ContentType = contentType,
            MessageExpiryInterval = messageExpiryInterval,
            PayloadFormatIndicator = payloadFormatIndicator
        };
        return new BufferedMqttMessage(message, timestamp ?? DateTimeOffset.UtcNow);
    }

    // Helper to build expected content based on TextExporter format
    private string BuildExpectedContent(MqttApplicationMessage msg, DateTime receivedTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Timestamp: {receivedTime:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Topic: {msg.Topic}");
        sb.AppendLine($"Response Topic: {msg.ResponseTopic}");
        sb.AppendLine($"QoS: {msg.QualityOfServiceLevel}");
        sb.AppendLine($"Message Expiry Interval: {msg.MessageExpiryInterval}");
        // Use Payload property getter which returns ReadOnlySequence<byte>
        sb.AppendLine($"Correlation Data: {(msg.CorrelationData == null || msg.CorrelationData.Length == 0 ? "" : Convert.ToBase64String(msg.CorrelationData))}"); // Handle null/empty
        sb.AppendLine($"Payload Format: {msg.PayloadFormatIndicator}");
        sb.AppendLine($"Content Type: {msg.ContentType ?? "N/A"}");
        sb.AppendLine($"Retain: {msg.Retain}");

        if (msg.UserProperties != null && msg.UserProperties.Count > 0)
        {
            sb.AppendLine("\n--- User Properties ---");
            foreach (var prop in msg.UserProperties)
            {
                sb.AppendLine($"{prop.Name}: {prop.Value}");
            }
        }

        sb.AppendLine("\n--- Payload ---");
        string payloadAsString;
        // Use Payload property getter which returns ReadOnlySequence<byte>
        if (!msg.Payload.IsEmpty)
        {
            try
            {
                // Use the overload accepting ReadOnlySequence<byte>
                payloadAsString = Encoding.UTF8.GetString(msg.Payload);
            }
            catch (Exception ex)
            {
                payloadAsString = $"[Could not decode payload as UTF-8: {ex.Message}]";
            }
        }
        else
        {
            payloadAsString = "[No Payload]";
        }
        sb.AppendLine(payloadAsString); // Append payload string or placeholder

        return sb.ToString(); // Ensure return is inside the method
    } // End of BuildExpectedContent method


    [Fact]
    public async Task ExportToFile_BasicMessage_CreatesFileWithCorrectContentAndName() // Keep async for ReadAllTextAsync
    {
        // Arrange
        var exporter = new TextExporter();
        var timestamp = new DateTimeOffset(2024, 5, 15, 10, 30, 45, 123, TimeSpan.Zero);
        var correlationBytes = new byte[] { 0x01, 0x02, 0x03 };
        var userProps = new List<MqttUserProperty> { new MqttUserProperty("Prop1", "Value1") };
        var bufferedMessage = CreateTestBufferedMessage(
            topic: "sensor/data",
            payload: "{\"temp\": 25.5}",
            qos: MqttQualityOfServiceLevel.ExactlyOnce,
            retain: true,
            userProperties: userProps,
            correlationData: correlationBytes,
            responseTopic: "sensor/data/response",
            contentType: "application/json",
            messageExpiryInterval: 3600,
            payloadFormatIndicator: MqttPayloadFormatIndicator.CharacterData,
            timestamp: timestamp);

        var expectedFilename = $"{timestamp:yyyyMMdd_HHmmssfff}_sensor_data.txt";
        var expectedContent = BuildExpectedContent(bufferedMessage.Message, timestamp.DateTime);

        // Act
        var filePath = exporter.ExportToFile(bufferedMessage.Message, bufferedMessage.ReceivedTimestamp.DateTime, _testDirectory);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(File.Exists(filePath), $"File should exist at {filePath}");
        Assert.Equal(expectedFilename, Path.GetFileName(filePath));
        var actualContent = await File.ReadAllTextAsync(filePath); // Await file read
        Assert.Equal(expectedContent.TrimEnd().Replace("\r\n", "\n"), actualContent.TrimEnd().Replace("\r\n", "\n"));
    }

    [Fact]
    public async Task ExportToFile_EmptyPayload_ExportsCorrectly() // Keep async for ReadAllTextAsync
    {
        // Arrange
        var exporter = new TextExporter();
        var timestamp = DateTimeOffset.UtcNow;
        var bufferedMessage = CreateTestBufferedMessage(payload: "", timestamp: timestamp);
        var expectedFilename = $"{timestamp:yyyyMMdd_HHmmssfff}_test_topic.txt";
        var expectedContent = BuildExpectedContent(bufferedMessage.Message, timestamp.DateTime);

        // Act
        var filePath = exporter.ExportToFile(bufferedMessage.Message, bufferedMessage.ReceivedTimestamp.DateTime, _testDirectory);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedFilename, Path.GetFileName(filePath));
        var actualContent = await File.ReadAllTextAsync(filePath); // Await file read
        Assert.Equal(expectedContent.TrimEnd().Replace("\r\n", "\n"), actualContent.TrimEnd().Replace("\r\n", "\n"));
        Assert.Contains("[No Payload]", actualContent);
    }

    [Fact]
    public async Task ExportToFile_NoUserProperties_ExportsCorrectly() // Keep async for ReadAllTextAsync
    {
        // Arrange
        var exporter = new TextExporter();
        var timestamp = DateTimeOffset.UtcNow;
        var bufferedMessage = CreateTestBufferedMessage(userProperties: null, timestamp: timestamp);
        var expectedFilename = $"{timestamp:yyyyMMdd_HHmmssfff}_test_topic.txt";
        var expectedContent = BuildExpectedContent(bufferedMessage.Message, timestamp.DateTime);

        // Act
        var filePath = exporter.ExportToFile(bufferedMessage.Message, bufferedMessage.ReceivedTimestamp.DateTime, _testDirectory);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedFilename, Path.GetFileName(filePath));
        var actualContent = await File.ReadAllTextAsync(filePath); // Await file read
        Assert.Equal(expectedContent.TrimEnd().Replace("\r\n", "\n"), actualContent.TrimEnd().Replace("\r\n", "\n"));
        Assert.DoesNotContain("--- User Properties ---", actualContent);
    }

    [Fact]
    public async Task ExportToFile_NoCorrelationData_ExportsCorrectly() // Keep async for ReadAllTextAsync
    {
        // Arrange
        var exporter = new TextExporter();
        var timestamp = DateTimeOffset.UtcNow;
        // Pass empty byte array for CorrelationData instead of null, as MqttApplicationMessage expects byte[]? but getter returns ReadOnlySequence<byte>
        var bufferedMessage = CreateTestBufferedMessage(correlationData: Array.Empty<byte>(), timestamp: timestamp);
        var expectedFilename = $"{timestamp:yyyyMMdd_HHmmssfff}_test_topic.txt";
        var expectedContent = BuildExpectedContent(bufferedMessage.Message, timestamp.DateTime);

        // Act
        var filePath = exporter.ExportToFile(bufferedMessage.Message, bufferedMessage.ReceivedTimestamp.DateTime, _testDirectory);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedFilename, Path.GetFileName(filePath));
        var actualContent = await File.ReadAllTextAsync(filePath); // Await file read
        Assert.Equal(expectedContent.TrimEnd().Replace("\r\n", "\n"), actualContent.TrimEnd().Replace("\r\n", "\n"));
        // Check the line specifically, accounting for potential line ending differences
        Assert.Contains($"Correlation Data: \n", actualContent.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ExportToFile_HandlesTopicSlashesInFilename() // Synchronous test
    {
        // Arrange
        var exporter = new TextExporter();
        var timestamp = DateTimeOffset.UtcNow;
        var bufferedMessage = CreateTestBufferedMessage(topic: "a/b/c", timestamp: timestamp);
        var expectedFilename = $"{timestamp:yyyyMMdd_HHmmssfff}_a_b_c.txt";

        // Act
        var filePath = exporter.ExportToFile(bufferedMessage.Message, bufferedMessage.ReceivedTimestamp.DateTime, _testDirectory);

        // Assert
        Assert.NotNull(filePath);
        Assert.Equal(expectedFilename, Path.GetFileName(filePath));
        Assert.True(File.Exists(filePath));
    }

     [Fact]
    public void ExportToFile_HandlesInvalidFilenameCharsInTopic() // Synchronous test
    {
        // Arrange
        var exporter = new TextExporter();
        var timestamp = DateTimeOffset.UtcNow;
        var invalidTopic = "topic:with?invalid*chars<>";
        var expectedSanitizedTopicPart = "topic_with_invalid_chars__";
        var bufferedMessage = CreateTestBufferedMessage(topic: invalidTopic, timestamp: timestamp);
        var expectedFilename = $"{timestamp:yyyyMMdd_HHmmssfff}_{expectedSanitizedTopicPart}.txt";

        // Act
        var filePath = exporter.ExportToFile(bufferedMessage.Message, bufferedMessage.ReceivedTimestamp.DateTime, _testDirectory);

        // Assert
        Assert.NotNull(filePath);
        Assert.True(File.Exists(filePath));
        Assert.Equal(expectedFilename, Path.GetFileName(filePath));
    }
} // End of TextExporterTests class