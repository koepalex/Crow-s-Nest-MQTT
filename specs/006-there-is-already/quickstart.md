# Quickstart: Export All Messages Feature

**Feature**: Export All Messages from Topic History
**Version**: 1.0.0
**Date**: 2026-01-21

## Overview

This quickstart guide demonstrates the three ways to export MQTT messages in Crow's NestMQTT:
1. **Export All** via `:export all` command (bulk export up to 100 messages)
2. **Export All** via toolbar button click
3. **Export Single** via per-message button in history view

---

## Prerequisites

### Configuration

1. **Set Export Format** (in Settings):
   - Open Settings (Ctrl+Shift+P → `:settings`)
   - Select **Export Format**: `json` or `txt`

2. **Set Export Path** (in Settings):
   - In Settings panel, set **Export Path**: e.g., `C:\mqtt-exports` or `/home/user/mqtt-exports`
   - Path will be created automatically if it doesn't exist

3. **Connect to MQTT Broker**:
   ```
   :connect test.mosquitto.org:1883
   ```

4. **Collect Some Messages**:
   - Select a topic in the topic tree (e.g., `sensors/temperature`)
   - Wait for messages to arrive or publish some manually
   - Verify messages appear in history view (bottom panel)

---

## Scenario 1: Export All via Command

### Step 1: Select Topic

Click on a topic in the topic tree (left panel). The history view should show messages for that topic.

### Step 2: Execute Export All Command

**Option A: Use Default Settings**
```
:export all
```

**Expected Result:**
- File created: `{topic}_{timestamp}.json` (or `.txt`)
- Location: Your configured Export Path
- StatusBar message: `"Exported 25 messages to sensors_temperature_20260121_143045.json"`

**Option B: Specify Format and Path**
```
:export all json C:\custom-exports
```

or

```
:export all txt /home/user/custom-exports
```

**Expected Result:**
- File created in specified path
- Uses specified format regardless of settings

### Step 3: Verify Export File

**For JSON Format:**
```json
[
  {
    "Timestamp": "2026-01-21T14:30:45.123Z",
    "Topic": "sensors/temperature",
    "QualityOfServiceLevel": "AtLeastOnce",
    "Retain": false,
    "CorrelationData": "012AFF",
    "UserProperties": [
      {"Name": "source", "Value": "sensor-01"}
    ],
    "Payload": "{\"temperature\":22.5}"
  },
  {
    "Timestamp": "2026-01-21T14:30:46.456Z",
    "Topic": "sensors/temperature",
    "Payload": "{\"temperature\":23.1}"
  }
]
```

**For TXT Format:**
```
Timestamp: 2026-01-21 14:30:45.123
Topic: sensors/temperature
QoS: AtLeastOnce
Retain: false

--- User Properties ---
source: sensor-01

--- Payload ---
{"temperature":22.5}

================================================================================

Timestamp: 2026-01-21 14:30:46.456
Topic: sensors/temperature
...
```

---

## Scenario 2: Export All via Toolbar Button

### Step 1: Select Topic with Messages

1. Click on a topic in the topic tree
2. Verify messages appear in history view
3. Verify Export All button (download icon) next to Delete Topic button is **enabled**

### Step 2: Click Export All Button

1. Click the Export All button (looks like download icon)
2. Watch status bar for progress message

**Expected Result:**
- File created: `{topic}_{timestamp}.{ext}` in configured Export Path
- StatusBar message: `"Exported 42 messages to sensors_humidity_20260121_150305.json"`

### Step 3: Handle 100-Message Limit

**If topic has > 100 messages:**
- Status bar shows: `"Exported 100 of 175 messages to topic_timestamp.json (limit enforced)"`
- Only the **most recent 100 messages** are exported
- Older messages can be exported by filtering time range first (future enhancement)

---

## Scenario 3: Export Single Message via Row Button

### Step 1: Locate Message in History

1. Scroll through message history view (bottom panel)
2. Find the specific message you want to export
3. Notice two buttons on the right side of each row:
   - **Export button** (new - download icon)
   - **Copy button** (existing - clipboard icon)

### Step 2: Click Export Button for Specific Message

1. Click the **Export** button (left of Copy button) on desired row
2. Watch status bar for confirmation

**Expected Result:**
- Single message exported to configured path
- Filename: `{yyyyMMdd_HHmmssfff}_{topic}.{ext}` (existing single-export pattern)
- StatusBar message: `"Exported message to 20260121_150512_sensors_temperature.json"`

### Step 3: Verify Single Message File

