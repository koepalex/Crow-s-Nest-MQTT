# Research: JSON Viewer Default Expansion

**Feature**: 003-json-viewer-should
**Date**: 2025-10-08
**Status**: Complete

## Research Overview

This document consolidates technical research and decisions for implementing default JSON expansion in the viewer component.

## Technical Decisions

### 1. UI Framework Tree Expansion

**Decision**: Use WPF/Avalonia `TreeView` or `TreeViewItem` with `IsExpanded` property set programmatically during tree construction.

**Rationale**:
- Both WPF and Avalonia provide built-in tree view controls with expansion state management
- Setting `IsExpanded = true` during tree construction is more performant than post-construction traversal
- Cross-platform compatibility maintained through Avalonia abstraction layer

**Alternatives Considered**:
- Custom tree rendering: Rejected due to complexity and maintenance burden
- Third-party JSON tree controls: Rejected to minimize dependencies per constitution

**Best Practices**:
- Set expansion state during tree node creation (construction-time) rather than post-creation traversal
- Use virtualization for large trees to maintain UI responsiveness
- Bind `IsExpanded` to view model property for testability

### 2. Depth Tracking Algorithm

**Decision**: Track depth using integer counter incremented during recursive JSON parsing, expanding only nodes ≤ level 5.

**Rationale**:
- Simple O(n) time complexity with O(d) space complexity where d = max depth
- No additional data structures needed beyond existing JSON parsing tree
- Clear termination condition prevents accidental infinite expansion

**Implementation Approach**:
```csharp
void BuildJsonTree(JsonElement element, TreeNode parent, int currentDepth)
{
    bool shouldExpand = currentDepth <= 5;
    TreeNode node = new TreeNode { IsExpanded = shouldExpand };

    if (element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array)
    {
        foreach (var child in GetChildren(element))
        {
            BuildJsonTree(child, node, currentDepth + 1);
        }
    }

    parent.Children.Add(node);
}
```

**Best Practices**:
- Use zero-based or one-based indexing consistently (recommend 1-based for user-facing depth)
- Add depth as metadata to tree nodes for debugging
- Log depth distribution for large JSONs to monitor performance

### 3. State Reset Mechanism

**Decision**: Rebuild tree from scratch when switching messages rather than resetting existing tree state.

**Rationale**:
- Simpler implementation - no need to traverse existing tree to reset state
- Avoids stale state bugs from incomplete reset logic
- Memory footprint is acceptable for typical MQTT message sizes (<1MB per spec)
- Aligns with existing message switching behavior (likely already rebuilding view)

**Alternatives Considered**:
- State reset via tree traversal: Rejected due to complexity and potential for subtle bugs
- State persistence across messages: Rejected per requirements (FR-006)

**Best Practices**:
- Dispose old tree properly to avoid memory leaks
- Use async/await for tree construction to maintain UI responsiveness
- Show loading indicator for large trees (>1000 nodes)

### 4. Cross-Context Consistency

**Decision**: Extract expansion logic into shared `JsonTreeBuilder` utility class in Utils layer, consumed by all UI contexts.

**Rationale**:
- DRY principle - single source of truth for expansion behavior
- Ensures consistent behavior across `:view json`, previews, command palette
- Easier to test - one unit test suite covers all contexts
- Respects modular architecture (Utils layer for shared logic)

**Implementation Approach**:
```csharp
// Utils/JsonTreeBuilder.cs
public class JsonTreeBuilder
{
    private const int MaxAutoExpandDepth = 5;

    public TreeNode BuildTree(JsonDocument document)
    {
        return BuildTreeRecursive(document.RootElement, depth: 1);
    }

    private TreeNode BuildTreeRecursive(JsonElement element, int depth)
    {
        // Expansion logic here
    }
}

// UI/JsonViewer.cs
public class JsonViewer : UserControl
{
    private readonly JsonTreeBuilder _treeBuilder;

    public void DisplayJson(JsonDocument document)
    {
        var tree = _treeBuilder.BuildTree(document);
        TreeViewControl.ItemsSource = tree.Children;
    }
}
```

