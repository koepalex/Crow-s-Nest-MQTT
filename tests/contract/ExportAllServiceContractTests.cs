using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Tests.TestData;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for IMessageExporter.ExportAllToFile service extension.
///
/// CRITICAL CONTRACT REQUIREMENTS:
/// - ExportAllToFile(messages, timestamps, path) → Exports multiple messages to single file
/// - JSON format → Valid JSON array: [{msg1}, {msg2}, ...]
/// - TXT format → Delimited messages with 80-equals delimiter
/// - Empty collection → Returns null, no file created
/// - Count mismatch → Throws ArgumentException
///
/// These tests will FAIL initially because:
/// - IMessageExporter interface doesn't have ExportAllToFile method (T017)
/// - JsonExporter/TextExporter don't implement ExportAllToFile (T015, T016)
/// </summary>
public class ExportAllServiceContractTests : IDisposable
{
    private readonly string _testDirectory;

    public ExportAllServiceContractTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_ExportAllContractTests", Guid.NewGuid().ToString());
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

    /// <summary>
    /// T006: Contract test for ExportAllToFile with single message (JSON).
    /// Expected to FAIL until T015 (JsonExporter.ExportAllToFile) is implemented.
    /// </summary>
    [Fact]
    public void ExportAllToFile_SingleMessage_Json_CreatesValidArray()
    {
        // Arrange
        var exporter = new JsonExporter();
        var testMessage = CreateTestMessage("test/topic", "payload content");
        var timestamp = DateTime.UtcNow;
        var outputPath = Path.Combine(_testDirectory, "single_message.json");

        // Act
        // NOTE: This WILL FAIL because ExportAllToFile doesn't exist yet (T015, T017)
        string? result = exporter.ExportAllToFile(
            new[] { testMessage },
            new[] { timestamp },
            outputPath);

        // Assert - CONTRACT REQUIREMENT: JSON array format
        Assert.NotNull(result);
        Assert.Equal(outputPath, result);
        Assert.True(File.Exists(outputPath), "File should be created");

        string content = File.ReadAllText(outputPath);

        // Must be valid JSON array
        var array = JsonSerializer.Deserialize<JsonElement[]>(content);
        Assert.NotNull(array);
        Assert.Single(array);

        // Verify message structure
        var msg = array[0];
        Assert.Equal("test/topic", msg.GetProperty("Topic").GetString());
    }

    /// <summary>
    /// T007: Contract test for ExportAllToFile with multiple messages (JSON).
    /// Expected to FAIL until T015 implementation.
    /// </summary>
    [Fact]
    public void ExportAllToFile_MultipleMessages_Json_CreatesArrayWithCorrectCount()
    {
        // Arrange
        var exporter = new JsonExporter();
        var messages = Enumerable.Range(1, 10)
            .Select(i => CreateTestMessage($"topic/{i}", $"payload {i}"))
            .ToArray();
        var timestamps = Enumerable.Repeat(DateTime.UtcNow, 10).ToArray();
        var outputPath = Path.Combine(_testDirectory, "multiple_messages.json");

        // Act
        // NOTE: This WILL FAIL - ExportAllToFile not implemented (T015)
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);

        // Assert - CONTRACT: Array with correct count
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        var array = JsonSerializer.Deserialize<JsonElement[]>(content);

        Assert.Equal(10, array!.Length);

