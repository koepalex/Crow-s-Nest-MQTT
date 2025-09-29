# Export Command Contract

**Command**: `:export`
**Purpose**: Export correlation data from metadata table in readable format
**Layer**: UI Command → BusinessLogic Operation

## Input Contract

### Command Syntax
```
:export [format] [filepath]
:export correlation-data [filepath]  # specific to this fix
```

### Parameters
- **format**: string (optional) - Export format type, defaults to current selection
- **filepath**: string (optional) - Export destination, prompts if not provided

### Preconditions
- Correlation data is selected or currently displayed in metadata table
- User has write permissions to target directory (if filepath provided)
- Correlation data exists and is accessible

## Output Contract

### Success Response
```csharp
public class ExportResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; }
    public int RecordCount { get; set; }
    public string Format { get; set; }
    public DateTime ExportTimestamp { get; set; }
}
```

### Export File Format
```
// For correlation data specifically:
// Output format must match exactly what user sees in metadata table
// No base64 encoding applied
// UTF-8 text encoding
// Platform-appropriate line endings

Example output:
correlation-id-12345
user-session-abc789
request-response-pair-456
```

### Error Responses
```csharp
public class ExportError
{
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
}

// Error scenarios:
// - "NO_DATA": No correlation data selected
// - "ACCESS_DENIED": Cannot write to target location
// - "FORMAT_ERROR": Data format conversion failed
// - "IO_ERROR": File system operation failed
```

## Behavioral Contract

### Format Consistency Rule
```csharp
// CRITICAL REQUIREMENT:
// ExportedData.Format == MetadataTable.DisplayFormat
//
// If metadata table shows: "correlation-id-12345"
// Export file must contain: "correlation-id-12345"
// NOT: "Y29ycmVsYXRpb24taWQtMTIzNDU=" (base64)
```

### Cross-Platform Requirements
- File paths normalized for target platform
- Text encoding set to UTF-8 with BOM for Windows compatibility
- Line endings appropriate for target platform (\r\n Windows, \n Unix)

### Performance Contract
- Export operation completes in <100ms for typical correlation data
- Progress feedback for large datasets (>1000 records)
- Non-blocking UI operation (async execution)

## Integration Contract

### UI Layer Interface
```csharp
public interface IExportCommand
{
    Task<ExportResult> ExecuteAsync(ExportRequest request);
    bool CanExecute(object parameter);
    event EventHandler<ExportCompletedEventArgs> ExportCompleted;
}
```

### BusinessLogic Layer Interface
```csharp
public interface ICorrelationDataExporter
{
    Task<ExportResult> ExportCorrelationDataAsync(
        IEnumerable<CorrelationData> data,
        string filePath,
        ExportFormat format);

    string FormatCorrelationData(CorrelationData data, ExportFormat format);
}
```

### Test Contract Requirements
```csharp
// Must provide test implementations that verify:
// 1. Format consistency with metadata table display
// 2. Cross-platform file handling
// 3. Error scenarios and recovery
// 4. Performance under load
// 5. Unicode/special character preservation
```