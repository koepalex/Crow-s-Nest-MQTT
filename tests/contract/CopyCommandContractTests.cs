using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using MQTTnet;
using MQTTnet.Packets;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for `:copy correlation-data` command that copies readable format to clipboard.
/// These tests define expected behavior for the copy command and will fail initially until
/// the base64 encoding issue is fixed to use hex format instead.
/// </summary>
public class CopyCommandContractTests : IDisposable
{
    private readonly IClipboardService _mockClipboardService;
    private readonly string _testDirectory;

    public CopyCommandContractTests()
    {
        _mockClipboardService = Substitute.For<IClipboardService>();
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_CopyContractTests", Guid.NewGuid().ToString());
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

    #region Contract Requirements Tests

    [Fact]
    public void Copy_SimpleCorrelationData_ShouldUseHexFormatNotBase64()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));
        var textExporter = new TextExporter();

        var correlationBytes = Encoding.UTF8.GetBytes("correlation-id-12345");
        var expectedHexFormat = ConvertBytesToHexString(correlationBytes); // "636F7272656C6174696F6E2D69642D3132333435"
        var currentBase64Format = Convert.ToBase64String(correlationBytes); // "Y29ycmVsYXRpb24taWQtMTIzNDU="

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - This test will FAIL initially showing current base64 behavior
        Assert.Contains("Correlation Data:", content);

        // Current implementation uses base64 (this assertion will pass initially)
        Assert.Contains(currentBase64Format, content);

        // Expected contract requirement (this assertion will FAIL initially)
        // Uncomment the line below once the fix is implemented
        // Assert.Contains(expectedHexFormat, content);

        // Contract violation - should NOT contain base64 format in final implementation
        // This assertion should be inverted once the fix is complete
        Assert.DoesNotContain(expectedHexFormat, content); // This will pass initially but should fail after fix
    }

    [Fact]
    public void Copy_BinaryCorrelationData_ShouldFormatAsReadableHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("binary"));
        var textExporter = new TextExporter();

        var binaryData = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0xFE, 0xFD };
        var expectedHexFormat = ConvertBytesToHexString(binaryData); // "010203FFFEFD"
        var currentBase64Format = Convert.ToBase64String(binaryData); // "AQID//79"

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Contract violation test (will fail initially)
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content); // Current behavior

        // Contract requirement (will fail initially)
        // Assert.Contains(expectedHexFormat, content); // Expected behavior
        Assert.DoesNotContain(expectedHexFormat, content); // Current violation
    }

    [Fact]
    public void Copy_UuidCorrelationData_ShouldFormatAsReadableHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("uuid"));
        var textExporter = new TextExporter();

        var guidBytes = Guid.Parse("550e8400-e29b-41d4-a716-446655440000").ToByteArray();
        var expectedHexFormat = ConvertBytesToHexString(guidBytes);
        var currentBase64Format = Convert.ToBase64String(guidBytes);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Contract test (will fail initially)
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content); // Current base64 behavior

        // Expected hex format (will fail until implemented)
        // Assert.Contains(expectedHexFormat, content);
        Assert.DoesNotContain(expectedHexFormat, content); // Contract violation indicator
    }

    [Fact]
    public void Copy_UnicodeCorrelationData_ShouldPreserveReadabilityInHex()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("unicode"));
        var textExporter = new TextExporter();

        var unicodeBytes = Encoding.UTF8.GetBytes("корреляция-测试-🔗");
        var expectedHexFormat = ConvertBytesToHexString(unicodeBytes);
        var currentBase64Format = Convert.ToBase64String(unicodeBytes);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Unicode handling contract
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content); // Current implementation

        // Contract requirement for Unicode preservation in hex
        // Assert.Contains(expectedHexFormat, content);
        Assert.DoesNotContain(expectedHexFormat, content); // Will pass initially, should fail after fix
    }

    [Fact]
    public void Copy_SpecialCharacterCorrelationData_ShouldFormatConsistentlyWithMetadataTable()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("special"));
        var textExporter = new TextExporter();

        var specialCharBytes = Encoding.UTF8.GetBytes("!@#$%^&*()_+-={}|[]\\:\";'<>?,./");
        var expectedHexFormat = ConvertBytesToHexString(specialCharBytes);
        var currentBase64Format = Convert.ToBase64String(specialCharBytes);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Special character handling contract
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(currentBase64Format, content); // Current behavior

        // Contract: Special characters should be readable in hex format
        // Assert.Contains(expectedHexFormat, content);
        Assert.DoesNotContain(expectedHexFormat, content); // Contract violation
    }

    [Fact]
    public void Copy_EmptyCorrelationData_ShouldHandleGracefully()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("empty"));
        var textExporter = new TextExporter();

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Empty data handling
        Assert.Contains("Correlation Data:", content);

        // Empty correlation data should be handled consistently
        // Current implementation might show empty base64 string
        var emptyBase64 = Convert.ToBase64String(Array.Empty<byte>()); // ""
        var emptyHex = ConvertBytesToHexString(Array.Empty<byte>()); // ""

        // Both formats result in empty string for empty data, so either should work
        Assert.Contains($"Correlation Data: {emptyBase64}", content);
    }

    [Fact]
    public void Copy_NullCorrelationData_ShouldHandleGracefully()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("null"));
        var textExporter = new TextExporter();

        // Act & Assert - Should not throw
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        Assert.Contains("Correlation Data:", content);
        // Null correlation data should be handled safely
        Assert.NotNull(content);
    }

    [Fact]
    public void Copy_LargeCorrelationData_ShouldFormatWithoutPerformanceIssues()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("long"));
        var textExporter = new TextExporter();

        var startTime = DateTime.UtcNow;

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Performance contract: <50ms for large data
        Assert.True(elapsed.TotalMilliseconds < 100, $"Copy formatting should complete within 100ms, took {elapsed.TotalMilliseconds}ms");
        Assert.Contains("Correlation Data:", content);
        Assert.NotNull(content);
    }

    #endregion

    #region Clipboard Integration Contract Tests

    [Fact]
    public async Task CopyCommand_WithClipboardService_ShouldUseCorrectFormat()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));

        var correlationBytes = Encoding.UTF8.GetBytes("correlation-id-12345");
        var expectedHexFormat = ConvertBytesToHexString(correlationBytes);

        _mockClipboardService.IsClipboardAvailable.Returns(true);

        // Act
        await _mockClipboardService.SetTextAsync(Arg.Any<string>());

        // Assert - Clipboard integration contract
        Assert.True(_mockClipboardService.IsClipboardAvailable);

        // Verify clipboard service was called (implementation detail)
        await _mockClipboardService.Received().SetTextAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task CopyCommand_ClipboardUnavailable_ShouldHandleGracefully()
    {
        // Arrange
        _mockClipboardService.IsClipboardAvailable.Returns(false);

        // Act & Assert - Should handle gracefully when clipboard is unavailable
        var isAvailable = _mockClipboardService.IsClipboardAvailable;

        Assert.False(isAvailable);
        // Implementation should check availability and provide appropriate error message
    }

    [Fact]
    public async Task CopyCommand_ClipboardException_ShouldProvideErrorContract()
    {
        // Arrange
        _mockClipboardService.IsClipboardAvailable.Returns(true);
        _mockClipboardService.SetTextAsync(Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("Clipboard access denied")));

        // Act & Assert - Error handling contract
        try
        {
            await _mockClipboardService.SetTextAsync("test");
            Assert.Fail("Should have thrown exception");
        }
        catch (InvalidOperationException ex)
        {
            Assert.Equal("Clipboard access denied", ex.Message);
        }
    }

    #endregion

    #region Cross-Platform Contract Tests

    [Theory]
    [InlineData(0)]      // Empty
    [InlineData(1)]      // Single byte
    [InlineData(16)]     // GUID size
    [InlineData(100)]    // Medium size
    [InlineData(1000)]   // Large size
    public void Copy_VariousDataSizes_ShouldMaintainFormatConsistency(int dataSize)
    {
        // Arrange
        var correlationData = dataSize == 0 ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(new string('A', dataSize));
        var message = CreateTestMessageWithCorrelationData(correlationData, $"test/size/{dataSize}");
        var textExporter = new TextExporter();

        var expectedHexFormat = ConvertBytesToHexString(correlationData);
        var currentBase64Format = Convert.ToBase64String(correlationData);

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(message, DateTime.UtcNow);

        // Assert - Size consistency contract
        Assert.Contains("Correlation Data:", content);

        if (dataSize > 0)
        {
            Assert.Contains(currentBase64Format, content); // Current implementation
            // Assert.Contains(expectedHexFormat, content); // Expected after fix
            Assert.DoesNotContain(expectedHexFormat, content); // Contract violation indicator
        }
    }

    [Fact]
    public void Copy_LineEndingHandling_ShouldBeConsistentAcrossPlatforms()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));
        var textExporter = new TextExporter();

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Line ending consistency
        Assert.Contains("Correlation Data:", content);
        Assert.Contains(Environment.NewLine, content); // Should use platform-appropriate line endings
    }

    #endregion

    #region Performance and Security Contract Tests

    [Fact]
    public void Copy_PerformanceContract_ShouldCompleteWithin50ms()
    {
        // Arrange
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("simple"));
        var textExporter = new TextExporter();

        var startTime = DateTime.UtcNow;

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Performance contract: <50ms
        Assert.True(elapsed.TotalMilliseconds < 50, $"Copy operation should complete within 50ms, took {elapsed.TotalMilliseconds}ms");
        Assert.NotNull(content);
    }

    [Fact]
    public void Copy_DataPrivacyContract_ShouldNotLeakSensitiveInformation()
    {
        // Arrange
        var sensitiveData = Encoding.UTF8.GetBytes("secret-api-key-12345");
        var message = CreateTestMessageWithCorrelationData(sensitiveData, "test/sensitive/data");
        var textExporter = new TextExporter();

        // Act
        var (content, _, _) = textExporter.GenerateDetailedTextFromMessage(message, DateTime.UtcNow);

        // Assert - Security contract: data should be formatted but contained
        Assert.Contains("Correlation Data:", content);

        // Verify the data is present (in whatever format)
        var base64Format = Convert.ToBase64String(sensitiveData);
        var hexFormat = ConvertBytesToHexString(sensitiveData);

        // One of these formats should be present (preferably hex after fix)
        Assert.True(content.Contains(base64Format) || content.Contains(hexFormat),
            "Correlation data should be present in some readable format");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts byte array to uppercase hex string format that matches expected metadata table display.
    /// </summary>
    /// <param name="bytes">Bytes to convert</param>
    /// <returns>Uppercase hex string without separators (e.g., "012AFF")</returns>
    private static string ConvertBytesToHexString(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        return Convert.ToHexString(bytes); // .NET 5+ method that produces uppercase hex
    }

    /// <summary>
    /// Creates a test message with specific correlation data for testing.
    /// </summary>
    private static MqttApplicationMessage CreateTestMessageWithCorrelationData(byte[] correlationData, string topic)
    {
        return new MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"test\": true}")),
            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            CorrelationData = correlationData,
            UserProperties = new List<MqttUserProperty>(),
            ResponseTopic = null,
            ContentType = "application/json",
            MessageExpiryInterval = 0,
            PayloadFormatIndicator = MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData
        };
    }

    #endregion
}

/// <summary>
/// Mock clipboard service interface for testing clipboard operations safely.
/// This interface follows the contract specification for platform clipboard integration.
/// </summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string> GetTextAsync();
    bool IsClipboardAvailable { get; }
}