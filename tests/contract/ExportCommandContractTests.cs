using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using Xunit;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for :export command correlation data functionality.
/// These tests define the expected behavior that export output format must match metadata table display format.
///
/// CRITICAL CONTRACT REQUIREMENT:
/// If metadata table shows correlation data as hexadecimal (e.g., "012AFF"),
/// export files must contain the same hexadecimal format, NOT base64 encoding.
///
/// These tests will FAIL initially because current TextExporter uses Convert.ToBase64String()
/// instead of the required BitConverter.ToString().Replace("-", "").ToUpper() format.
/// </summary>
public class ExportCommandContractTests : IDisposable
{
    private readonly string _testDirectory;

    public ExportCommandContractTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_ExportContractTests", Guid.NewGuid().ToString());
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
    public void ExportCommand_SimpleCorrelationData_MustOutputHexadecimalNotBase64()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var correlationBytes = testMessage.Message.CorrelationData!;
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();
        var currentBase64Format = Convert.ToBase64String(correlationBytes);

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - CONTRACT VIOLATION: This test will FAIL initially
        // Current implementation outputs base64, but contract requires hex format
        Assert.Contains("Correlation Data:", content);

        // This assertion will FAIL - proving current implementation violates contract
        Assert.Contains(expectedHexFormat, content);

