# Contract: Export All Service

**Feature**: Export All Messages from Topic History
**Contract Type**: Service Interface Extension
**Version**: 1.0.0
**Date**: 2026-01-21

## Overview

This contract defines the extension to the `IMessageExporter` interface for bulk export functionality. The new `ExportAllToFile` method enables exporting multiple messages to a single aggregated file while maintaining consistency with existing single-message export behavior.

## Interface Extension

### IMessageExporter.ExportAllToFile

```csharp
public interface IMessageExporter
{
    // Existing members (unchanged):
    ExportTypes ExporterType { get; }
    (string content, bool isPayloadValidUtf8, string payloadAsString)
        GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime);
    string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath);

    // New member:
    /// <summary>
    /// Exports multiple MQTT messages to a single file in aggregated format.
    /// </summary>
    /// <param name="messages">Collection of MQTT messages to export. Must not be null.</param>
    /// <param name="timestamps">Corresponding receive timestamps. Count must match messages.</param>
    /// <param name="outputFilePath">Full path to output file. Will overwrite if exists.</param>
    /// <returns>
    ///   The output file path if export succeeds.
    ///   null if export fails (I/O error, validation error, or empty collection).
    /// </returns>
    /// <exception cref="ArgumentNullException">If messages or timestamps is null.</exception>
    /// <exception cref="ArgumentException">If messages/timestamps count mismatch.</exception>
    string? ExportAllToFile(
        IEnumerable<MqttApplicationMessage> messages,
        IEnumerable<DateTime> timestamps,
        string outputFilePath);
}
```

---

## Behavior Specification

### Preconditions

1. **messages** parameter:
   - Must not be null
   - Can be empty (returns null, no file created)
   - Each message must have valid Topic property
   - Each message may have null Payload (handled gracefully)

2. **timestamps** parameter:
   - Must not be null
   - Count must match messages count exactly
   - Each timestamp must be valid DateTime (not DateTime.MinValue/MaxValue for sanity)

3. **outputFilePath** parameter:
   - Must not be null or empty
   - Must be valid file path (absolute or relative)
   - Parent directory must exist or be creatable
   - If file exists, it will be overwritten without warning

### Postconditions

**Success Case:**
- Returns `outputFilePath` (same as input)
- File created/overwritten at specified path
- File contains all messages in format-specific aggregated structure
- File encoding: UTF-8
- File permissions: Default OS permissions (readable by user)

**Failure Cases:**
- Returns `null`
- No file created (or existing file left unchanged if I/O fails mid-write)
- Error logged via ILogger (implementation-specific)

---

## Format-Specific Behavior

### JsonExporter Implementation

**Output Structure:**
```json
[
  {
    "Timestamp": "2026-01-21T14:30:45.123Z",
    "Topic": "sensors/temperature",
    "ResponseTopic": null,
    "QualityOfServiceLevel": "AtLeastOnce",
    "Retain": false,
    "MessageExpiryInterval": 3600,
    "CorrelationData": "012AFF",
    "PayloadFormatIndicator": "Utf8",
    "ContentType": "application/json",
    "UserProperties": [
      {"Name": "source", "Value": "sensor-01"}
    ],
    "Payload": "{\"temperature\":22.5}"
  },
  { /* message 2 */ },
  { /* message 3 */ }
]
```

**Implementation Requirements:**
- Use `System.Text.Json.JsonSerializer` with `WriteIndented = true`
- Each message serialized as `MqttMessageExportDto` (existing structure)
- Array wrapper around all DTOs: `JsonSerializer.Serialize(dtos)`
- Correlation data in hexadecimal format (existing: `BitConverter.ToString().Replace("-", "")`)
- Payload only included if valid UTF-8 (existing logic)
- UserProperties as array of name/value pairs (existing structure)

**Empty Collection Handling:**
```csharp
if (!messages.Any())
{
    _logger.LogWarning("ExportAllToFile called with empty message collection");
    return null;  // No file created
}
```

---

### TextExporter Implementation

