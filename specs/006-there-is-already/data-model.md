# Data Model: Export All Messages Feature

**Feature**: Export All Messages from Topic History
**Date**: 2026-01-21

## Overview

This document defines the data entities, value objects, and their relationships for the export all messages feature. The design extends existing export infrastructure while adding new value objects to encapsulate bulk export operations.

## Entity Diagram

```
┌─────────────────────────┐
│  MessageViewModel       │
│  (Existing)             │
├─────────────────────────┤
│ + MessageId: Guid       │
│ + Topic: string         │
│ + Timestamp: DateTime   │
│ + PayloadPreview: string│
│ + Size: int             │
│ + GetFullMessage()      │
└───────────┬─────────────┘
            │
            │ 0..*
            │
            ▼
┌─────────────────────────┐
│  ExportAllOperation     │
│  (New Value Object)     │
├─────────────────────────┤
│ + TopicName: string     │
│ + MessageCount: int     │
│ + ExportFormat: string  │
│ + OutputFilePath: string│
│ + Timestamp: DateTime   │
│ + IsLimitExceeded: bool │
└───────────┬─────────────┘
            │
            │ uses
            │
            ▼
┌─────────────────────────┐      ┌─────────────────────────┐
│  IMessageExporter       │◄─────│  ExportConfiguration    │
│  (Extended Interface)   │      │  (Existing)             │
├─────────────────────────┤      ├─────────────────────────┤
│ + ExporterType          │      │ + ExportFormat: enum    │
│ + ExportToFile(...)     │      │ + ExportPath: string    │
│ + ExportAllToFile(...)  │◄─────┤                         │
└─────────────────────────┘      └─────────────────────────┘
            △
            │
      ┌─────┴─────┐
      │           │
┌─────────────┐ ┌──────────────┐
│JsonExporter │ │TextExporter  │
│(Extended)   │ │(Extended)    │
└─────────────┘ └──────────────┘
```

## Entities and Value Objects

### 1. MessageViewModel (Existing - Read-Only)

**Purpose**: Represents a single MQTT message in the UI history view

**Fields:**
- `MessageId: Guid` - Unique identifier for the message
- `Topic: string` - MQTT topic name
- `Timestamp: DateTime` - When message was received
- `PayloadPreview: string` - Truncated payload for display (max 100 chars)
- `Size: int` - Message size in bytes
- `IsEffectivelyRetained: bool` - Retain flag status
- `DisplayText: string` - Formatted display string for UI

**Methods:**
- `GetFullMessage(): MqttApplicationMessage?` - Retrieves complete message with all metadata

**Validation Rules:**
- MessageId must not be empty
- Topic must not be null or empty
- Timestamp must be valid DateTime
- Size must be >= 0

**Lifecycle:**
- Created when MQTT message received
- Stored in FilteredMessageHistory (ObservableCollection)
- Immutable after creation

---

### 2. ExportAllOperation (New Value Object)

**Purpose**: Encapsulates state and parameters for a bulk export operation

**Fields:**
```csharp
public record ExportAllOperation
{
    public string TopicName { get; init; }          // Selected topic full path
    public int MessageCount { get; init; }          // Total messages in history
    public int ExportedCount { get; init; }         // Actually exported (≤100)
    public ExportTypes ExportFormat { get; init; }  // json or txt
    public string OutputFilePath { get; init; }     // Generated file path
    public DateTime Timestamp { get; init; }        // When export was initiated
    public bool IsLimitExceeded { get; init; }      // True if MessageCount > 100
}
```

**Validation Rules:**
- `TopicName`: Must not be null or empty
- `MessageCount`: Must be >= 0
- `ExportedCount`: Must be >= 0 and ≤ min(MessageCount, 100)
- `ExportFormat`: Must be ExportTypes.json or ExportTypes.txt
- `OutputFilePath`: Must be valid file path (writable directory)
- `Timestamp`: Must be valid DateTime
- `IsLimitExceeded`: True if MessageCount > 100, false otherwise

