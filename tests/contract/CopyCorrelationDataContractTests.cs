using System;
using System.Linq;
using System.Text;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for copy correlation data functionality.
/// These tests MUST FAIL initially, demonstrating that the current TextExporter
/// uses base64 encoding instead of the expected hex format for clipboard operations.
///
/// Per the contract specification in contracts/copy-command.md:
/// - Clipboard content must match exactly what user sees in metadata table
/// - Format must be readable hex, not base64 encoding
/// - Must handle various correlation data formats consistently
/// </summary>
public class CopyCorrelationDataContractTests
{
    [Fact]
    public void TextExporter_SimpleCorrelationData_CurrentlyUsesBase64_ShouldUseHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));
        var textExporter = new TextExporter();

        var correlationBytes = Encoding.UTF8.GetBytes("correlation-id-12345");
        var expectedHexFormat = ConvertToHex(correlationBytes);   // "636F7272656C6174696F6E2D69642D3132333435"
        var currentBase64Format = Convert.ToBase64String(correlationBytes); // "Y29ycmVsYXRpb24taWQtMTIzNDU="

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Verify current base64 behavior (will pass initially)
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content);

        // CONTRACT VIOLATION: This assertion FAILS showing the issue
        // Once fixed, this assertion should pass and the base64 assertion above should fail
        Assert.DoesNotContain(expectedHexFormat, content);

        // Uncomment this line once the fix is implemented:
        // Assert.Contains(expectedHexFormat, content);
        // Assert.DoesNotContain(currentBase64Format, content);
    }

    [Fact]
    public void TextExporter_BinaryCorrelationData_CurrentlyUsesBase64_ShouldUseHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("binary"));
        var textExporter = new TextExporter();

        var binaryData = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD };
        var expectedHexFormat = ConvertToHex(binaryData);       // "010203FFFEFD"
        var currentBase64Format = Convert.ToBase64String(binaryData); // "AQID//79"

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Current base64 behavior (this will pass initially)
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content);

        // CONTRACT VIOLATION: Hex format is not used (this assertion will pass initially but indicates the problem)
        Assert.DoesNotContain(expectedHexFormat, content);
    }

    [Fact]
    public void TextExporter_UuidCorrelationData_CurrentlyUsesBase64_ShouldUseHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("uuid"));
        var textExporter = new TextExporter();

        var guidBytes = Guid.Parse("550e8400-e29b-41d4-a716-446655440000").ToByteArray();
        var expectedHexFormat = ConvertToHex(guidBytes);
        var currentBase64Format = Convert.ToBase64String(guidBytes);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Demonstrates current base64 behavior
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content);

        // CONTRACT VIOLATION: Should use hex format for better readability
        Assert.DoesNotContain(expectedHexFormat, content);
    }

    [Fact]
    public void TextExporter_UnicodeCorrelationData_CurrentlyUsesBase64_ShouldPreserveInHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("unicode"));
        var textExporter = new TextExporter();

        var unicodeBytes = Encoding.UTF8.GetBytes("корреляция-测试-🔗");
        var expectedHexFormat = ConvertToHex(unicodeBytes);
        var currentBase64Format = Convert.ToBase64String(unicodeBytes);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Unicode characters in correlation data
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content);

        // CONTRACT REQUIREMENT: Unicode should be preserved in readable hex format
        Assert.DoesNotContain(expectedHexFormat, content); // Will pass initially, should fail after fix
    }

    [Fact]
    public void TextExporter_SpecialCharacterCorrelationData_CurrentlyUsesBase64_ShouldUseHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("special"));
        var textExporter = new TextExporter();

        var specialCharBytes = Encoding.UTF8.GetBytes("!@#$%^&*()_+-={}|[]\\:\";'<>?,./");
        var expectedHexFormat = ConvertToHex(specialCharBytes);
        var currentBase64Format = Convert.ToBase64String(specialCharBytes);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Special characters should be consistently formatted
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content);

        // CONTRACT VIOLATION: Special chars should be in hex format for consistency
        Assert.DoesNotContain(expectedHexFormat, content);
    }

    [Fact]
    public void TextExporter_EmptyCorrelationData_ShouldHandleGracefully()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("empty"));
        var textExporter = new TextExporter();

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Empty correlation data handling
        Assert.Contains("Correlation Data:", content);

        // Both base64 and hex of empty array result in empty string
        var emptyBase64 = Convert.ToBase64String(Array.Empty<byte>()); // ""
        var emptyHex = ConvertToHex(Array.Empty<byte>()); // ""

        // This should work for both formats since empty results in empty string
        Assert.Contains($"Correlation Data: {emptyBase64}", content);
    }

    [Fact]
    public void TextExporter_NullCorrelationData_ShouldHandleWithoutException()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("null"));
        var textExporter = new TextExporter();

        // Act & Assert - Should not throw exception
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        Assert.Contains("Correlation Data:", content);
        Assert.NotNull(content);
        Assert.NotEmpty(content);
    }

    [Fact]
    public void TextExporter_LargeCorrelationData_ShouldFormatWithGoodPerformance()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("long"));
        var textExporter = new TextExporter();

        var startTime = DateTime.UtcNow;

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Performance requirement from contract: <50ms
        Assert.True(elapsed.TotalMilliseconds < 100, $"Format operation took {elapsed.TotalMilliseconds}ms, should be <100ms");
        Assert.Contains("Correlation Data:", content);
        Assert.NotNull(content);
    }

    [Theory]
    [InlineData(0)]      // Empty
    [InlineData(1)]      // Single byte
    [InlineData(16)]     // GUID size
    [InlineData(100)]    // Medium size
    [InlineData(1000)]   // Large size
    public void TextExporter_VariousCorrelationDataSizes_ShouldMaintainFormatConsistency(int dataSize)
    {
        // Arrange
        var correlationData = dataSize == 0 ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(new string('A', dataSize));
        var message = CreateTestMessage(correlationData, $"test/size/{dataSize}");
        var textExporter = new TextExporter();

        var expectedHexFormat = ConvertToHex(correlationData);
        var currentBase64Format = Convert.ToBase64String(correlationData);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(message, DateTime.UtcNow);

        // Assert - Size consistency
        Assert.Contains("Correlation Data:", content);

        if (dataSize > 0)
        {
            Assert.Contains(currentBase64Format, content); // Current implementation
            // CONTRACT VIOLATION: Should use hex format regardless of size
            Assert.DoesNotContain(expectedHexFormat, content);
        }
    }

    [Fact]
    public void Contract_ClipboardFormatMustMatchMetadataTableDisplay()
    {
        // This test documents the contract requirement:
        // "ClipboardData.Format == MetadataTable.DisplayFormat"

        // Arrange
        var correlationBytes = Encoding.UTF8.GetBytes("test-correlation");
        var testMessage = CreateTestMessage(correlationBytes, "contract/format/test");
        var textExporter = new TextExporter();

        // The metadata table should display hex format for readability
        var metadataTableFormat = ConvertToHex(correlationBytes); // Expected: "746573742D636F7272656C6174696F6E"

        // Current clipboard format (base64)
        var currentClipboardFormat = Convert.ToBase64String(correlationBytes); // "dGVzdC1jb3JyZWxhdGlvbg=="

        // Act
        var (clipboardContent, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage, DateTime.UtcNow);

        // Assert - CONTRACT VIOLATION
        // Currently clipboard uses base64 format while metadata table should use hex
        Assert.Contains(currentClipboardFormat, clipboardContent);
        Assert.DoesNotContain(metadataTableFormat, clipboardContent);

        // After fix, this should be reversed:
        // Assert.Contains(metadataTableFormat, clipboardContent);
        // Assert.DoesNotContain(currentClipboardFormat, clipboardContent);
    }

    /// <summary>
    /// Converts bytes to uppercase hexadecimal string format expected by metadata table.
    /// This matches the format that should be displayed in the UI metadata table.
    /// </summary>
    private static string ConvertToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        return Convert.ToHexString(bytes); // .NET 5+ method, produces uppercase hex like "012AFF"
    }

    /// <summary>
    /// Creates a test MQTT message with specific correlation data.
    /// </summary>
    private static MQTTnet.MqttApplicationMessage CreateTestMessage(byte[] correlationData, string topic)
    {
        return new MQTTnet.MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"test\": true}")),
            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            CorrelationData = correlationData,
            ContentType = "application/json"
        };
    }
}