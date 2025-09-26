using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using Xunit;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for correlation data export functionality.
/// Verifies that the export interface contract is maintained for correlation data scenarios.
/// </summary>
public class CorrelationDataExportContractTests : IDisposable
{
    private readonly string _testDirectory;

    public CorrelationDataExportContractTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_ContractTests", Guid.NewGuid().ToString());
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
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not delete test directory {_testDirectory}. Reason: {ex.Message}");
            }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TextExporter_ExportToFile_MustReturnValidFilePathForCorrelationData()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);

        // Assert - Contract requirements
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(Path.IsPathFullyQualified(result));
        Assert.True(File.Exists(result));
        Assert.True(result.StartsWith(_testDirectory));
        Assert.True(result.EndsWith(".txt"));
    }

    [Fact]
    public void TextExporter_ExportToFile_MustHandleNullCorrelationDataWithoutException()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("null"));

        // Act & Assert - Should not throw
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void TextExporter_ExportToFile_MustHandleEmptyCorrelationDataWithoutException()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("empty"));

        // Act & Assert - Should not throw
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    [Fact]
    public void TextExporter_ExportToFile_MustIncludeCorrelationDataInOutput()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result);

        // Assert - Contract requires correlation data to be included
        Assert.Contains("Correlation Data:", content);

        // For simple correlation data, should contain the base64 representation
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("correlation-id-12345"));
        Assert.Contains(expectedBase64, content);
    }

    [Fact]
    public void TextExporter_ExportToFile_MustHandleBinaryCorrelationDataCorrectly()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("binary"));

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result);

        // Assert - Contract requires binary data to be properly encoded
        Assert.Contains("Correlation Data:", content);

        // Binary data should be base64 encoded
        var expectedBase64 = Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD });
        Assert.Contains(expectedBase64, content);
    }

    [Fact]
    public void TextExporter_ExportToFile_MustHandleUnicodeCorrelationDataCorrectly()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("unicode"));

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result, Encoding.UTF8);

        // Assert - Contract requires Unicode to be handled properly
        Assert.Contains("Correlation Data:", content);

        // Unicode data should be base64 encoded
        var unicodeBytes = Encoding.UTF8.GetBytes("корреляция-测试-🔗");
        var expectedBase64 = Convert.ToBase64String(unicodeBytes);
        Assert.Contains(expectedBase64, content);
    }

    [Fact]
    public void TextExporter_ExportToFile_MustHandleLargeCorrelationDataWithoutPerformanceIssues()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("long"));

        var startTime = DateTime.UtcNow;

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Contract requires reasonable performance
        Assert.True(elapsed.TotalSeconds < 5, "Export should complete within 5 seconds for large correlation data");
        Assert.NotNull(result);
        Assert.True(File.Exists(result));

        var content = File.ReadAllText(result);
        Assert.Contains("Correlation Data:", content);
    }

    [Theory]
    [InlineData(0)]      // Empty
    [InlineData(1)]      // Single byte
    [InlineData(16)]     // GUID size
    [InlineData(100)]    // Medium size
    [InlineData(1000)]   // Large size
    public void TextExporter_ExportToFile_MustHandleVariousCorrelationDataSizes(int dataSize)
    {
        // Arrange
        var exporter = new TextExporter();
        var correlationData = dataSize == 0 ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(new string('A', dataSize));

        var testMessages = MqttTestDataGenerator.GetCorrelationDataTestMessages().ToList();
        var baseMessage = testMessages.First();

        // Create a test message with specific correlation data size
        var message = new MQTTnet.MqttApplicationMessage
        {
            Topic = $"test/size/{dataSize}",
            PayloadSegment = baseMessage.Message.PayloadSegment,
            QualityOfServiceLevel = baseMessage.Message.QualityOfServiceLevel,
            Retain = baseMessage.Message.Retain,
            CorrelationData = correlationData,
            UserProperties = baseMessage.Message.UserProperties,
            ResponseTopic = baseMessage.Message.ResponseTopic,
            ContentType = baseMessage.Message.ContentType,
            MessageExpiryInterval = baseMessage.Message.MessageExpiryInterval,
            PayloadFormatIndicator = baseMessage.Message.PayloadFormatIndicator
        };

        // Act & Assert - Should not throw regardless of size
        var result = exporter.ExportToFile(message, baseMessage.ReceivedTimestamp, _testDirectory);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));

        var content = File.ReadAllText(result);
        Assert.Contains("Correlation Data:", content);

        if (dataSize > 0)
        {
            var expectedBase64 = Convert.ToBase64String(correlationData);
            Assert.Contains(expectedBase64, content);
        }
    }
}