**Creation Example:**
```csharp
var operation = new ExportAllOperation
{
    TopicName = "sensors/temperature",
    MessageCount = 150,
    ExportedCount = 100,
    ExportFormat = ExportTypes.json,
    OutputFilePath = @"C:\exports\sensors_temperature_20260121_143045123.json",
    Timestamp = DateTime.Now,
    IsLimitExceeded = true  // 150 > 100
};
```

**Lifecycle:**
- Created at start of export all operation
- Passed to export service for execution
- Logged for audit trail
- Discarded after export completes

---

### 3. ExportConfiguration (Existing)

**Purpose**: User settings for export operations

**Fields:**
- `ExportFormat: ExportTypes?` - Default export format (json or txt)
- `ExportPath: string?` - Default export directory path

**Validation Rules:**
- `ExportFormat`: If set, must be valid ExportTypes enum value
- `ExportPath`: If set, must be valid directory path

**Access Pattern:**
```csharp
// From MainViewModel:
string format = Settings.ExportFormat?.ToString() ?? "json";
string path = Settings.ExportPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
```

**Lifecycle:**
- Loaded from configuration file at app startup
- Modified through Settings UI
- Persisted to disk on change
- Accessed by export commands

---

### 4. IMessageExporter (Extended Interface)

**Purpose**: Contract for exporting MQTT messages to files

**Existing Methods:**
```csharp
ExportTypes ExporterType { get; }

(string content, bool isPayloadValidUtf8, string payloadAsString)
    GenerateDetailedTextFromMessage(MqttApplicationMessage msg, DateTime receivedTime);

string? ExportToFile(MqttApplicationMessage msg, DateTime receivedTime, string exportFolderPath);
```

**New Method (Extension):**
```csharp
/// <summary>
/// Exports multiple MQTT messages to a single file in aggregated format.
/// For JSON: outputs array of message objects [{ }, { }, ...]
/// For TXT: outputs delimited messages with separator lines
/// </summary>
/// <param name="messages">Collection of MQTT messages to export</param>
/// <param name="timestamps">Corresponding receive timestamps (same count as messages)</param>
/// <param name="outputFilePath">Full path to output file (will overwrite if exists)</param>
/// <returns>The output file path if successful, null if export failed</returns>
string? ExportAllToFile(
    IEnumerable<MqttApplicationMessage> messages,
    IEnumerable<DateTime> timestamps,
    string outputFilePath);
```

**Validation Rules (ExportAllToFile):**
- `messages`: Must not be null, count must match `timestamps` count
- `timestamps`: Must not be null, count must match `messages` count
- `outputFilePath`: Must be valid file path, directory must exist or be creatable
- Returns null if any validation fails or I/O error occurs

**Throws:**
- `ArgumentNullException`: If messages or timestamps is null
- `ArgumentException`: If counts don't match
- `IOException`: If file write fails

**Implementations:**
- `JsonExporter`: Serializes messages to JSON array
- `TextExporter`: Concatenates messages with `=` delimiter lines

---

### 5. ExportResult (New Value Object - Optional)

**Purpose**: Encapsulates result of export operation for consistent error handling

**Fields:**
```csharp
public record ExportResult
{
    public bool IsSuccess { get; init; }
    public string? FilePath { get; init; }          // Null if failed
    public int ExportedCount { get; init; }         // Actual messages written
    public string? ErrorMessage { get; init; }      // Null if successful
    public ExportAllOperation? Operation { get; init; }  // Original operation context
}
```

**Factory Methods:**
```csharp
public static ExportResult Success(string filePath, int count, ExportAllOperation operation)
    => new() { IsSuccess = true, FilePath = filePath, ExportedCount = count, Operation = operation };

public static ExportResult Failure(string errorMessage, ExportAllOperation operation)
    => new() { IsSuccess = false, ErrorMessage = errorMessage, Operation = operation };
```

