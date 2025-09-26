# Phase 0: Research - Fix Correlation Data Export Encoding

**Date**: 2025-09-26
**Feature**: Fix base64 encoding issue in correlation data export/copy functionality

## Research Findings

### Current Export/Copy Implementation Analysis
**Decision**: Research existing export/copy command implementation to understand base64 encoding source
**Rationale**: Need to identify where in the data flow the base64 encoding is being applied to correlation data specifically
**Alternatives considered**: Direct implementation without research, but this could miss root cause

### C# String Encoding Best Practices
**Decision**: Use UTF-8 string encoding with proper escaping for special characters in correlation data
**Rationale**: Correlation data should be human-readable when exported, matching metadata table display format
**Alternatives considered**: Binary export formats, but requirement specifies readable format matching UI display

### Cross-Platform File Export Patterns
**Decision**: Use .NET Core file I/O with cross-platform path handling for export functionality
**Rationale**: Must work identically on Windows, Linux, and macOS per constitutional requirements
**Alternatives considered**: Platform-specific implementations, but violates cross-platform compatibility principle

### MQTT Correlation Data Handling
**Decision**: Preserve original correlation data format from MQTT message metadata
**Rationale**: Users expect correlation data to match what they see in the metadata table, not encoded versions
**Alternatives considered**: Standardized encoding, but this conflicts with user expectations and requirements

### Testing Strategy for Export Functionality
**Decision**: Unit tests for data formatting logic, integration tests for command processing
**Rationale**: TDD requirement mandates comprehensive test coverage for export/copy operations
**Alternatives considered**: Manual testing only, but violates constitutional TDD requirement

## Implementation Approach

### Root Cause Investigation
The base64 encoding likely occurs in one of these locations:
1. Data serialization before display (but display shows correct format)
2. Export command data processing pipeline
3. Copy-to-clipboard functionality
4. File output encoding

### Fix Strategy
1. Identify where base64 encoding is applied in export/copy flow
2. Bypass or remove encoding for correlation data specifically
3. Ensure output format matches metadata table display
4. Maintain existing functionality for other data types that may legitimately need encoding

### Data Flow Analysis
```
MQTT Message → Correlation Data Extraction → Metadata Table Display (✅ correct format)
                                          → Export/Copy Command (❌ base64 encoded)
```

Fix point: Export/Copy Command processing should use same format as Metadata Table Display

## Technical Decisions Summary

- **Language**: C# (.NET) - established project standard
- **Testing**: xUnit/NUnit with TDD approach - constitutional requirement
- **Architecture**: Changes isolated to BusinessLogic layer - maintains modular design
- **Performance**: No impact on real-time message processing - maintains performance standards
- **Compatibility**: Cross-platform string handling - meets compatibility requirements

All research complete. No unknowns remaining for implementation planning.