**Output Structure:**
```
Timestamp: 2026-01-21 14:30:45.123
Topic: sensors/temperature
Response Topic:
QoS: AtLeastOnce
Message Expiry Interval: 3600
Correlation Data: 012AFF
Payload Format: Utf8
Content Type: application/json
Retain: false

--- User Properties ---
source: sensor-01

--- Payload ---
{"temperature":22.5}

================================================================================

Timestamp: 2026-01-21 14:30:46.456
Topic: sensors/humidity
...
```

**Implementation Requirements:**
- Reuse `GenerateDetailedTextFromMessage()` for each message
- Insert delimiter between messages: 80 equals signs (`new string('=', 80)`)
- Blank lines before and after delimiter for readability
- No delimiter after last message
- Payload pretty-printing for JSON content (existing: detect "application/json" ContentType)

**Delimiter Specification:**
```csharp
const string MessageDelimiter = "\n" + new string('=', 80) + "\n\n";
```

---

## Contract Tests

### Test 1: Export Single Message (JSON)
```csharp
[Fact]
public void ExportAllToFile_SingleMessage_Json_CreatesValidArray()
{
    // Arrange
    var exporter = new JsonExporter();
    var message = CreateTestMessage("test/topic", "payload content");
    var timestamp = DateTime.Now;
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        string? result = exporter.ExportAllToFile([message], [timestamp], outputPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(outputPath, result);
        Assert.True(File.Exists(outputPath));

        string content = File.ReadAllText(outputPath);
        var array = JsonSerializer.Deserialize<List<MqttMessageExportDto>>(content);
        Assert.NotNull(array);
        Assert.Single(array);
        Assert.Equal("test/topic", array[0].Topic);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

### Test 2: Export Multiple Messages (JSON)
```csharp
[Fact]
public void ExportAllToFile_MultipleMessages_Json_CreatesArrayWithCorrectCount()
{
    // Arrange
    var exporter = new JsonExporter();
    var messages = Enumerable.Range(1, 10)
        .Select(i => CreateTestMessage($"topic/{i}", $"payload {i}"))
        .ToList();
    var timestamps = Enumerable.Repeat(DateTime.Now, 10).ToList();
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);

        // Assert
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        var array = JsonSerializer.Deserialize<List<MqttMessageExportDto>>(content);
        Assert.Equal(10, array!.Count);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