**Best Practices**:
- Inject `JsonTreeBuilder` via dependency injection for testability
- Add configuration parameter to builder for future extensibility (e.g., `maxDepth`)
- Log builder usage for observability

### 5. Performance Optimization

**Decision**: Use lazy-loading for nodes beyond depth 5, materializing children only when user expands parent.

**Rationale**:
- Prevents memory/CPU waste parsing deep JSON that user may never view
- Maintains <100ms render time for typical payloads
- Standard pattern in tree controls - well-supported by WPF/Avalonia

**Implementation Approach**:
- Mark nodes at depth > 5 as "HasChildren = true" but don't populate children
- Attach expand event handler to materialize children on-demand
- Use placeholder node ("Loading...") during async load if needed

**Alternatives Considered**:
- Fully materialize entire tree: Rejected due to performance concerns for deeply nested JSON
- Virtual scrolling only: Rejected as orthogonal concern (addresses width, not depth)

**Best Practices**:
- Profile with 10-level nested JSON to validate <100ms target
- Add telemetry for expansion depth distribution in production
- Consider background parsing for very large payloads

### 6. Edge Case Handling

**Decision Matrix**:

| Edge Case | Behavior | Rationale |
|-----------|----------|-----------|
| Malformed JSON | Show parse error, don't attempt expansion | User needs to fix JSON; partial expansion misleading |
| Circular references | Not applicable | JSON spec prohibits cycles; parser will error |
| Large arrays (1000+ items) | Expand container, virtualize items | Maintain expansion UX while avoiding DOM bloat |
| Mixed depth (some branches >5, some <5) | Expand each branch to its own 5-level limit | Consistent per-branch behavior |

**Best Practices**:
- Log edge case occurrences for product insights
- Provide user feedback (status bar) for large JSON processing
- Add escape hatch (e.g., `:view raw`) if expansion causes issues

## Dependencies

**No new dependencies required**:
- System.Text.Json (already in project per CLAUDE.md)
- WPF/Avalonia (existing UI framework)
- xUnit/NUnit (existing test framework)

## Open Questions

**Resolved during clarification session**:
- ✅ Scope of expansion (all contexts)
- ✅ User configurability (none)
- ✅ State persistence (reset per message)
- ✅ Depth limit (5 levels)

**Deferred to implementation**:
- Exact large payload threshold for loading indicator (profile during development)
- Telemetry events to track (determine during task execution)

## Testing Strategy

**Unit Tests**:
- `JsonTreeBuilder.BuildTree()` with 1-5 level JSON → all expanded
- `JsonTreeBuilder.BuildTree()` with 6+ level JSON → only first 5 expanded
- Depth calculation accuracy across different JSON structures
- Tree rebuild logic (verify no state leaks between calls)

**Integration Tests**:
- `:view json` command displays expanded tree
- Message preview displays expanded tree
- Command palette JSON displays expanded tree
- Switch messages → verify state reset

**Manual Tests** (from spec):
- Large payload (1000+ properties) → verify performance
- Deep nesting (10+ levels) → verify correct partial expansion
- Malformed JSON → verify graceful error handling

## Constitutional Compliance

All research decisions align with constitutional principles:

- **I. User-Centric Interface**: Leverages existing `:view json` command
- **II. Real-Time Performance**: 5-level depth limit + lazy loading
- **III. Test-Driven Development**: Tests outlined above
- **IV. Modular Architecture**: Shared `JsonTreeBuilder` in Utils layer
- **V. Cross-Platform Compatibility**: WPF/Avalonia abstractions

No constitutional violations require complexity tracking.

## Next Steps

Proceed to Phase 1 (Design & Contracts):
- Define data model for tree nodes
- Extract viewer contracts from requirements
- Generate quickstart.md with manual test scenarios
- Update CLAUDE.md with new technical context
