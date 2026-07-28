using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Integration tests for export functionality with correlation data.
/// Tests the complete flow from MQTT message receipt to export file generation.
/// </summary>
public sealed class ExportCorrelationDataIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly MqttTestUtilities _mqttUtils;
    private readonly string _testDirectory;

    public ExportCorrelationDataIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _mqttUtils = new MqttTestUtilities();
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_IntegrationTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public async ValueTask InitializeAsync()
    {
        var port = await _mqttUtils.StartEmbeddedBrokerAsync();
        _output.WriteLine($"Started embedded MQTT broker on port {port}");
    }

    public async ValueTask DisposeAsync()
    {
        await _mqttUtils.DisposeAsync();

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Could not delete test directory {_testDirectory}. Reason: {ex.Message}");
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExportMessages_WithVariousCorrelationData_CreatesCorrectExportFiles()
    {
        // Arrange
        var testMessages = MqttTestDataGenerator.GetCorrelationDataTestMessages().Take(5).ToList();
        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("Publisher");
        var exporter = new TextExporter();

        // Act - Publish test messages
        await MqttTestUtilities.PublishTestMessagesAsync(publisher, testMessages);

        // Wait a moment for messages to be published
        await Task.Delay(100);

        // Export each message
        var exportedFiles = new List<string>();
        foreach (var message in testMessages)
        {
            var filePath = exporter.ExportToFile(message.Message, message.ReceivedTimestamp, _testDirectory);
            if (filePath != null)
            {
                exportedFiles.Add(filePath);
            }
        }

        // Assert
        Assert.Equal(5, exportedFiles.Count);

        foreach (var filePath in exportedFiles)
        {
            Assert.True(File.Exists(filePath), $"Export file should exist: {filePath}");

            var content = await File.ReadAllTextAsync(filePath);
            Assert.NotEmpty(content);

            // Verify correlation data appears in export
            if (filePath.Contains("simple"))
            {
                var expectedHex = Convert.ToHexString(Encoding.UTF8.GetBytes("correlation-id-12345"));
                Assert.Contains($"Correlation Data: {expectedHex}", content);
            }
            else if (filePath.Contains("binary"))
            {
                var expectedHex = Convert.ToHexString(new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD });
                Assert.Contains($"Correlation Data: {expectedHex}", content);
            }
        }
    }

    [Fact]
    public async Task ExportMessages_WithUnicodeCorrelationData_HandlesEncodingCorrectly()
    {
        // Arrange
        var unicodeMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("unicode"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("UnicodePublisher");
        var exporter = new TextExporter();

        // Act
        await MqttTestUtilities.PublishTestMessagesAsync(publisher, new[] { unicodeMessage });
        await Task.Delay(50);

        var filePath = exporter.ExportToFile(unicodeMessage.Message, unicodeMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(filePath));

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        Assert.Contains("Correlation Data:", content);

        // Verify Unicode characters are exported as hex without separators.
        var unicodeBytes = Encoding.UTF8.GetBytes("корреляция-测试-🔗");
        var expectedHex = Convert.ToHexString(unicodeBytes);
        Assert.Contains($"Correlation Data: {expectedHex}", content);
    }

    [Fact]
    public async Task ExportMessages_WithEmptyAndNullCorrelationData_HandlesCorrectly()
    {
        // Arrange
        var emptyCorrelationMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("empty"));
        var nullCorrelationMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("null"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("EmptyPublisher");
        var exporter = new TextExporter();

        // Act
        await MqttTestUtilities.PublishTestMessagesAsync(publisher, new[] { emptyCorrelationMessage, nullCorrelationMessage });
        await Task.Delay(50);

        var emptyFilePath = exporter.ExportToFile(emptyCorrelationMessage.Message, emptyCorrelationMessage.ReceivedTimestamp, _testDirectory);
        var nullFilePath = exporter.ExportToFile(nullCorrelationMessage.Message, nullCorrelationMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(emptyFilePath));
        Assert.True(File.Exists(nullFilePath));

        var emptyContent = await File.ReadAllTextAsync(emptyFilePath);
        var nullContent = await File.ReadAllTextAsync(nullFilePath);

        // Both should handle empty/null correlation data gracefully by omitting the optional field.
        Assert.DoesNotContain("Correlation Data:", emptyContent);
        Assert.DoesNotContain("Correlation Data:", nullContent);
    }

    [Fact]
    public async Task ExportMessages_WithLargeCorrelationData_HandlesCorrectly()
    {
        // Arrange
        var largeCorrelationMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("long"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("LargePublisher");
        var exporter = new TextExporter();

        // Act
        await MqttTestUtilities.PublishTestMessagesAsync(publisher, new[] { largeCorrelationMessage });
        await Task.Delay(50);

        var filePath = exporter.ExportToFile(largeCorrelationMessage.Message, largeCorrelationMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(filePath));

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("Correlation Data:", content);

        // Should contain the large correlation data as hex without separators.
        var expectedBytes = Encoding.UTF8.GetBytes(new string('A', 1000) + "END");
        var expectedHex = Convert.ToHexString(expectedBytes);
        Assert.Contains($"Correlation Data: {expectedHex}", content);
    }

    [Fact]
    public async Task ExportMessages_WithComplexMqtt5Properties_ExportsAllFields()
    {
        // Arrange
        var complexMessage = MqttTestDataGenerator.GetEdgeCaseTestMessages()
            .First(m => m.Message.Topic.Contains("complete"));

        var publisher = await _mqttUtils.CreateConnectedTestClientAsync("ComplexPublisher");
        var exporter = new TextExporter();

        // Act
        await MqttTestUtilities.PublishTestMessagesAsync(publisher, new[] { complexMessage });
        await Task.Delay(50);

        var filePath = exporter.ExportToFile(complexMessage.Message, complexMessage.ReceivedTimestamp, _testDirectory);

        // Assert
        Assert.True(File.Exists(filePath));

        var content = await File.ReadAllTextAsync(filePath);

        // Verify all MQTT 5.0 properties are exported
        Assert.Contains("Topic: complete/mqtt5/message", content);
        Assert.Contains("Response Topic: response/complete/mqtt5", content);
        Assert.Contains("QoS: ExactlyOnce", content);
        Assert.Contains("Retain: True", content);
        Assert.Contains("Correlation Data:", content);
        Assert.Contains("Content Type: application/json", content);
        Assert.Contains("Message Expiry Interval: 3600", content);
        Assert.Contains("Payload Format: CharacterData", content);

        // Verify user properties
        Assert.Contains("--- User Properties ---", content);
        Assert.Contains("test-type: complete", content);
        Assert.Contains("version: 5.0", content);
    }
}