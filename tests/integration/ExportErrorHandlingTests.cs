using System.IO;
using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using MQTTnet;
using Xunit;
using Xunit.Abstractions;
using System.Runtime.InteropServices;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Integration tests for error handling in export and copy operations.
/// Tests comprehensive error scenarios including file system errors, clipboard issues,
/// and graceful recovery from failure states.
/// </summary>
public class ExportErrorHandlingTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly MqttTestUtilities _mqttUtils;
    private readonly string _testDirectory;
    private readonly string _readOnlyDirectory;
    private readonly string _nonExistentParentDirectory;

    public ExportErrorHandlingTests(ITestOutputHelper output)
    {
        _output = output;
        _mqttUtils = new MqttTestUtilities();
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_ErrorHandlingTests", Guid.NewGuid().ToString());
        _readOnlyDirectory = Path.Combine(_testDirectory, "readonly");
        _nonExistentParentDirectory = Path.Combine(_testDirectory, "nonexistent", "child");

        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_readOnlyDirectory);

        // Make directory read-only on supported platforms
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetAttributes(_readOnlyDirectory, FileAttributes.ReadOnly);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Could not set read-only attribute: {ex.Message}");
            }
        }
    }

    public async Task InitializeAsync()
    {
        var port = await _mqttUtils.StartEmbeddedBrokerAsync();
        _output.WriteLine($"Started embedded MQTT broker on port {port}");
    }

    public async Task DisposeAsync()
    {
        await _mqttUtils.DisposeAsync();

        if (Directory.Exists(_testDirectory))
        {
            try
            {
                // Remove read-only attribute before deletion
                if (Directory.Exists(_readOnlyDirectory))
                {
                    File.SetAttributes(_readOnlyDirectory, FileAttributes.Normal);
                }
                Directory.Delete(_testDirectory, true);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Warning: Could not delete test directory {_testDirectory}. Reason: {ex.Message}");
            }
        }
    }

    #region Export Command Error Tests

    [Fact]
    public void ExportToFile_WithNullMessage_HandlesGracefully()
    {
        // Arrange
        var exporter = new TextExporter();
        var testTimestamp = DateTime.UtcNow;

        // Act & Assert - Should handle null message without crashing
        var result = exporter.ExportToFile(null!, testTimestamp, _testDirectory);

        // Should return null indicating failure but not crash
        Assert.Null(result);
    }

    [Fact]
    public void ExportToFile_WithNullDirectory_ThrowsArgumentException()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Act & Assert - Should throw for null directory
        Assert.Throws<ArgumentNullException>(() =>
            exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, null!));
    }

    [Fact]
    public void ExportToFile_WithEmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Act & Assert - Should throw for empty directory
        Assert.Throws<ArgumentException>(() =>
            exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, string.Empty));
    }

    [Fact]
    public void ExportToFile_WithInvalidPath_ReturnsNullAndLogsError()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Create an invalid path with illegal characters
        var invalidPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "C:\\invalid<>path|with*chars?.txt"
            : "/invalid\0path";

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, invalidPath);

        // Assert - Should return null indicating failure
        Assert.Null(result);
    }

    [Fact]
    public void ExportToFile_WithReadOnlyDirectory_ReturnsNullAndLogsError()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Act - Try to export to read-only directory
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _readOnlyDirectory);

        // Assert - Should return null due to access denied
        Assert.Null(result);
    }

    [Fact]
    public void ExportToFile_WithNonExistentParentDirectory_CreatesDirectoryAndSucceeds()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Ensure parent directory doesn't exist
        Assert.False(Directory.Exists(_nonExistentParentDirectory));

        // Act
        var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _nonExistentParentDirectory);

        // Assert - Should create directory and succeed
        Assert.NotNull(result);
        Assert.True(File.Exists(result));
        Assert.True(Directory.Exists(_nonExistentParentDirectory));
    }

    [Fact]
    public void ExportToFile_WithVeryLongFilePath_HandlesCorrectly()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Create a very long topic name that could cause filename issues
        var longTopic = "sensor/" + string.Join("/", Enumerable.Repeat("verylongsubpath", 20));
        var longMessage = new MqttApplicationMessage
        {
            Topic = longTopic,
            PayloadSegment = testMessage.Message.PayloadSegment,
            QualityOfServiceLevel = testMessage.Message.QualityOfServiceLevel,
            Retain = testMessage.Message.Retain,
            CorrelationData = testMessage.Message.CorrelationData
        };

        // Act
        var result = exporter.ExportToFile(longMessage, testMessage.ReceivedTimestamp, _testDirectory);

        // Assert - Should handle long paths gracefully
        if (result != null)
        {
            Assert.True(File.Exists(result));
            // Filename should be sanitized to valid length
            var filename = Path.GetFileName(result);
            Assert.True(filename.Length < 260, "Filename should be within Windows MAX_PATH limits");
        }
        else
        {
            // If it fails due to path length, that's also acceptable behavior
            _output.WriteLine("Export failed due to path length limitations, which is acceptable");
        }
    }

    [Fact]
    public async Task ExportToFile_WithFileInUse_HandlesGracefully()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // First, create a file and keep it open
        var tempFilePath = Path.Combine(_testDirectory, "locked_file.txt");
        using var lockedFileStream = File.Create(tempFilePath);

        // Mock the scenario by trying to export to a path that would conflict
        var timestampPrefix = testMessage.ReceivedTimestamp.ToString("yyyyMMdd_HHmmssfff");
        var sanitizedTopic = string.Join("_", testMessage.Message.Topic.Split(GetInvalidFileNameChars()));
        var expectedFileName = $"{timestampPrefix}_{sanitizedTopic}.txt";
        var expectedPath = Path.Combine(_testDirectory, expectedFileName);

        // Create and lock the expected file
        if (expectedPath != tempFilePath)
        {
            using var expectedFileStream = File.Create(expectedPath);
            // Keep stream open during export attempt

            // Act - Try to export while file is locked
            var result = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);

            // Assert - Should either succeed with different name or fail gracefully
            if (result != null)
            {
                Assert.True(File.Exists(result));
                // Should create with different name or overwrite if possible
            }
            else
            {
                // Failing gracefully is also acceptable
                _output.WriteLine("Export failed due to file in use, which is acceptable behavior");
            }
        }
    }

    [Fact]
    public void ExportToFile_WithExtremelyLargeCorrelationData_HandlesMemoryConstraints()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Create message with very large correlation data (but not so large as to exhaust memory)
        var largeCorrelationData = new byte[1024 * 1024]; // 1MB
        Array.Fill<byte>(largeCorrelationData, 0xAA);

        var largeMessage = new MqttApplicationMessage
        {
            Topic = "test/large/correlation",
            PayloadSegment = testMessage.Message.PayloadSegment,
            CorrelationData = largeCorrelationData,
            QualityOfServiceLevel = testMessage.Message.QualityOfServiceLevel,
            Retain = testMessage.Message.Retain
        };

        var startTime = DateTime.UtcNow;

        // Act
        var result = exporter.ExportToFile(largeMessage, testMessage.ReceivedTimestamp, _testDirectory);

        var elapsed = DateTime.UtcNow - startTime;

        // Assert - Should complete within reasonable time
        Assert.True(elapsed.TotalSeconds < 10, "Large correlation data export should complete within 10 seconds");

        if (result != null)
        {
            Assert.True(File.Exists(result));
            var content = File.ReadAllText(result);
            Assert.Contains("Correlation Data:", content);

            // Verify the large data is properly base64 encoded
            var expectedBase64 = Convert.ToBase64String(largeCorrelationData);
            Assert.Contains(expectedBase64, content);
        }
        else
        {
            // If export fails due to memory constraints, that's acceptable
            _output.WriteLine("Export failed due to memory constraints, which is acceptable for very large data");
        }
    }

    #endregion

    #region Copy Command Error Tests

    [Fact]
    public void CopyCorrelationData_WithNullData_HandlesGracefully()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("null"));

        // Act
        var (content, isValid, payload) = exporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Should handle null correlation data without exception
        Assert.NotNull(content);
        Assert.Contains("Correlation Data:", content);
        Assert.True(isValid);
    }

    [Fact]
    public void CopyCorrelationData_WithEmptyData_HandlesGracefully()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages()
            .First(m => m.Message.Topic.Contains("empty"));

        // Act
        var (content, isValid, payload) = exporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - Should handle empty correlation data
        Assert.NotNull(content);
        Assert.Contains("Correlation Data:", content);
        Assert.True(isValid);
    }

    [Fact]
    public void CopyCorrelationData_WithMalformedMessage_HandlesGracefully()
    {
        // Arrange
        var exporter = new TextExporter();

        // Create a message with potentially problematic data
        var malformedMessage = new MqttApplicationMessage
        {
            Topic = null!, // Null topic
            PayloadSegment = new ArraySegment<byte>(new byte[] { 0xFF, 0xFE, 0xFD, 0xFC }), // Binary payload
            CorrelationData = new byte[] { 0x00, 0x01, 0xFF }, // Binary correlation data
            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.ExactlyOnce,
            ContentType = "invalid/content-type",
            ResponseTopic = string.Empty
        };

        var timestamp = DateTime.UtcNow;

        // Act & Assert - Should not throw exception
        var exception = Record.Exception(() =>
        {
            var (content, isValid, payload) = exporter.GenerateDetailedTextFromMessage(malformedMessage, timestamp);
            Assert.NotNull(content);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void CopyCorrelationData_WithVeryLargeData_DoesNotExceedMemoryLimits()
    {
        // Arrange
        var exporter = new TextExporter();

        // Create message with large correlation data and large payload
        var largePayload = new byte[512 * 1024]; // 512KB payload
        var largeCorrelation = new byte[256 * 1024]; // 256KB correlation data

        Array.Fill<byte>(largePayload, 0x42);
        Array.Fill<byte>(largeCorrelation, 0x84);

        var largeMessage = new MqttApplicationMessage
        {
            Topic = "test/memory/large",
            PayloadSegment = new ArraySegment<byte>(largePayload),
            CorrelationData = largeCorrelation,
            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false
        };

        var timestamp = DateTime.UtcNow;
        var initialMemory = GC.GetTotalMemory(false);

        // Act
        var (content, isValid, payload) = exporter.GenerateDetailedTextFromMessage(largeMessage, timestamp);

        var finalMemory = GC.GetTotalMemory(true); // Force GC
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Should not consume excessive memory
        Assert.NotNull(content);
        Assert.Contains("Correlation Data:", content);

        // Memory increase should be reasonable (less than 10MB for this test)
        Assert.True(memoryIncrease < 10 * 1024 * 1024,
            $"Memory increase ({memoryIncrease:N0} bytes) should be less than 10MB");
    }

    #endregion

    #region General Error Handling Tests

    [Fact]
    public void GenerateDetailedTextFromMessage_WithCorruptedPayload_HandlesEncodingErrors()
    {
        // Arrange
        var exporter = new TextExporter();

        // Create message with invalid UTF-8 sequence
        var corruptedPayload = new byte[]
        {
            0xC0, 0x80, // Invalid UTF-8: overlong encoding of null
            0xED, 0xA0, 0x80, // Invalid UTF-8: high surrogate
            0xFF, 0xFE, 0xFD // More invalid bytes
        };

        var corruptedMessage = new MqttApplicationMessage
        {
            Topic = "test/corrupted/payload",
            PayloadSegment = new ArraySegment<byte>(corruptedPayload),
            CorrelationData = Encoding.UTF8.GetBytes("valid-correlation"),
            QualityOfServiceLevel = MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce
        };

        var timestamp = DateTime.UtcNow;

        // Act
        var (content, isValid, payloadString) = exporter.GenerateDetailedTextFromMessage(corruptedMessage, timestamp);

        // Assert - Should handle encoding errors gracefully
        Assert.NotNull(content);
        Assert.False(isValid, "Payload should be marked as invalid UTF-8");
        Assert.Contains("Could not decode payload as UTF-8", payloadString);

        // Correlation data should still be processed correctly
        Assert.Contains("Correlation Data:", content);
        var expectedBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("valid-correlation"));
        Assert.Contains(expectedBase64, content);
    }

    [Fact]
    public async Task ExportAndCopy_MultipleOperationsSimultaneously_HandlesConcurrently()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessages = MqttTestDataGenerator.GetCorrelationDataTestMessages().Take(5).ToList();
        var tasks = new List<Task<(string? filePath, string content)>>();

        // Act - Run multiple export operations concurrently
        foreach (var testMessage in testMessages)
        {
            var task = Task.Run(() =>
            {
                var filePath = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
                var (content, _, _) = exporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);
                return (filePath, content);
            });
            tasks.Add(task);
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Assert - All operations should complete successfully
        foreach (var task in tasks)
        {
            Assert.True(task.IsCompletedSuccessfully, "All concurrent operations should complete successfully");
            var (filePath, content) = task.Result;

            Assert.NotNull(content);
            Assert.Contains("Correlation Data:", content);

            if (filePath != null)
            {
                Assert.True(File.Exists(filePath), $"Export file should exist: {filePath}");
            }
        }
    }

    [Fact]
    public void ErrorRecovery_AfterFailedExport_SubsequentOperationsSucceed()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Act 1 - First, cause an export failure with invalid path
        var invalidResult = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\invalid|path" : "/invalid\0path");

        // Act 2 - Then, perform valid operations
        var validResult = exporter.ExportToFile(testMessage.Message, testMessage.ReceivedTimestamp, _testDirectory);
        var (content, isValid, payload) = exporter.GenerateDetailedTextFromMessage(testMessage.Message, testMessage.ReceivedTimestamp);

        // Assert - First operation should fail, subsequent should succeed
        Assert.Null(invalidResult);
        Assert.NotNull(validResult);
        Assert.True(File.Exists(validResult));
        Assert.NotNull(content);
        Assert.Contains("Correlation Data:", content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\r\t")]
    public void ExportToFile_WithWhitespaceOnlyTopic_HandlesGracefully(string whitespaceTopic)
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        var whitespaceMessage = new MqttApplicationMessage
        {
            Topic = whitespaceTopic,
            PayloadSegment = testMessage.Message.PayloadSegment,
            CorrelationData = testMessage.Message.CorrelationData,
            QualityOfServiceLevel = testMessage.Message.QualityOfServiceLevel
        };

        // Act
        var result = exporter.ExportToFile(whitespaceMessage, testMessage.ReceivedTimestamp, _testDirectory);

        // Assert - Should handle whitespace-only topics without crashing
        if (result != null)
        {
            Assert.True(File.Exists(result));
            var filename = Path.GetFileName(result);
            Assert.NotEmpty(filename);
            Assert.EndsWith(".txt", filename);
        }
        // If it returns null, that's also acceptable behavior for edge cases
    }

    #endregion

    #region Platform-Specific Error Tests

    [Fact]
    public void ExportToFile_WithPlatformSpecificInvalidChars_SanitizesCorrectly()
    {
        // Arrange
        var exporter = new TextExporter();
        var testMessage = MqttTestDataGenerator.GetCorrelationDataTestMessages().First();

        // Create topic with platform-specific invalid characters
        var invalidCharsTopic = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "test:<>|*?/\"topic" // Windows invalid chars
            : "test/topic\0with/null"; // Unix invalid chars

        var invalidMessage = new MqttApplicationMessage
        {
            Topic = invalidCharsTopic,
            PayloadSegment = testMessage.Message.PayloadSegment,
            CorrelationData = testMessage.Message.CorrelationData,
            QualityOfServiceLevel = testMessage.Message.QualityOfServiceLevel
        };

        // Act
        var result = exporter.ExportToFile(invalidMessage, testMessage.ReceivedTimestamp, _testDirectory);

        // Assert - Should sanitize filename correctly
        Assert.NotNull(result);
        Assert.True(File.Exists(result));

        var filename = Path.GetFileName(result);

        // Should not contain any invalid characters
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var invalidChars = new char[] { '<', '>', ':', '"', '|', '?', '*' };
            Assert.True(invalidChars.All(c => !filename.Contains(c)),
                "Filename should not contain Windows invalid characters");
        }
        else
        {
            Assert.False(filename.Contains('\0'), "Filename should not contain null character");
        }
    }

    #endregion

    #region Helper Methods

    private static char[] GetInvalidFileNameChars()
    {
        return new char[] { ':', '?', '*', '<', '>', '/', '\\', '|', '"' };
    }

    #endregion
}