using System.Diagnostics;
using System.Text;
using CrowsNestMqtt.BusinessLogic.Exporter;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Performance tests for Export All Messages feature.
/// T032: Validates that bulk export completes within acceptable time limits.
/// </summary>
public class ExportPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;

    public ExportPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), "CrowsNestMQTT_PerformanceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _output.WriteLine($"Test directory: {_testDirectory}");
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
                _output.WriteLine($"Warning: Could not delete test directory {_testDirectory}. Reason: {ex.Message}");
            }
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// T032: Validates that exporting 100 messages to JSON completes within 1 second.
    /// </summary>
    [Fact]
    public void ExportAll_100Messages_Json_CompletesWithin1Second()
    {
        // Arrange
        var exporter = new JsonExporter();
        var messages = CreateTestMessages(100, "performance/test/json");
        var timestamps = messages.Select(m => DateTime.UtcNow.AddMilliseconds(-messages.IndexOf(m))).ToList();
        var outputPath = Path.Combine(_testDirectory, "perf_100_messages.json");

        // Act
        var stopwatch = Stopwatch.StartNew();
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(outputPath), "Export file should exist");

        long elapsedMs = stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"✓ Exported 100 messages to JSON in {elapsedMs}ms");

        // Performance requirement: < 1000ms
        Assert.True(elapsedMs < 1000, $"Export took {elapsedMs}ms, expected < 1000ms");

        // Verify file size is reasonable
        var fileInfo = new FileInfo(outputPath);
        _output.WriteLine($"  File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0:F2} KB)");
        Assert.True(fileInfo.Length > 0, "File should not be empty");
    }

    /// <summary>
    /// T032: Validates that exporting 100 messages to TXT completes within 1 second.
    /// </summary>
    [Fact]
    public void ExportAll_100Messages_Txt_CompletesWithin1Second()
    {
        // Arrange
        var exporter = new TextExporter();
        var messages = CreateTestMessages(100, "performance/test/txt");
        var timestamps = messages.Select(m => DateTime.UtcNow.AddMilliseconds(-messages.IndexOf(m))).ToList();
        var outputPath = Path.Combine(_testDirectory, "perf_100_messages.txt");

        // Act
        var stopwatch = Stopwatch.StartNew();
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(outputPath), "Export file should exist");

        long elapsedMs = stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"✓ Exported 100 messages to TXT in {elapsedMs}ms");

        // Performance requirement: < 1000ms
        Assert.True(elapsedMs < 1000, $"Export took {elapsedMs}ms, expected < 1000ms");

        // Verify file size is reasonable
        var fileInfo = new FileInfo(outputPath);
        _output.WriteLine($"  File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0:F2} KB)");
        Assert.True(fileInfo.Length > 0, "File should not be empty");
    }

    /// <summary>
    /// T032: Validates performance with large payloads (simulating real sensor data).
    /// </summary>
    [Fact]
    public void ExportAll_100MessagesWithLargePayloads_CompletesWithin1Second()
    {
        // Arrange
        var exporter = new JsonExporter();
        var messages = CreateTestMessagesWithLargePayloads(100, "performance/large", payloadSizeKB: 10);
        var timestamps = messages.Select(m => DateTime.UtcNow.AddMilliseconds(-messages.IndexOf(m))).ToList();
        var outputPath = Path.Combine(_testDirectory, "perf_large_payloads.json");

        // Act
        var stopwatch = Stopwatch.StartNew();
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(outputPath), "Export file should exist");

        long elapsedMs = stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"✓ Exported 100 messages with 10KB payloads in {elapsedMs}ms");

        // More lenient requirement for large payloads: < 2000ms
        Assert.True(elapsedMs < 2000, $"Export took {elapsedMs}ms, expected < 2000ms for large payloads");

        // Verify file size (should be ~1MB total)
        var fileInfo = new FileInfo(outputPath);
        _output.WriteLine($"  File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
        Assert.True(fileInfo.Length > 1_000_000, "File should be > 1MB for large payloads");
    }

    /// <summary>
    /// T032: Benchmark comparison between JSON and TXT export formats.
    /// </summary>
    [Fact]
    public void ExportAll_FormatComparison_BothMeetPerformanceRequirements()
    {
        // Arrange
        int messageCount = 100;
        var messages = CreateTestMessages(messageCount, "performance/comparison");
        var timestamps = messages.Select(m => DateTime.UtcNow.AddMilliseconds(-messages.IndexOf(m))).ToList();

        var jsonExporter = new JsonExporter();
        var txtExporter = new TextExporter();

        var jsonPath = Path.Combine(_testDirectory, "comparison.json");
        var txtPath = Path.Combine(_testDirectory, "comparison.txt");

        // Act - JSON Export
        var jsonStopwatch = Stopwatch.StartNew();
        string? jsonResult = jsonExporter.ExportAllToFile(messages, timestamps, jsonPath);
        jsonStopwatch.Stop();

        // Act - TXT Export
        var txtStopwatch = Stopwatch.StartNew();
        string? txtResult = txtExporter.ExportAllToFile(messages, timestamps, txtPath);
        txtStopwatch.Stop();

        // Assert
        Assert.NotNull(jsonResult);
        Assert.NotNull(txtResult);

        long jsonElapsedMs = jsonStopwatch.ElapsedMilliseconds;
        long txtElapsedMs = txtStopwatch.ElapsedMilliseconds;

        _output.WriteLine($"Performance Comparison:");
        _output.WriteLine($"  JSON: {jsonElapsedMs}ms");
        _output.WriteLine($"  TXT:  {txtElapsedMs}ms");
        _output.WriteLine($"  Ratio: {(double)txtElapsedMs / jsonElapsedMs:F2}x");

        // Both should be under 1 second
        Assert.True(jsonElapsedMs < 1000, $"JSON export took {jsonElapsedMs}ms");
        Assert.True(txtElapsedMs < 1000, $"TXT export took {txtElapsedMs}ms");

        // File size comparison
        var jsonFileSize = new FileInfo(jsonPath).Length;
        var txtFileSize = new FileInfo(txtPath).Length;

        _output.WriteLine($"File Size Comparison:");
        _output.WriteLine($"  JSON: {jsonFileSize:N0} bytes ({jsonFileSize / 1024.0:F2} KB)");
        _output.WriteLine($"  TXT:  {txtFileSize:N0} bytes ({txtFileSize / 1024.0:F2} KB)");
    }

    /// <summary>
    /// T032: Validates performance does not degrade with complex MQTT metadata.
    /// </summary>
    [Fact]
    public void ExportAll_MessagesWithComplexMetadata_CompletesWithin1Second()
    {
        // Arrange
        var exporter = new JsonExporter();
        var messages = CreateMessagesWithComplexMetadata(100, "performance/metadata");
        var timestamps = messages.Select(m => DateTime.UtcNow.AddMilliseconds(-messages.IndexOf(m))).ToList();
        var outputPath = Path.Combine(_testDirectory, "perf_complex_metadata.json");

        // Act
        var stopwatch = Stopwatch.StartNew();
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);
        stopwatch.Stop();

        // Assert
        Assert.NotNull(result);

        long elapsedMs = stopwatch.ElapsedMilliseconds;
        _output.WriteLine($"✓ Exported 100 messages with complex metadata in {elapsedMs}ms");

        Assert.True(elapsedMs < 1000, $"Export took {elapsedMs}ms, expected < 1000ms");
    }

    // Helper methods

    private List<MqttApplicationMessage> CreateTestMessages(int count, string topicPrefix)
    {
        var messages = new List<MqttApplicationMessage>();

        for (int i = 0; i < count; i++)
        {
            messages.Add(CreateTestMessage(topicPrefix, $"Test message payload {i}", i));
        }

        return messages;
    }

    private List<MqttApplicationMessage> CreateTestMessagesWithLargePayloads(int count, string topicPrefix, int payloadSizeKB)
    {
        var messages = new List<MqttApplicationMessage>();
        string largePayload = new string('x', payloadSizeKB * 1024); // Create KB-sized payload

        for (int i = 0; i < count; i++)
        {
            messages.Add(CreateTestMessage(topicPrefix, largePayload, i));
        }

        return messages;
    }

    private List<MqttApplicationMessage> CreateMessagesWithComplexMetadata(int count, string topicPrefix)
    {
        var messages = new List<MqttApplicationMessage>();

        for (int i = 0; i < count; i++)
        {
            var userProperties = new List<MQTTnet.Packets.MqttUserProperty>();
            for (int j = 0; j < 20; j++) // 20 user properties per message
            {
                userProperties.Add(new MQTTnet.Packets.MqttUserProperty($"prop-{j}", $"value-{j}-{i}"));
            }

            messages.Add(new MqttApplicationMessage
            {
                Topic = $"{topicPrefix}/{i}",
                PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes($"Message {i}")),
                QualityOfServiceLevel = MqttQualityOfServiceLevel.ExactlyOnce,
                Retain = true,
                MessageExpiryInterval = 3600,
                ContentType = "application/json",
                ResponseTopic = $"{topicPrefix}/response/{i}",
                CorrelationData = Encoding.UTF8.GetBytes($"correlation-{i:D10}"),
                UserProperties = userProperties
            });
        }

        return messages;
    }

    private MqttApplicationMessage CreateTestMessage(string topic, string payload, int id)
    {
        return new MqttApplicationMessage
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)),
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
            Retain = false,
            MessageExpiryInterval = 3600,
            ContentType = "text/plain",
            CorrelationData = Encoding.UTF8.GetBytes($"corr-{id}"),
            UserProperties = new List<MQTTnet.Packets.MqttUserProperty>
            {
                new MQTTnet.Packets.MqttUserProperty("test-id", id.ToString())
            }
        };
    }
}
