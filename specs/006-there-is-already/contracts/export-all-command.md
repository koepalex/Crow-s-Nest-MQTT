# Contract: Export All Command

**Feature**: Export All Messages from Topic History
**Contract Type**: Command Parsing
**Version**: 1.0.0
**Date**: 2026-01-21

## Overview

This contract defines the behavior of the `:export all` command variant in the Crow's NestMQTT application. It extends the existing `:export` command with bulk export capability while maintaining backward compatibility.

## Command Syntax

### Variant 1: Export All with Settings
```
:export all
```

**Preconditions:**
- User has configured export settings (Settings.ExportFormat and Settings.ExportPath)
- A topic is selected in the UI
- Selected topic has at least one message in history

**Postconditions:**
- Up to 100 most recent messages exported to auto-generated file
- File named: `{sanitized_topic}_{timestamp}.{ext}`
- Status bar shows success message with exported count
- If message count > 100, status bar includes limit warning

**Errors:**
- No topic selected: "No topic selected for export all"
- No messages in history: "No messages to export"
- Export settings not configured: "Export format or path not configured"

---

### Variant 2: Export All with Explicit Format and Path
```
:export all json /path/to/exports
:export all txt C:\exports
```

**Preconditions:**
- A topic is selected in the UI
- Selected topic has at least one message in history
- Specified path is valid and writable
- Format is either "json" or "txt" (case-insensitive)

**Postconditions:**
- Up to 100 most recent messages exported to specified path
- File named: `{sanitized_topic}_{timestamp}.{ext}`
- Status bar shows success message with exported count
- If message count > 100, status bar includes limit warning

**Errors:**
- Invalid format: "Invalid format for :export all. Expected 'json' or 'txt'"
- Invalid path: "Export path not accessible: {path}"
- No topic selected: "No topic selected for export all"
- No messages in history: "No messages to export"

---

### Variant 3: Single Message Export (Existing - Unchanged)
```
:export
:export json /path/to/exports
```

**Preconditions:**
- A message is selected in the message history view
- For parameterless variant: Settings configured

**Postconditions:**
- Selected message exported to file (existing behavior)
- No changes to existing functionality

**Errors:**
- No message selected: "No message selected for export"
- (Other existing error messages unchanged)

---

## Parsing Logic

### Input Processing

**Step 1: Tokenize Command**
```csharp
// Input: ":export all json /exports"
// After SplitArguments():
// commandName = "export"
// arguments = ["all", "json", "/exports"]
```

**Step 2: Check for "all" Parameter**
```csharp
if (arguments.Count >= 1 && arguments[0].ToLowerInvariant() == "all")
{
    // Export all mode
}
else
{
    // Single export mode (existing logic)
}
```

**Step 3: Parse Export All Arguments**
```csharp
// Case 1: :export all (0 additional args after "all")
if (arguments.Count == 1)
{
    // Use settings
    if (settingsData.ExportFormat != null && settingsData.ExportPath != null)
    {
        return CommandResult.SuccessCommand(
            new ParsedCommand(CommandType.Export,
                ["all", settingsData.ExportFormat.ToString(), settingsData.ExportPath]));
    }
    else
    {
        return CommandResult.Failure("Export format or path not configured");
    }
}

// Case 2: :export all json /path (2 additional args after "all")
else if (arguments.Count == 3)
{
    string format = arguments[1].ToLowerInvariant();
    string path = arguments[2];

    if (format != "json" && format != "txt")
    {
        return CommandResult.Failure("Invalid format for :export all. Expected 'json' or 'txt'");
    }

    return CommandResult.SuccessCommand(
        new ParsedCommand(CommandType.Export, ["all", format, path]));
}

// Invalid argument count
else
{
    return CommandResult.Failure(
        "Invalid arguments for :export all. Expected: :export all OR :export all <format> <path>");
}
```

---

## Contract Tests

### Test 1: Export All with Settings
```csharp
[Fact]
public void ParseCommand_ExportAll_WithSettings_ReturnsSuccess()
{
    // Arrange
    var settings = new SettingsData(ExportFormat: ExportTypes.json, ExportPath: "/exports");
    var parser = new CommandParserService(settings);

    // Act
    var result = parser.ParseCommand(":export all");

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.ParsedCommand);
    Assert.Equal(CommandType.Export, result.ParsedCommand.Type);
    Assert.Equal(3, result.ParsedCommand.Arguments.Count);
    Assert.Equal("all", result.ParsedCommand.Arguments[0]);
    Assert.Equal("json", result.ParsedCommand.Arguments[1]);
    Assert.Equal("/exports", result.ParsedCommand.Arguments[2]);
}
```

### Test 2: Export All with Explicit Parameters
```csharp
[Fact]
public void ParseCommand_ExportAll_WithExplicitParams_ReturnsSuccess()
{
    // Arrange
    var parser = new CommandParserService(new SettingsData());

    // Act
    var result = parser.ParseCommand(":export all txt /custom/path");

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.ParsedCommand);
    Assert.Equal(CommandType.Export, result.ParsedCommand.Type);
    Assert.Equal(3, result.ParsedCommand.Arguments.Count);
    Assert.Equal("all", result.ParsedCommand.Arguments[0]);
    Assert.Equal("txt", result.ParsedCommand.Arguments[1]);
    Assert.Equal("/custom/path", result.ParsedCommand.Arguments[2]);
}
```