        // This assertion confirms current broken behavior (should be removed after fix)
        Assert.DoesNotContain(currentBase64Format, content);
    }

    [Fact]
    public void ExportCommand_BinaryCorrelationData_MustOutputHexadecimalFormat()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("binary"));

        var correlationBytes = testMessage.Message.CorrelationData!;
        // UI displays as: BitConverter.ToString(data).Replace("-", "")
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - CONTRACT REQUIREMENT: Export format must match UI display
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(expectedHexFormat, content);

        // Verify specific expected format for binary test data: {0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD}
        Assert.Contains("010203FFFEFD", content);
    }

    [Fact]
    public void ExportCommand_UuidCorrelationData_MustOutputHexadecimalFormat()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("uuid"));

        var correlationBytes = testMessage.Message.CorrelationData!;
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - Contract requirement for UUID format consistency
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(expectedHexFormat, content);

        // UUID bytes should not be displayed as base64
        var base64Format = Convert.ToBase64String(correlationBytes);
        Assert.DoesNotContain(base64Format, content);
    }

    [Fact]
    public void ExportCommand_UnicodeCorrelationData_MustOutputHexadecimalFormat()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("unicode"));

        var correlationBytes = testMessage.Message.CorrelationData!;
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!, Encoding.UTF8);

        // Assert - Unicode data must also follow hex format contract
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(expectedHexFormat, content);

        // Verify it's not the UTF-8 string representation
        var unicodeString = "корреляция-测试-🔗";
        Assert.DoesNotContain(unicodeString, content);
    }

    [Fact]
    public void ExportCommand_SpecialCharactersCorrelationData_MustOutputHexadecimalFormat()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("special"));

        var correlationBytes = testMessage.Message.CorrelationData!;
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - Special characters must be hex encoded
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(expectedHexFormat, content);

        // Verify it's not the raw string with special characters
        var specialCharsString = "!@#$%^&*()_+-={}|[]\\:\";'<>?,./";
        Assert.DoesNotContain(specialCharsString, content);
    }

    [Fact]
    public void ExportCommand_EmptyCorrelationData_MustHandleGracefully()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("empty"));

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - Empty correlation data handling
        Assert.Contains("Correlation Data:", content);

        // Empty correlation data should export as empty hex string ""
        Assert.Contains("Correlation Data: ", content);

        // Should not contain base64 representation of empty array
        Assert.DoesNotContain("Correlation Data: " + Convert.ToBase64String(Array.Empty<byte>()), content);
    }

    [Fact]
    public void ExportCommand_NullCorrelationData_MustHandleGracefully()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("null"));

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - Null correlation data handling
        Assert.Contains("Correlation Data:", content);

        // Null correlation data should be handled consistently with metadata table display
        // Should not throw exceptions or produce invalid output
        Assert.NotNull(result);
        Assert.True(File.Exists(result));
    }

    [Theory]
    [InlineData(1, "41")]      // Single byte 'A' = 0x41
    [InlineData(2, "4142")]    // "AB" = 0x41, 0x42
    [InlineData(16, 32)]       // GUID size = 32 hex chars
    [InlineData(100, 200)]     // Medium size = 200 hex chars
    public void ExportCommand_VariousCorrelationDataSizes_MustOutputCorrectHexLength(int dataSize, object expectedOutput)
    {
        // Arrange
        var exporter = new TextExporter();
        var correlationData = dataSize == 1 ? new byte[] { 0x41 } :
                             dataSize == 2 ? new byte[] { 0x41, 0x42 } :
                             Encoding.UTF8.GetBytes(new string('A', dataSize));

        var testMessages = MqttTestDataGenerator.GetCorrelationDataTestMessages().ToList();
        var baseMessage = testMessages.First();

        var message = new MQTTnet.MqttApplicationMessage
        {
            Topic = $"test/size/{dataSize}",
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes("test payload")),
            QualityOfServiceLevel = baseMessage.Message.QualityOfServiceLevel,
            Retain = baseMessage.Message.Retain,
            CorrelationData = correlationData,
            UserProperties = baseMessage.Message.UserProperties,
            ResponseTopic = baseMessage.Message.ResponseTopic,
            ContentType = baseMessage.Message.ContentType,
            MessageExpiryInterval = baseMessage.Message.MessageExpiryInterval,
            PayloadFormatIndicator = baseMessage.Message.PayloadFormatIndicator
        };

        // Act
        var result = exporter.ExportToFile(message, baseMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - Hex format length verification
        Assert.Contains("Correlation Data:", content);

        var expectedHexFormat = BitConverter.ToString(correlationData).Replace("-", "").ToUpper();
        Assert.Contains(expectedHexFormat, content);

        if (expectedOutput is string expectedHex)
        {
            Assert.Contains(expectedHex, content);
        }
        else if (expectedOutput is int expectedLength)
        {
            // Find the correlation data line and verify hex length
            var lines = content.Split('\n');
            var correlationLine = lines.First(l => l.Contains("Correlation Data:"));
            var hexPart = correlationLine.Split(':')[1].Trim();
            Assert.Equal(expectedLength, hexPart.Length);
        }
    }

    [Fact]
    public void ExportCommand_JsonFormatCorrelationData_MustAlsoUseHexFormat()
    {
        // Arrange - This test verifies JSON export also follows hex format contract
        var exporter = new CrowsNestMqtt.BusinessLogic.Exporter.JsonExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var correlationBytes = testMessage.Message.CorrelationData!;
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - JSON format must also use hex for correlation data consistency
        Assert.Contains("\"CorrelationData\":", content);

        // NOTE: This test will FAIL initially because JsonExporter serializes byte[] as base64 in JSON
        // The contract requires that correlation data should be consistently formatted as hex across all export formats
        Assert.Contains($"\"{expectedHexFormat}\"", content);

        // Should not contain base64 in JSON format either (this will currently fail)
        var base64Format = Convert.ToBase64String(correlationBytes);
        Assert.DoesNotContain($"\"{base64Format}\"", content);
    }

    [Fact]
    public void ExportCommand_ContractValidation_MustMatchMetadataTableDisplayFormat()
    {
        // Arrange - This is the core contract test
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var correlationBytes = testMessage.Message.CorrelationData!;

        // This is exactly how MainViewModel displays correlation data (line 1466)
        var metadataTableDisplayFormat = BitConverter.ToString(correlationBytes).Replace("-", string.Empty);

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - CRITICAL CONTRACT: Export format MUST match metadata table display
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(metadataTableDisplayFormat, content);

        // Additional verification: ensure it's not base64
        var base64Format = Convert.ToBase64String(correlationBytes);
        Assert.DoesNotContain(base64Format, content);
    }

    [Fact]
    public void ExportCommand_ErrorHandling_MustMaintainContractWithInvalidData()
    {
        // Arrange - Test error scenarios while maintaining contract
        var exporter = new TextExporter();

        // Create message with null correlation data
        var message = new MQTTnet.MqttApplicationMessage
        {
            Topic = "test/error/handling",
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes("test")),
            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            CorrelationData = null // Null correlation data
        };

        // Act & Assert - Should not throw exceptions
        var result = exporter.ExportToFile(message, DateTime.UtcNow, _testDirectory);

        Assert.NotNull(result);
        Assert.True(File.Exists(result));

        var content = File.ReadAllText(result);
        Assert.Contains("Correlation Data:", content);

        // Even with null data, contract format expectations should be maintained
        // Should not contain error messages that break the expected format
        Assert.DoesNotContain("Exception", content);
        Assert.DoesNotContain("Error", content);
    }

    [Fact]
    public void ExportCommand_CrossPlatformConsistency_MustProduceSameHexFormat()
    {
        // Arrange - Verify cross-platform hex format consistency
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("binary"));

        var correlationBytes = testMessage.Message.CorrelationData!;

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var content = File.ReadAllText(result!);

        // Assert - Platform-independent hex format
        var expectedHexFormat = BitConverter.ToString(correlationBytes).Replace("-", "").ToUpper();
        Assert.Contains(expectedHexFormat, content);

        // Verify consistent uppercase hex format (not lowercase)
        Assert.DoesNotContain(expectedHexFormat.ToLower(), content);

        // Verify no platform-specific encoding artifacts
        Assert.DoesNotContain("\\x", content);
        Assert.DoesNotContain("0x", content);
    }
}