**Usage Example:**
```csharp
// In MainViewModel.ExecuteExportAllAsync:
ExportResult result = await PerformExportAsync(operation);
if (result.IsSuccess)
{
    StatusBarText = $"Exported {result.ExportedCount} messages to {Path.GetFileName(result.FilePath)}";
}
else
{
    StatusBarText = $"Export failed: {result.ErrorMessage}";
}
```

**Rationale:**
- Provides consistent return type across export operations
- Encapsulates both success and failure paths
- Enables structured logging and error reporting
- Optional: Can be added if error handling needs improvement

---

## Data Structures for Export Formats

### JSON Export Format

**Single Message DTO (Existing):**
```csharp
internal record MqttMessageExportDto
{
    public DateTime Timestamp { get; init; }
    public string Topic { get; init; }
    public string? ResponseTopic { get; init; }
    public MqttQualityOfServiceLevel QualityOfServiceLevel { get; init; }
    public bool Retain { get; init; }
    public uint MessageExpiryInterval { get; init; }
    public string? CorrelationData { get; init; }  // Hex format
    public MqttPayloadFormatIndicator PayloadFormatIndicator { get; init; }
    public string? ContentType { get; init; }
    public List<MqttUserPropertyDto>? UserProperties { get; init; }
    public string? Payload { get; init; }
}

internal record MqttUserPropertyDto(string Name, string Value);
```

**Bulk Export Structure (Array):**
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

---

### TXT Export Format

**Structure:**
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

**Delimiter:** 80-character line of equals signs (`=`)

---

## Relationships and Dependencies

### Message Flow

1. **User initiates export all:**
   - Via `:export all` command OR export all button click

2. **Create ExportAllOperation:**
   ```csharp
   var operation = new ExportAllOperation
   {
       TopicName = SelectedNode.FullPath,
       MessageCount = FilteredMessageHistory.Count,
       ExportedCount = Math.Min(FilteredMessageHistory.Count, 100),
       ExportFormat = Settings.ExportFormat ?? ExportTypes.json,
       OutputFilePath = GenerateFilePath(topicName, timestamp, format),
       Timestamp = DateTime.Now,
       IsLimitExceeded = FilteredMessageHistory.Count > 100
   };
   ```

3. **Select messages to export:**
   ```csharp
   var messagesToExport = FilteredMessageHistory
       .OrderByDescending(m => m.Timestamp)
       .Take(100)
       .Select(m => m.GetFullMessage())
       .Where(m => m != null)
       .ToList();
   ```

4. **Invoke exporter:**
   ```csharp
   IMessageExporter exporter = operation.ExportFormat == ExportTypes.json
       ? new JsonExporter()
       : new TextExporter();

   string? filePath = exporter.ExportAllToFile(
       messagesToExport,
       messagesToExport.Select(m => operation.Timestamp),  // Or individual timestamps
       operation.OutputFilePath);
   ```

5. **Report result:**
   ```csharp
   if (filePath != null)
   {
       StatusBarText = $"Exported {operation.ExportedCount} messages to {Path.GetFileName(filePath)}";
       if (operation.IsLimitExceeded)
           StatusBarText += $" (limited from {operation.MessageCount})";
   }
   ```

---

## State Transitions

### ExportAllOperation Lifecycle

```
┌─────────────┐
│   Created   │  User triggers export all
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Validated  │  Check MessageCount, TopicName, ExportPath
└──────┬──────┘
       │
       ├─── (invalid) ──► Error: Display validation message
       │
       ▼ (valid)
┌─────────────┐
│  Executing  │  Call exporter.ExportAllToFile()
└──────┬──────┘
       │
       ├─── (I/O error) ──► Error: Log error, show error message
       │
       ▼ (success)
┌─────────────┐
│  Completed  │  Display success message, log result
└─────────────┘
```

---

## Validation Summary

### Pre-Export Validation

**Check 1: Topic Selected**
```csharp
if (SelectedNode == null)
    return Error("No topic selected");
```

