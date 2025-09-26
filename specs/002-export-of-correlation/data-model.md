# Data Model - Fix Correlation Data Export Encoding

**Date**: 2025-09-26
**Feature**: Correlation data export/copy format consistency

## Entity: Correlation Data

### Definition
Correlation data represents metadata associated with MQTT messages that links related messages together. This data is displayed in the metadata table and should be exportable/copyable in the same human-readable format.

### Attributes
- **Value**: string - The actual correlation data content as displayed in metadata table
- **DisplayFormat**: string - Human-readable representation shown in UI
- **SourceEncoding**: string - Original encoding from MQTT message (if applicable)
- **IsReadable**: boolean - Indicates if data can be displayed as readable text

### State Transitions
```
MQTT Message Received → Correlation Data Extracted → Display Format Applied → UI Table Display
                                                                           → Export/Copy (should maintain Display Format)
```

### Validation Rules
- Display format must preserve original correlation data meaning
- Export format must match display format exactly
- No base64 encoding should be applied to readable correlation data
- Special characters should be preserved as shown in metadata table

### Business Rules
1. **Format Consistency**: Export/copy output must match metadata table display format
2. **Encoding Preservation**: If metadata table shows readable text, export should be readable text
3. **Special Character Handling**: Maintain same character representation across display and export
4. **Binary Data Handling**: If correlation data is legitimately binary, handle appropriately but consistently

## Entity: Export Operation

### Definition
Represents the action of exporting correlation data via `:export` command with format consistency requirements.

### Attributes
- **SourceData**: CorrelationData - The correlation data to export
- **TargetFormat**: string - Output format (should match display format)
- **FilePath**: string - Export destination (if applicable)
- **Success**: boolean - Operation completion status

### Business Rules
1. **Format Matching**: Output format must match metadata table display
2. **File Encoding**: Use UTF-8 for text files, preserve readability
3. **Error Handling**: Report format conversion issues clearly

## Entity: Copy Operation

### Definition
Represents the action of copying correlation data to clipboard via `:copy` command.

### Attributes
- **SourceData**: CorrelationData - The correlation data to copy
- **ClipboardFormat**: string - Format for clipboard (should match display format)
- **Success**: boolean - Operation completion status

### Business Rules
1. **Clipboard Format**: Data copied must match metadata table display format
2. **Platform Compatibility**: Clipboard handling must work consistently across platforms
3. **Size Limits**: Handle large correlation data appropriately

## Data Flow Requirements

### Current State (Problem)
```
CorrelationData → MetadataTable.Display (✅ readable format)
CorrelationData → Export.Process → Base64Encode (❌ unreadable)
CorrelationData → Copy.Process → Base64Encode (❌ unreadable)
```

### Target State (Solution)
```
CorrelationData → MetadataTable.Display (✅ readable format)
CorrelationData → Export.Process → SameAsDisplay (✅ readable format)
CorrelationData → Copy.Process → SameAsDisplay (✅ readable format)
```

## Integration Points

### UI Layer
- Metadata table display logic (reference implementation)
- Export/copy command handlers
- Error message display

### Business Logic Layer
- Correlation data formatting logic
- Export/copy operation processors
- Format consistency validators

### Platform Integration
- File system access (export)
- Clipboard access (copy)
- Cross-platform encoding handling