### Test 3: Export All with Invalid Format
```csharp
[Fact]
public void ParseCommand_ExportAll_InvalidFormat_ReturnsFailure()
{
    // Arrange
    var parser = new CommandParserService(new SettingsData());

    // Act
    var result = parser.ParseCommand(":export all xml /path");

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("Invalid format for :export all. Expected 'json' or 'txt'", result.ErrorMessage);
}
```

### Test 4: Export All without Settings
```csharp
[Fact]
public void ParseCommand_ExportAll_NoSettings_ReturnsFailure()
{
    // Arrange
    var parser = new CommandParserService(new SettingsData());  // No export settings

    // Act
    var result = parser.ParseCommand(":export all");

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Equal("Export format or path not configured", result.ErrorMessage);
}
```

### Test 5: Export All Case Insensitive
```csharp
[Theory]
[InlineData(":export ALL json /path")]
[InlineData(":export All json /path")]
[InlineData(":export aLl json /path")]
public void ParseCommand_ExportAll_CaseInsensitive_ReturnsSuccess(string command)
{
    // Arrange
    var parser = new CommandParserService(new SettingsData());

    // Act
    var result = parser.ParseCommand(command);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal("all", result.ParsedCommand!.Arguments[0]);  // Normalized to lowercase
}
```

### Test 6: Backward Compatibility - Single Export Unchanged
```csharp
[Fact]
public void ParseCommand_Export_WithoutAll_UsesExistingLogic()
{
    // Arrange
    var settings = new SettingsData(ExportFormat: ExportTypes.json, ExportPath: "/exports");
    var parser = new CommandParserService(settings);

    // Act
    var result = parser.ParseCommand(":export");

    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.ParsedCommand);
    Assert.Equal(CommandType.Export, result.ParsedCommand.Type);
    Assert.Equal(2, result.ParsedCommand.Arguments.Count);  // No "all" parameter
    Assert.Equal("json", result.ParsedCommand.Arguments[0]);
    Assert.Equal("/exports", result.ParsedCommand.Arguments[1]);
}
```

---

## Argument Structure

### ParsedCommand Arguments for Export All

**Format:**
```csharp
ParsedCommand
{
    Type = CommandType.Export,
    Arguments = [
        "all",              // [0] - Scope indicator
        "json",             // [1] - Export format
        "/path/to/exports"  // [2] - Export path
    ]
}
```

**Dispatch Logic in MainViewModel:**
```csharp
private async Task HandleExportCommand(ParsedCommand command)
{
    if (command.Arguments.Count >= 3 && command.Arguments[0] == "all")
    {
        // Export all mode
        string format = command.Arguments[1];
        string path = command.Arguments[2];
        await ExecuteExportAllAsync(format, path);
    }
    else if (command.Arguments.Count == 2)
    {
        // Single export mode (existing)
        string format = command.Arguments[0];
        string path = command.Arguments[1];
        await ExecuteExportAsync(format, path);
    }
    else
    {
        StatusBarText = "Invalid export command arguments";
    }
}
```

---

## Error Handling

### Validation Order

1. **Command parsing**: Syntax errors (invalid format, wrong arg count)
2. **Precondition checks**: Topic selected, messages exist
3. **Path validation**: Directory accessible/writable
4. **Export execution**: File I/O errors

### Error Message Standards

**Command Parsing Errors:**
- Clear indication of what's wrong
- Show expected syntax
- Case-insensitive format names in error messages

**Runtime Errors:**
- Distinguish between user errors (no topic selected) and system errors (I/O failure)
- Log system errors to file, show friendly message to user
- Include actionable information (e.g., "Check export path in Settings")

---

## Success Criteria

✅ **Command parses correctly** for all variants
✅ **Backward compatibility** maintained (`:export` behavior unchanged)
✅ **Error messages** clear and actionable
✅ **Argument structure** consistent and testable
✅ **Case insensitivity** for "all" and format parameters
✅ **Settings integration** works when no explicit parameters provided

---

## Non-Functional Requirements

**Performance:**
- Command parsing < 1ms (simple string comparison and validation)
- No regex or complex parsing required

**Usability:**
- Intuitive syntax extension of existing command
- Discoverable through command palette autocomplete
- Consistent with other parameterized commands (`:filter`, `:deletetopic`)

**Maintainability:**
- Extends existing switch case in CommandParserService
- Reuses CommandResult and ParsedCommand classes
- No new command type enum value needed

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-01-21 | Initial contract definition |

---

**Related Contracts:**
- [export-all-service.md](./export-all-service.md) - IMessageExporter extension
- [ui-export-buttons.md](./ui-export-buttons.md) - UI button contracts