### Test 3: Export with Empty Collection
```csharp
[Fact]
public void ExportAllToFile_EmptyCollection_ReturnsNull()
{
    // Arrange
    var exporter = new JsonExporter();
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        string? result = exporter.ExportAllToFile([], [], outputPath);

        // Assert
        Assert.Null(result);
        Assert.False(File.Exists(outputPath));  // No file created
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

### Test 4: Export with Mismatched Counts
```csharp
[Fact]
public void ExportAllToFile_MismatchedCounts_ThrowsArgumentException()
{
    // Arrange
    var exporter = new JsonExporter();
    var messages = new[] { CreateTestMessage("topic", "payload") };
    var timestamps = new[] { DateTime.Now, DateTime.Now };  // Wrong count

    // Act & Assert
    Assert.Throws<ArgumentException>(() =>
        exporter.ExportAllToFile(messages, timestamps, Path.GetTempFileName()));
}
```

### Test 5: Export Overwrites Existing File
```csharp
[Fact]
public void ExportAllToFile_FileExists_Overwrites()
{
    // Arrange
    var exporter = new JsonExporter();
    var message = CreateTestMessage("topic", "new content");
    var timestamp = DateTime.Now;
    var outputPath = Path.GetTempFileName();
    File.WriteAllText(outputPath, "old content");

    try
    {
        // Act
        string? result = exporter.ExportAllToFile([message], [timestamp], outputPath);

        // Assert
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        Assert.DoesNotContain("old content", content);
        Assert.Contains("new content", content);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

### Test 6: Export with Delimiter (TXT)
```csharp
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
    var timestamps = Enumerable.Repeat(DateTime.Now, 3).ToArray();
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        string? result = exporter.ExportAllToFile(messages, timestamps, outputPath);

        // Assert
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);

        // Check for delimiters (80 equals signs)
        int delimiterCount = Regex.Matches(content, new string('=', 80)).Count;
        Assert.Equal(2, delimiterCount);  // 3 messages = 2 delimiters

        // Check all topics present
        Assert.Contains("Topic: topic1", content);
        Assert.Contains("Topic: topic2", content);
        Assert.Contains("Topic: topic3", content);
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

### Test 7: Export with Null Payload
```csharp
[Fact]
public void ExportAllToFile_NullPayload_HandlesGracefully()
{
    // Arrange
    var exporter = new JsonExporter();
    var message = new MqttApplicationMessage
    {
        Topic = "test/topic",
        Payload = null  // Null payload
    };
    var timestamp = DateTime.Now;
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        string? result = exporter.ExportAllToFile([message], [timestamp], outputPath);

        // Assert
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        var array = JsonSerializer.Deserialize<List<MqttMessageExportDto>>(content);
        Assert.Null(array![0].Payload);  // Payload field is null in DTO
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

### Test 8: Export with Binary Payload (Non-UTF8)
```csharp
[Fact]
public void ExportAllToFile_BinaryPayload_Json_OmitsPayload()
{
    // Arrange
    var exporter = new JsonExporter();
    var message = new MqttApplicationMessage
    {
        Topic = "test/binary",
        Payload = new byte[] { 0xFF, 0xFE, 0x00, 0x01 }  // Invalid UTF-8
    };
    var timestamp = DateTime.Now;
    var outputPath = Path.GetTempFileName();

    try
    {
        // Act
        string? result = exporter.ExportAllToFile([message], [timestamp], outputPath);

        // Assert
        Assert.NotNull(result);
        string content = File.ReadAllText(outputPath);
        var array = JsonSerializer.Deserialize<List<MqttMessageExportDto>>(content);
        Assert.Null(array![0].Payload);  // Binary payload not included
    }
    finally
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);
    }
}
```

---

## Error Handling

### Validation Errors

**ArgumentNullException:**
```csharp
if (messages == null)
    throw new ArgumentNullException(nameof(messages));
if (timestamps == null)
    throw new ArgumentNullException(nameof(timestamps));
```

**ArgumentException:**
```csharp
var messageList = messages.ToList();
var timestampList = timestamps.ToList();

if (messageList.Count != timestampList.Count)
    throw new ArgumentException(
        $"Count mismatch: {messageList.Count} messages but {timestampList.Count} timestamps");
```

### I/O Errors

**IOException Handling:**
```csharp
try
{
    File.WriteAllText(outputFilePath, content);
    return outputFilePath;
}
catch (IOException ex)
{
    _logger.LogError(ex, "Failed to write export file to {Path}", outputFilePath);
    return null;
}
catch (UnauthorizedAccessException ex)
{
    _logger.LogError(ex, "Access denied writing to {Path}", outputFilePath);
    return null;
}
```

---

## Performance Requirements

**Memory Usage:**
- For 100 messages @ ~1KB each = ~100KB in-memory during serialization
- Peak: 2× (messages + serialized string) = ~200KB
- Acceptable for synchronous in-memory serialization

**Execution Time:**
- Serialization: <50ms for 100 messages
- File write: <50ms on SSD (100KB)
- Total: <100ms target (non-blocking via Task.Run)

**Thread Safety:**
- Method is not thread-safe (creates new exporter per call)
- File I/O runs on thread pool (via `Task.Run` in caller)

---

## Success Criteria

✅ **Valid JSON array** produced by JsonExporter
✅ **Delimited text** produced by TextExporter
✅ **Empty collection** handled gracefully (returns null)
✅ **Null/binary payloads** handled without crashes
✅ **File overwrite** works without confirmation
✅ **Count mismatch** throws ArgumentException
✅ **I/O errors** return null and log error

---

## Non-Functional Requirements

**Consistency:**
- Reuses existing DTO structures (`MqttMessageExportDto`)
- Reuses existing formatting logic (`GenerateDetailedTextFromMessage`)
- Maintains existing metadata fields (correlation data, user properties)

**Extensibility:**
- Interface extension allows other exporters to implement
- Future formats (CSV, XML, Parquet) can implement same contract

**Logging:**
- Logs warnings for empty collections
- Logs errors for I/O failures
- Logs info for successful exports (count, path)

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-01-21 | Initial contract definition |

---

**Related Contracts:**
- [export-all-command.md](./export-all-command.md) - Command parsing contract
- [ui-export-buttons.md](./ui-export-buttons.md) - UI button contracts
