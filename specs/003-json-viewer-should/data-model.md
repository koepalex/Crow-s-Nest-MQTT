# Data Model: JSON Viewer Default Expansion

**Feature**: 003-json-viewer-should
**Date**: 2025-10-08

## Overview

This document defines the data structures and state management for JSON viewer expansion behavior.

## Entities

### JsonTreeNode

Represents a single node in the JSON tree visualization.

**Properties**:
- `Key`: string - Property name (for objects) or index (for arrays) or "root" for top-level
- `Value`: object - The JSON value (string, number, bool, null, or complex type indicator)
- `ValueKind`: JsonValueKind enum - Type discriminator (Object, Array, String, Number, True, False, Null)
- `Depth`: int - Nesting level (1-based: root = 1, root's children = 2, etc.)
- `IsExpanded`: bool - Expansion state for UI binding
- `IsExpandable`: bool - Whether node has children (Object/Array with items)
- `Children`: IObservableCollection<JsonTreeNode> - Child nodes (for Object/Array types)
- `Parent`: JsonTreeNode? - Reference to parent node (null for root)

**Validation Rules** (from FR-008):
- `IsExpanded` = true when `Depth <= 5` AND `IsExpandable` = true
- `IsExpanded` = false when `Depth > 5` OR `IsExpandable` = false
- `Children` = empty collection when `ValueKind` is scalar (String, Number, True, False, Null)
- `Children` populated when `ValueKind` is Object or Array

**State Transitions**:
```
State: Collapsed → Expanded
Trigger: User clicks expand control OR initial render with Depth <= 5
Action: Set IsExpanded = true, materialize Children if lazy-loaded

State: Expanded → Collapsed
Trigger: User clicks collapse control
Action: Set IsExpanded = false (Children remain in memory)

State: * → Reset
Trigger: User switches to different message (FR-006)
Action: Dispose tree, rebuild from new JSON with default expansion state
```

**Relationships**:
- JsonTreeNode (parent) ← 1:N → JsonTreeNode (children)
- JsonTreeNode → (wraps) → System.Text.Json.JsonElement

### JsonTreeBuilder (Utility)

Responsible for constructing JsonTreeNode hierarchy from System.Text.Json.JsonDocument.

**Properties**:
- `MaxAutoExpandDepth`: const int = 5 - Depth threshold for auto-expansion

**Methods**:
- `BuildTree(JsonDocument document)`: JsonTreeNode
  - Entry point, returns root node
  - Initiates recursive build with depth = 1

- `BuildTreeRecursive(JsonElement element, string key, int depth, JsonTreeNode? parent)`: JsonTreeNode
  - Recursive worker method
  - Sets IsExpanded = (depth <= MaxAutoExpandDepth)
  - Recursively processes children for Object/Array types

**Constraints**:
- Thread-safe (multiple JSON documents may be parsed concurrently)
- No state retention between `BuildTree` calls (stateless utility)

### JsonViewerViewModel (UI Layer)

View model for JSON viewer component, managing display state.

**Properties**:
- `RootNode`: JsonTreeNode? - Current tree root (bound to UI)
- `CurrentMessage`: MqttMessage - Source message being displayed
- `IsLoading`: bool - Loading indicator for async tree construction
- `ErrorMessage`: string? - Parse error message if JSON invalid

**Methods**:
- `LoadJsonAsync(MqttMessage message)`: Task
  - Parse JSON, build tree, update RootNode
  - Set IsLoading during operation
  - Handle parse errors gracefully

- `RefreshTree()`: void
  - Rebuild tree from CurrentMessage
  - Implements FR-006 (state reset)

**Validation Rules**:
- CurrentMessage.Payload must be valid JSON (catch JsonException)
- RootNode replaced (not modified in-place) when switching messages

**State Lifecycle**:
```
Initial: RootNode = null, CurrentMessage = null
  ↓
User executes :view json
  ↓
State: IsLoading = true, ErrorMessage = null
  ↓
Parse JSON + Build Tree (async)
  ↓
Success: RootNode = tree, IsLoading = false
OR
Failure: ErrorMessage = ex.Message, IsLoading = false
  ↓
User switches message
  ↓
Dispose old RootNode, repeat cycle
```

## Integration Points

### With UI Layer
- `JsonViewer.xaml` binds to `JsonViewerViewModel.RootNode`
- TreeView ItemsSource = RootNode.Children
- TreeViewItem.IsExpanded bound to JsonTreeNode.IsExpanded
- TreeViewItem.Header displays Key + Value representation

### With BusinessLogic Layer
- Receives MqttMessage from message processing pipeline
- No modifications to BusinessLogic needed - purely presentation concern
- BusinessLogic provides message via existing message selection events

### With Utils Layer
- JsonTreeBuilder utility class in Utils/JsonTreeBuilder.cs
- Shared across all JSON display contexts (`:view json`, previews, palette)
- Dependency-injected into JsonViewerViewModel

## Performance Considerations

**Memory**:
- Tree structure overhead: ~100 bytes/node (object + properties)
- 1000-node tree ≈ 100KB additional memory (acceptable per constitution)
- Large array optimization: Virtualize Items (ObservableCollection handles this)

**CPU**:
- Tree construction: O(n) where n = node count (up to depth 5)
- Lazy loading beyond depth 5 defers work until user expands
- Typical 5-level JSON with 100 nodes/level = 111,111 nodes worst case (0.1111s @ 1M nodes/sec)

**UI Rendering**:
- WPF/Avalonia TreeView uses virtualization by default
- Only render visible nodes (scrolling viewport)
- Maintains <100ms responsiveness target per constitution

## Testing Contracts

**Unit Test Scenarios**:
1. JsonTreeBuilder with flat JSON (1 level) → all expanded
2. JsonTreeBuilder with 5-level JSON → all expanded
3. JsonTreeBuilder with 6-level JSON → first 5 expanded, level 6 collapsed
4. JsonTreeNode.IsExpanded state transitions
5. JsonViewerViewModel.LoadJsonAsync with valid JSON
6. JsonViewerViewModel.LoadJsonAsync with malformed JSON → ErrorMessage set
7. JsonViewerViewModel.RefreshTree → new tree instance created

**Integration Test Scenarios** (from spec acceptance criteria):
1. `:view json` command → verify RootNode.IsExpanded = true for depth ≤ 5
2. Switch messages → verify new RootNode instance (not same object)
3. Manually collapse node → verify IsExpanded = false persists until message switch
4. Large payload (1000+ properties) → verify completion <1s

## Non-Functional Requirements

**From Constitution & Spec**:
- **Performance**: Tree construction <100ms for typical payloads (<1000 nodes)
- **Memory**: Tree overhead <10% of payload size
- **Reliability**: Graceful handling of malformed JSON (no crashes)
- **Testability**: All components dependency-injected, no static state
- **Cross-Platform**: Identical behavior on Windows/Linux/macOS

## Change Impact

**Files Modified**:
- None (existing JSON viewer component)

**Files Created**:
- `Utils/JsonTreeBuilder.cs` - New utility class
- `UI/ViewModels/JsonViewerViewModel.cs` - May need modifications to use builder
- `UI/Views/JsonViewer.xaml(.cs)` - May need binding updates

**Dependencies**:
- No new external dependencies
- Internal dependency: UI → Utils (already permitted by architecture)

## Migration Notes

N/A - New feature, no migration from previous state.

## Glossary

- **Depth**: Nesting level, 1-based (root = 1)
- **Node**: Single element in JSON tree (object property, array item, or scalar value)
- **Expansion State**: Boolean indicating whether node's children are visible in UI
- **Auto-Expansion**: Setting IsExpanded = true during initial tree construction
- **State Reset**: Rebuilding tree from scratch when switching messages
