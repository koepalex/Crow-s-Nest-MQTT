# Quickstart: Fix Correlation Data Export Encoding

**Feature**: Correlation data export/copy format consistency
**Test Duration**: 5-10 minutes
**Prerequisites**: Running Crow's NestMQTT application with MQTT connection and correlation data

## Test Scenarios

### Scenario 1: Export Command Format Verification

**Objective**: Verify `:export` command outputs readable correlation data

**Steps**:
1. Open Crow's NestMQTT application
2. Connect to MQTT broker with messages containing correlation data
3. Navigate to message with correlation data in metadata table
4. Note the correlation data format displayed in metadata table (e.g., "correlation-id-12345")
5. Execute command: `:export correlation-data test-export.txt`
6. Open the exported file `test-export.txt`

**Expected Result**:
- Exported file contains correlation data in same format as metadata table
- No base64 encoding present (should see "correlation-id-12345", not "Y29ycmVsYXRpb24taWQtMTIzNDU=")
- File is readable in any text editor
- Unicode/special characters preserved correctly

**Success Criteria**: ✅ Export format matches metadata table display exactly

---

### Scenario 2: Copy Command Format Verification

**Objective**: Verify `:copy` command copies readable correlation data to clipboard

**Steps**:
1. With correlation data visible in metadata table
2. Select correlation data entry
3. Execute command: `:copy correlation-data`
4. Open any text editor (Notepad, VS Code, etc.)
5. Paste clipboard content (Ctrl+V)

**Expected Result**:
- Pasted content matches correlation data format from metadata table
- No base64 encoding in clipboard content
- Text is immediately readable and usable
- Special characters/Unicode preserved

**Success Criteria**: ✅ Clipboard content matches metadata table display exactly

---

### Scenario 3: Special Characters Handling

**Objective**: Verify proper handling of correlation data with special characters

**Test Data Setup**:
- Correlation data containing: "user-ñame_测试@domain.com"
- Correlation data with symbols: "session#123$%^&*()"
- Correlation data with whitespace: "multi word correlation data"

**Steps**:
1. Display messages with special character correlation data in metadata table
2. Execute `:export` command for each test case
3. Execute `:copy` command for each test case
4. Verify exported files and clipboard content

**Expected Result**:
- All special characters preserved in both export and copy operations
- No character encoding issues (question marks, boxes, etc.)
- Format identical to metadata table display

**Success Criteria**: ✅ Special characters handled consistently across display/export/copy

---

### Scenario 4: Cross-Platform Consistency (if applicable)

**Objective**: Verify consistent behavior across different operating systems

**Steps**:
1. Test export functionality on Windows, Linux, and macOS
2. Compare exported file contents across platforms
3. Test copy/paste functionality across platforms
4. Verify file encoding and line endings

**Expected Result**:
- Identical correlation data format across all platforms
- Files readable on all platforms
- Clipboard functionality works consistently

**Success Criteria**: ✅ Cross-platform format consistency maintained

---

### Scenario 5: Error Handling Verification

**Objective**: Verify graceful error handling for edge cases

**Test Cases**:
- No correlation data selected: Execute `:export` → Should show clear error message
- Read-only export location: Execute `:export invalid-path/file.txt` → Should show access error
- Clipboard unavailable: Test `:copy` when clipboard is locked → Should show clipboard error

**Expected Result**:
- Clear, actionable error messages for each scenario
- Application remains stable and responsive
- User can recover from error states easily

**Success Criteria**: ✅ Robust error handling with helpful messages

## Validation Checklist

After running all scenarios:

- [ ] Export files contain readable correlation data (no base64)
- [ ] Clipboard content matches metadata table format
- [ ] Special characters preserved correctly
- [ ] Cross-platform behavior consistent (if tested)
- [ ] Error scenarios handled gracefully
- [ ] Performance acceptable (<100ms for commands)
- [ ] No regression in existing functionality

## Common Issues & Troubleshooting

**Issue**: Exported data still base64 encoded
**Solution**: Verify fix was applied to correct data processing pipeline

**Issue**: Special characters corrupted
**Solution**: Check UTF-8 encoding settings in export functionality

**Issue**: Copy command not working
**Solution**: Verify clipboard permissions and platform integration

**Issue**: Export command fails silently
**Solution**: Check error logging and file system permissions

## Success Metrics

- **Format Accuracy**: 100% match between metadata table and export/copy output
- **Character Preservation**: All Unicode/special characters handled correctly
- **Performance**: Commands complete in <100ms
- **Reliability**: No crashes or data corruption
- **Usability**: Clear error messages for failure scenarios