**JSON Format:**
```json
{
  "Timestamp": "2026-01-21T15:05:12.789Z",
  "Topic": "sensors/temperature",
  "Payload": "{\"temperature\":24.3}"
}
```

**TXT Format:**
```
Timestamp: 2026-01-21 15:05:12.789
Topic: sensors/temperature
...
--- Payload ---
{"temperature":24.3}
```

---

## Scenario 4: Backward Compatibility - Existing `:export` Command

### Test Existing Export Still Works

**Command:**
```
:export
```
or
```
:export json C:\exports
```

**Expected Behavior:**
- Exports currently **selected message** from history (not all messages)
- Same as before - no changes to existing workflow
- Filename pattern unchanged

---

## Edge Cases

### Case 1: No Topic Selected

**Action:** Execute `:export all` without selecting a topic

**Expected:**
- StatusBar error: `"No topic selected for export all"`
- No file created
- Export All button is **disabled** (grayed out)

### Case 2: Topic Selected but No Messages

**Action:** Select a topic with zero messages in history, try to export all

**Expected:**
- StatusBar error: `"No messages to export"`
- No file created
- Export All button is **disabled**

### Case 3: Export Path Not Configured

**Action:** Execute `:export all` without settings configured

**Expected:**
- StatusBar error: `"Export format or path not configured"`
- Prompt user to configure settings first
- Alternative: Falls back to Documents folder (implementation choice)

### Case 4: File Already Exists

**Action:** Export all to same path twice in same second (filename collision)

**Expected:**
- Second export **overwrites** first file without warning
- StatusBar shows: `"Exported 10 messages to topic_timestamp.json"`
- No confirmation dialog (as per clarification)

### Case 5: Message Evicted from Buffer

**Action:** Click per-message export button for old message no longer in buffer

**Expected:**
- StatusBar error: `"Message no longer available in buffer"`
- No file created
- This happens when message is very old and buffer limit was reached

---

## Acceptance Validation Checklist

Use this checklist to validate feature implementation:

### Command Parsing
- [ ] `:export all` works with default settings
- [ ] `:export all json /path` works with explicit parameters
- [ ] `:export all txt /path` works with text format
- [ ] `:export all XML /path` shows error: "Invalid format"
- [ ] `:export` still exports single selected message (backward compatible)

### Export All Functionality
- [ ] Exports up to 100 messages from selected topic
- [ ] Uses configured export format (JSON or TXT)
- [ ] Uses configured export path
- [ ] Generates filename: `{topic}_{timestamp}.{ext}`
- [ ] Shows limit warning when topic has > 100 messages
- [ ] Shows success message with count and filename

### JSON Export Format
- [ ] Outputs valid JSON array: `[{message1}, {message2}, ...]`
- [ ] Each message includes all metadata (timestamp, QoS, retain, etc.)
- [ ] Correlation data in hexadecimal format
- [ ] User properties as array of name/value pairs
- [ ] Binary payloads omitted (null in Payload field)

### TXT Export Format
- [ ] Messages separated by `================================================================================` delimiter
- [ ] Each message uses existing detailed text format
- [ ] JSON payloads pretty-printed if ContentType is "application/json"
- [ ] No delimiter after last message

### UI - Export All Button
- [ ] Button visible next to Delete Topic button in toolbar
- [ ] Button enabled when topic selected AND messages exist
- [ ] Button disabled when no topic selected
- [ ] Button disabled when topic has zero messages
- [ ] Button click triggers bulk export
- [ ] Tooltip shows: "Export all messages from selected topic (max 100)"

### UI - Per-Message Export Button
- [ ] Button visible in each message history row
- [ ] Button positioned left of Copy button
- [ ] Button always enabled (no conditional logic)
- [ ] Button click exports that specific message
- [ ] Tooltip shows: "Export this message"
- [ ] Uses existing `:export` logic (same filename pattern)

### Error Handling
- [ ] Invalid format shows clear error message
- [ ] No topic selected shows error message
- [ ] No messages shows error message
- [ ] File write error shows error and logs to file
- [ ] Unconfigured settings shows error or uses fallback

### File Overwrite
- [ ] Existing file is overwritten without confirmation
- [ ] No modal dialog appears
- [ ] StatusBar message doesn't indicate overwrite (silent operation)

### Performance
- [ ] Command parsing < 1ms
- [ ] Export all 100 messages < 1 second
- [ ] UI remains responsive during export (async operation)
- [ ] No UI freezing or lag

---

## Validation Results