        // Verify each message is present
        for (int i = 0; i < 10; i++)
        {
            var topic = array[i].GetProperty("Topic").GetString();
            Assert.Contains($"topic/{i + 1}", topic);
        }
    }

    /// <summary>
    /// T008: Contract test for ExportAllToFile with empty collection.
    /// Expected to FAIL until T015 implementation.
    /// </summary>
    [Fact]
    public void ExportAllToFile_EmptyCollection_ReturnsNull()
    {
        // Arrange
        var exporter = new JsonExporter();
        var outputPath = Path.Combine(_testDirectory, "empty.json");

        // Act
        // NOTE: This WILL FAIL - ExportAllToFile not implemented (T015)
        string? result = exporter.ExportAllToFile(
            Array.Empty<MqttApplicationMessage>(),
            Array.Empty<DateTime>(),
            outputPath);

        // Assert - CONTRACT: Empty collection returns null, no file created
        Assert.Null(result);
        Assert.False(File.Exists(outputPath), "File should NOT be created for empty collection");
    }

    /// <summary>
    /// T009: Contract test for ExportAllToFile with delimiter (TXT format).
    /// Expected to FAIL until T016 (TextExporter.ExportAllToFile) is implemented.
    /// </summary>
    [Fact]
    public void ExportAllToFile_MultipleMessages_Txt_ContainsDelimiters()
    {
        // Arrange
        var exporter = new TextExporter();
        var messages = new[]
        {
            CreateTestMessage("topic1", "payload1"),
            CreateTestMessage("topic2", "payload2"),
            CreateTestMessage("topic3", "payload3")
        };
        var timestamps = Enumerable.Repeat(DateTime.UtcNow, 3).ToArray();
        var outputPath = Path.Combine(_testDirectory, "delimited.txt");

        // Act
        // NOTE: This WILL FAIL - ExportAllToFile not implemented (T016)
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);

        // Assert - CONTRACT: Delimited with 80 equals signs
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);

        // Check for delimiters: 3 messages = 2 delimiters
        var delimiterPattern = new string('=', 80);
        int delimiterCount = Regex.Matches(content, Regex.Escape(delimiterPattern)).Count;
        Assert.Equal(2, delimiterCount);

        // Verify all topics present
        Assert.Contains("Topic: topic1", content);
        Assert.Contains("Topic: topic2", content);
        Assert.Contains("Topic: topic3", content);

        // Verify payloads present
        Assert.Contains("payload1", content);
        Assert.Contains("payload2", content);
        Assert.Contains("payload3", content);
    }

    /// <summary>
    /// Additional contract test: Count mismatch throws ArgumentException.
    /// </summary>
    [Fact]
    public void ExportAllToFile_MismatchedCounts_ThrowsArgumentException()
    {
        // Arrange
        var exporter = new JsonExporter();
        var messages = new[] { CreateTestMessage("topic", "payload") };
        var timestamps = new[] { DateTime.UtcNow, DateTime.UtcNow }; // Wrong count!
        var outputPath = Path.Combine(_testDirectory, "mismatch.json");

        // Act & Assert
        // NOTE: This WILL FAIL - ExportAllToFile not implemented (T015)
        Assert.Throws<ArgumentException>(() =>
            exporter.ExportAllToFile(messages, timestamps, outputPath));
    }

    /// <summary>
    /// Contract test: File overwrite behavior.
    /// </summary>
    [Fact]
    public void ExportAllToFile_FileExists_Overwrites()
    {
        // Arrange
        var exporter = new JsonExporter();
        var message = CreateTestMessage("topic", "new content");
        var timestamp = DateTime.UtcNow;
        var outputPath = Path.Combine(_testDirectory, "overwrite.json");

        // Create existing file
        File.WriteAllText(outputPath, "old content");

        // Act
        // NOTE: This WILL FAIL - ExportAllToFile not implemented (T015)
        string? result = exporter.ExportAllToFile(
            new[] { message },
            new[] { timestamp },
            outputPath);

        // Assert - CONTRACT: Overwrites without warning
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        Assert.DoesNotContain("old content", content);
        Assert.Contains("new content", content);
    }

    /// <summary>
    /// Contract test: Null payload handling.
    /// </summary>
    [Fact]
    public void ExportAllToFile_NullPayload_HandlesGracefully()
    {
        // Arrange
        var exporter = new JsonExporter();
        var message = new MqttApplicationMessage
        {
            Topic = "test/null/payload",
            PayloadSegment = default // Null payload
        };
        var timestamp = DateTime.UtcNow;
        var outputPath = Path.Combine(_testDirectory, "null_payload.json");

        // Act
        // NOTE: This WILL FAIL - ExportAllToFile not implemented (T015)
        string? result = exporter.ExportAllToFile(
            new[] { message },
            new[] { timestamp },
            outputPath);

        // Assert - Should handle gracefully
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        var array = JsonSerializer.Deserialize<JsonElement[]>(content);

        // Payload field should be null or empty
        Assert.NotNull(array);
    }

    private MqttApplicationMessage CreateTestMessage(string topic, string payload)
    {
        return new MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)),
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false
        };
    }
}