**Check 2: Messages Exist**
```csharp
if (!FilteredMessageHistory.Any())
    return Error("No messages to export");
```

**Check 3: Export Configuration Valid**
```csharp
if (Settings.ExportPath != null && !Directory.Exists(Settings.ExportPath))
{
    try { Directory.CreateDirectory(Settings.ExportPath); }
    catch { return Error($"Cannot access export path: {Settings.ExportPath}"); }
}
```

**Check 4: Filename Generation**
```csharp
string sanitizedTopic = SanitizeTopicName(SelectedNode.FullPath);
if (string.IsNullOrWhiteSpace(sanitizedTopic))
    return Error("Invalid topic name for filename");
```

---

## Persistence and Serialization

### Export File Persistence

**JSON Format:**
- Serialized using `System.Text.Json.JsonSerializer`
- Options: `WriteIndented = true` for readability
- Encoding: UTF-8
- File extension: `.json`

**TXT Format:**
- Plain text with UTF-8 encoding
- Newline: Environment.NewLine (platform-specific)
- File extension: `.txt`

**File Overwrite Policy:**
- Always overwrite if file exists (no confirmation)
- Confirmed by user in clarification session

**Atomic Writes:**
```csharp
// Write to temp file, then move (optional for reliability):
string tempPath = Path.GetTempFileName();
File.WriteAllText(tempPath, content);
File.Move(tempPath, outputFilePath, overwrite: true);
```

---

## Performance Considerations

### Memory Usage

**100-Message Limit:**
- Assumes ~1KB per message average
- Total in-memory: ~100KB (acceptable for in-memory serialization)
- Peak memory: 2× (messages collection + serialized string) = ~200KB

**Lazy Evaluation:**
```csharp
// Good: Lazy evaluation with Take(100)
var messages = FilteredMessageHistory
    .OrderByDescending(m => m.Timestamp)
    .Take(100)
    .Select(m => m.GetFullMessage())
    .ToList();  // Materializes only 100 items

// Bad: Materializes all before taking
var messages = FilteredMessageHistory.ToList()  // All messages in memory
    .OrderByDescending(m => m.Timestamp)
    .Take(100);
```

**File I/O:**
- Offload to thread pool: `await Task.Run(() => File.WriteAllText(...))`
- Prevents UI blocking during disk writes
- ~100KB write typically <50ms on modern SSD

---

## Extension Points

### Future Enhancements

1. **Configurable Export Limit:**
   ```csharp
   public int MaxExportMessages { get; set; } = 100;  // In Settings
   ```

2. **Progress Reporting:**
   ```csharp
   public interface IProgress<T>
   {
       void Report(T value);
   }

   // Usage:
   exporter.ExportAllToFile(messages, timestamps, filePath, progress);
   ```

3. **Streaming Export (for large batches):**
   ```csharp
   using var writer = new StreamWriter(filePath);
   foreach (var msg in messages)
   {
       var dto = CreateDto(msg);
       await writer.WriteLineAsync(JsonSerializer.Serialize(dto));
   }
   ```

4. **Additional Export Formats:**
   - CSV: Flat structure for spreadsheet import
   - XML: For legacy system integration
   - Parquet: For big data analytics

---

## Summary

This data model extends the existing export infrastructure with:

1. **New Value Object**: `ExportAllOperation` encapsulates bulk export state
2. **Extended Interface**: `IMessageExporter.ExportAllToFile()` for aggregated export
3. **Reused Entities**: `MessageViewModel`, `ExportConfiguration` (no changes)
4. **Clear Validation**: Pre-export checks prevent invalid operations
5. **Performance Bounds**: 100-message limit keeps memory and I/O manageable

All entities follow immutability patterns (records/readonly collections) and integrate seamlessly with existing reactive UI patterns (ReactiveCommand, ObservableCollection).

---

**Next Steps:**
- Phase 1 continues: Create contracts/ directory with API contracts
- Phase 1 continues: Create quickstart.md with usage examples
- Phase 1 continues: Update CLAUDE.md with new feature details