This section documents actual test results from the implementation (as of 2026-01-21).

### Unit Tests - FilenameGenerator

**Test Suite**: `FilenameGeneratorTests.cs`
**Location**: `tests/UnitTests/FilenameGeneratorTests.cs`
**Total Tests**: 45
**Status**: ✅ All Passed

**Key Validations**:
- ✅ MQTT hierarchy separators (`/`) replaced with underscores
- ✅ MQTT wildcards (`+`, `#`) handled correctly in filenames
- ✅ Invalid filename characters replaced (`:`, `?`, `*`, `<`, `>`, `|`, `"`, `\`)
- ✅ Control characters (0x00-0x1F) sanitized
- ✅ Empty/null topic names fallback to "unknown"
- ✅ Long topic names truncated to 200 characters
- ✅ Filename pattern: `{sanitized_topic}_{yyyyMMdd_HHmmss}.{ext}`
- ✅ Leading dots stripped from extensions
- ✅ Unique filename generation when files exist (appends `_1`, `_2`, etc.)

**Example Sanitizations**:
```
Input                                   → Output
──────────────────────────────────────────────────────────────────────
sensors/temperature                     → sensors_temperature
home/living/room/temperature            → home_living_room_temperature
$SYS/broker/uptime                      → $SYS_broker_uptime
zigbee2mqtt/bridge/config               → zigbee2mqtt_bridge_config
sensor/+                                → sensor_+
sensor/#                                → sensor_#
topic:with:colons                       → topic_with_colons
topic|with|pipes                        → topic_with_pipes
```

**Generated Filenames**:
```
sensors/temperature + 2026-01-21 14:30:45 → sensors_temperature_20260121_143045.json
home/sensors/+      + 2026-01-21 14:30:45 → home_sensors_+_20260121_143045.txt
```

### Contract Tests - Export All Command Parsing

**Test Suite**: `ExportAllCommandContractTests.cs`
**Location**: `tests/contract/ExportAllCommandContractTests.cs`
**Total Tests**: 6
**Status**: ✅ All Passed

**Validated Commands**:
- ✅ `:export all` → uses settings (format + path)
- ✅ `:export all json C:\exports` → explicit format and path
- ✅ `:export all txt /home/exports` → cross-platform paths
- ✅ `:export all XML C:\exports` → rejects invalid format
- ✅ `:export all json` → requires both format and path
- ✅ `:export` → backward compatibility (single message export)

### Contract Tests - Export Service Methods

**Test Suite**: `ExportAllServiceContractTests.cs`
**Location**: `tests/contract/ExportAllServiceContractTests.cs`
**Total Tests**: 8
**Status**: ✅ All Passed

**Validated Behaviors**:
- ✅ JSON export creates valid JSON array format: `[{msg1}, {msg2}]`
- ✅ TXT export separates messages with 80-equals delimiter
- ✅ Empty message collection returns `null` (no file created)
- ✅ Count mismatch throws `ArgumentException`
- ✅ Single message export creates valid JSON object (not array)
- ✅ Binary payloads represented as `null` in JSON
- ✅ CorrelationData converted to hexadecimal string
- ✅ UserProperties serialized as name/value pairs

### Integration Tests - End-to-End Scenarios

**Test Suite**: `ExportAllMessagesIntegrationTests.cs`
**Location**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
**Total Tests**: 7
**Status**: ✅ All Passed

**Validated Scenarios**:

**T023**: ✅ Export 50 messages to JSON array
- Created valid JSON file with 50 elements
- All elements have required properties (Topic, Timestamp, Payload)

**T024**: ✅ Export 150 messages (limit enforcement)
- Exporter accepts 150 messages (no limit in service layer)
- ViewModel enforces 100-message limit via `.Take(100)`

**T025**: ✅ Per-message export (single message)
- Created file with single JSON object (NOT array)
- Filename pattern: `{yyyyMMdd_HHmmssfff}_{topic}.json`

**T026**: ✅ Backward compatibility - `:export` without "all"
- Existing export behavior preserved
- Text format verified (no delimiter for single message)

**T027**: ✅ Empty history error handling
- Returns `null` for empty collection
- No file created

**T028**: ✅ File overwrite behavior
- Existing file overwritten silently
- Old content replaced without confirmation

**Additional**: ✅ Text export delimiter validation
- 3 messages → 2 delimiters (80 equals)
- No delimiter after last message

### Test Execution Summary

```
Test Category        Location                              Tests   Status
─────────────────────────────────────────────────────────────────────────────
Unit Tests           tests/UnitTests/                        45    ✅ PASS
Contract Tests       tests/contract/                         14    ✅ PASS
Integration Tests    tests/integration/                       7    ✅ PASS
─────────────────────────────────────────────────────────────────────────────
TOTAL                                                        66    ✅ PASS
```

**Build Status**: ✅ Success (0 errors, warnings suppressed)
**Test Execution Time**: ~1-4 seconds
**Platform Tested**: Windows (net10.0)

### Files Implemented

**Core Implementation**:
- `src/BusinessLogic/Commands/CommandParserService.cs` - Command parsing for `:export all`
- `src/BusinessLogic/Models/ExportAllOperation.cs` - Value object for bulk export
- `src/BusinessLogic/Exporter/IMessageExporter.cs` - Interface extension
- `src/BusinessLogic/Exporter/JsonExporter.cs` - JSON array export
- `src/BusinessLogic/Exporter/TextExporter.cs` - Delimited text export
- `src/Utils/FilenameGenerator.cs` - Cross-platform filename generation
- `src/UI/ViewModels/MainViewModel.cs` - Export commands and UI logic
- `src/UI/Views/MainView.axaml` - Export All button and per-message buttons

**Test Files**:
- `tests/UnitTests/FilenameGeneratorTests.cs` - 45 unit tests
- `tests/contract/ExportAllCommandContractTests.cs` - 6 contract tests
- `tests/contract/ExportAllServiceContractTests.cs` - 8 contract tests
- `tests/contract/UiExportButtonsContractTests.cs` - 3 placeholder tests
- `tests/integration/ExportAllMessagesIntegrationTests.cs` - 7 integration tests

---

## Troubleshooting

### Problem: Export All button is disabled

**Solutions:**
1. Ensure a topic is selected in topic tree
2. Verify selected topic has messages in history
3. Try selecting a different topic with messages

### Problem: "Export format or path not configured" error

**Solutions:**
1. Open Settings (`:settings`)
2. Set Export Format (json or txt)
3. Set Export Path (valid directory)
4. Click outside Settings to save
5. Retry `:export all` command

### Problem: File not created in expected location

**Solutions:**
1. Check configured Export Path in Settings
2. Verify directory exists and is writable
3. Check StatusBar for error messages
4. Review logs for I/O errors

### Problem: Only 100 messages exported but topic has more

**Expected Behavior:**
- This is the hard limit per FR-013
- StatusBar will show: `"Exported 100 of {total} messages (limit enforced)"`
- To export specific messages, use filtering first or per-message export

### Problem: Per-message export says "Message no longer available"

**Cause:**
- Message was evicted from in-memory buffer (very old message)

**Solutions:**
1. Export newer messages (those still in buffer)
2. Increase buffer size in Settings (if available)
3. Use Export All to capture recent messages before they're evicted

---

## Next Steps

After validating basic functionality:

1. **Test Cross-Platform:**
   - Test on Windows, Linux, and macOS
   - Verify file paths work on all platforms
   - Check filename sanitization on different filesystems

2. **Test with Real Data:**
   - Connect to production MQTT broker
   - Export messages from high-traffic topics
   - Verify all metadata is preserved correctly

3. **Test Edge Cases:**
   - Topics with special characters (`/`, `+`, `#`)
   - Very large payloads (> 1MB)
   - Messages with binary payloads
   - Messages with extensive user properties (> 10 properties)

4. **Performance Testing:**
   - Export 100 messages and measure time
   - Monitor memory usage during export
   - Verify UI responsiveness

5. **Integration Testing:**
   - Export, then import into external tool (verify format compatibility)
   - Test file overwrite scenarios
   - Test with concurrent exports (multiple users/instances)

---

## Summary

This feature provides three export modes:
1. **`:export all`** - Command-driven bulk export
2. **Export All button** - One-click bulk export
3. **Per-message button** - Quick single-message export

All modes respect:
- Configured export settings (format and path)
- 100-message limit for bulk operations
- Existing filename and format patterns
- Cross-platform compatibility

**Key Benefits:**
- No need to select each message individually
- Batch export for analysis or archival
- Command-driven workflow (keyboard accessible)
- GUI support for mouse-driven users
- Backward compatible with existing `:export` command

**Key Limitations:**
- 100-message maximum per export all operation
- Most recent messages prioritized (oldest may be excluded if count > 100)
- Overwrites files without confirmation (use timestamp for uniqueness)
- Messages evicted from buffer cannot be exported
