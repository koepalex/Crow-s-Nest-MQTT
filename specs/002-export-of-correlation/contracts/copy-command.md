# Copy Command Contract

**Command**: `:copy`
**Purpose**: Copy correlation data from metadata table to clipboard in readable format
**Layer**: UI Command → BusinessLogic Operation

## Input Contract

### Command Syntax
```
:copy
:copy correlation-data    # specific to this fix
```

### Parameters
- No parameters required - operates on currently selected correlation data

### Preconditions
- Correlation data is selected or currently displayed in metadata table
- Clipboard access is available (platform permissions)
- Correlation data exists and is accessible

## Output Contract

### Success Response
```csharp
public class CopyResult
{
    public bool Success { get; set; }
    public int CharacterCount { get; set; }
    public string Format { get; set; }
    public DateTime CopyTimestamp { get; set; }
}
```

### Clipboard Format
```
// For correlation data specifically:
// Format must match exactly what user sees in metadata table
// No base64 encoding applied
// Plain text format for clipboard
// Preserve line breaks and formatting

Example clipboard content:
correlation-id-12345
user-session-abc789
request-response-pair-456
```

### Error Responses
```csharp
public class CopyError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
}

// Error scenarios:
// - "NO_DATA": No correlation data selected
// - "CLIPBOARD_ACCESS": Cannot access system clipboard
// - "FORMAT_ERROR": Data format conversion failed
// - "SIZE_LIMIT": Data too large for clipboard
```

## Behavioral Contract

### Format Consistency Rule
```csharp
// CRITICAL REQUIREMENT:
// ClipboardData.Format == MetadataTable.DisplayFormat
//
// If metadata table shows: "correlation-id-12345"
// Clipboard must contain: "correlation-id-12345"
// NOT: "Y29ycmVsYXRpb24taWQtMTIzNDU=" (base64)
```

### Cross-Platform Requirements
- Clipboard handling uses platform-appropriate APIs
- Text format compatible with target platform clipboard expectations
- Unicode support for special characters in correlation data
- Proper handling of line endings for multi-line correlation data

### Performance Contract
- Copy operation completes in <50ms for typical correlation data
- UI remains responsive during copy operation
- Clipboard cleared properly on application exit (optional, based on user preference)

## Integration Contract

### UI Layer Interface
```csharp
public interface ICopyCommand
{
    Task<CopyResult> ExecuteAsync(CopyRequest request);
    bool CanExecute(object parameter);
    event EventHandler<CopyCompletedEventArgs> CopyCompleted;
}
```

### BusinessLogic Layer Interface
```csharp
public interface ICorrelationDataCopier
{
    Task<CopyResult> CopyCorrelationDataAsync(
        IEnumerable<CorrelationData> data,
        CopyFormat format);

    string FormatCorrelationDataForClipboard(CorrelationData data);
}
```

### Platform Integration Interface
```csharp
public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task<string> GetTextAsync();
    bool IsClipboardAvailable { get; }
}
```

### Test Contract Requirements
```csharp
// Must provide test implementations that verify:
// 1. Format consistency with metadata table display
// 2. Cross-platform clipboard handling
// 3. Error scenarios and recovery
// 4. Performance characteristics
// 5. Unicode/special character preservation
// 6. Large data handling (clipboard size limits)
```

## Security Considerations

### Data Privacy
- Clipboard data may be accessible to other applications
- Consider warning user if correlation data contains sensitive information
- Implement option to clear clipboard after specified time

### Platform Security
- Respect platform clipboard security policies
- Handle clipboard access permissions gracefully
- No persistent storage of clipboard operations