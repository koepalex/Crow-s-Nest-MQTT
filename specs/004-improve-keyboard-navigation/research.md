# Research: Keyboard Navigation Enhancements

**Feature**: 004-improve-keyboard-navigation
**Date**: 2025-10-20

## Research Questions & Findings

### 1. Keyboard Event Handling in WPF/Avalonia

**Decision**: Use PreviewKeyDown events at application/window level for global shortcuts, with conditional suppression when command palette has focus.

**Rationale**:
- PreviewKeyDown events bubble up from child controls, allowing global capture
- Can check `FocusManager.GetFocusedElement()` to determine if command palette is active
- Handled flag prevents event from propagating to text input when shortcuts are active
- Cross-platform compatible through WPF/Avalonia abstraction layers

**Alternatives Considered**:
- **Input bindings**: Rejected because they don't support conditional behavior based on focus context
- **Global keyboard hooks**: Rejected due to platform-specific code requirements and security concerns
- **Command pattern only**: Rejected because `j`/`k`/`n`/`N` need to work outside command palette

**References**:
- WPF Routed Events: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/routed-events-overview
- Avalonia Input: https://docs.avaloniaui.net/docs/input/

---

### 2. Case-Insensitive String Matching for Topic Search

**Decision**: Use `StringComparison.OrdinalIgnoreCase` with `Contains()` or `IndexOf()` for substring matching.

**Rationale**:
- Culture-invariant comparison (consistent across platforms/locales)
- Built-in .NET optimization for case-insensitive operations
- No regex overhead for simple substring search
- MQTT topics are typically ASCII/UTF-8 technical identifiers (not natural language)

**Alternatives Considered**:
- **Regex with RegexOptions.IgnoreCase**: Rejected due to unnecessary overhead for literal substring search
- **ToLower() comparison**: Rejected because creates additional string allocations
- **Culture-specific comparison**: Rejected to ensure consistent behavior across platforms

**Code Pattern**:
```csharp
bool matches = topicName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
```

---

### 3. Search Context State Management

**Decision**: Implement `SearchContext` class in BusinessLogic layer with observable properties for UI binding.

**Rationale**:
- Separation of concerns: search logic separate from UI
- Observable pattern enables automatic UI updates (MVVM)
- Maintains immutable search results list for safe iteration
- Supports wrap-around navigation by tracking current index

**Structure**:
```csharp
public class SearchContext : INotifyPropertyChanged
{
    public string SearchTerm { get; }
    public IReadOnlyList<TopicReference> Matches { get; }
    public int CurrentIndex { get; set; }  // Observable for UI updates
    public int TotalMatches => Matches.Count;

    public void MoveNext() { /* wrap-around logic */ }
    public void MovePrevious() { /* wrap-around logic */ }
}
```

**Alternatives Considered**:
- **Static/singleton manager**: Rejected to improve testability
- **UI-layer state**: Rejected to maintain architectural separation
- **Immutable context with replacement**: Rejected due to unnecessary allocations on navigation

---

### 4. Visual Feedback Indicator Design

**Decision**: Use existing status bar with dynamic text binding, format: "Search: 'term' (match X of Y)"

**Rationale**:
- Leverages existing UI component (no new window chrome)
- Text binding updates automatically via MVVM pattern
- Consistent with established application design language
- Non-intrusive (doesn't obscure content)

**Alternatives Considered**:
- **Floating overlay**: Rejected as visually distracting and requires additional layout management
- **Toast notifications**: Rejected as temporary (need persistent indicator during active search)
- **Dedicated search panel**: Rejected as screen real estate inefficient for simple feedback

**Binding Pattern**:
```xml
<StatusBarItem>
    <TextBlock Text="{Binding SearchStatusText}" />
</StatusBarItem>
```

---

### 5. Message History Navigation State

**Decision**: Extend existing message list view model with `SelectedMessageIndex` property and wrap-around logic.

**Rationale**:
- Reuses existing message collection infrastructure
- Index-based navigation is efficient (O(1) access)
- Wrap-around implemented via modulo arithmetic
- No additional data structures needed

**Wrap-Around Logic**:
```csharp
public void MoveDown()
{
    if (Messages.Count == 0) return;
    SelectedMessageIndex = (SelectedMessageIndex + 1) % Messages.Count;
}

public void MoveUp()
{
    if (Messages.Count == 0) return;
    SelectedMessageIndex = (SelectedMessageIndex - 1 + Messages.Count) % Messages.Count;
}
```

**Alternatives Considered**:
- **Circular linked list**: Rejected as over-engineered for index-based collection
- **Separate navigation cursor**: Rejected to avoid state synchronization issues

---

### 6. Command Palette `/` Prefix Handling

**Decision**: Intercept `/` prefix in command palette command parser, route to topic search handler.

**Rationale**:
- Consistent with existing command-driven architecture
- Parser already handles colon-prefixed commands (`:connect`, `:filter`, etc.)
- `/` provides visual distinction for search vs. other commands
- No changes to existing command infrastructure needed

**Parser Enhancement**:
```csharp
if (commandText.StartsWith("/"))
{
    var searchTerm = commandText.Substring(1);
    return new TopicSearchCommand(searchTerm);
}
```

**Alternatives Considered**:
- **`:search` command**: Rejected as more verbose than `/` prefix
- **Dedicated search textbox**: Rejected to maintain single command palette entry point
- **Regex pattern matching**: Rejected as unnecessarily complex for prefix detection

---

### 7. Focus Detection for Keyboard Shortcut Suppression

**Decision**: Check `Keyboard.FocusedElement` type before handling `n`/`N`/`j`/`k` events.

**Rationale**:
- Reliable cross-platform focus detection via Avalonia/WPF APIs
- Type checking allows precise control (suppress only for TextBox in command palette)
- No custom focus tracking state required

**Implementation Pattern**:
```csharp
private void OnPreviewKeyDown(KeyEventArgs e)
{
    // Suppress shortcuts when command palette TextBox has focus
    if (Keyboard.FocusedElement is TextBox textBox
        && textBox.Name == "CommandPaletteInput")
    {
        return; // Let normal text input occur
    }

    // Handle global shortcuts
    switch (e.Key)
    {
        case Key.N when e.Modifiers == ModifierKeys.Shift:
            NavigateSearchPrevious();
            e.Handled = true;
            break;
        // ... other shortcuts
    }
}
```

**Alternatives Considered**:
- **Manual focus tracking flags**: Rejected to avoid state synchronization bugs
- **Event tunneling only**: Rejected as doesn't provide early interception point

---

## Technology Choices Summary

| Component | Technology | Justification |
|-----------|-----------|---------------|
| Keyboard Events | PreviewKeyDown (WPF/Avalonia) | Cross-platform, global capture with focus detection |
| String Matching | StringComparison.OrdinalIgnoreCase | Culture-invariant, optimized, no allocations |
| State Management | Observable BusinessLogic classes | MVVM pattern, testable, architectural separation |
| Visual Feedback | Status bar text binding | Non-intrusive, reuses existing UI |
| Navigation Logic | Index-based with modulo wrap-around | O(1) efficiency, simple implementation |
| Command Parsing | `/` prefix detection | Consistent with command-driven architecture |
| Focus Detection | Keyboard.FocusedElement API | Reliable, cross-platform, no custom state |

---

## Open Questions (Deferred to Implementation)

1. **Search replacement behavior**: When user enters `/newterm` while active search exists
   - **Recommendation**: Replace current search context (simplest, most predictable)

2. **Empty search handling**: When user enters `/` + Enter with no term
   - **Recommendation**: Show error message "Search term required" in status bar

3. **Search cancellation**: How to clear active search
   - **Recommendation**: Implement `:clearsearch` command or Escape key handler

4. **Dynamic topic updates**: Should search results refresh when topics added/removed?
   - **Recommendation**: Static snapshot (avoids navigation position confusion during active search)

---

## Performance Considerations

**Search Performance**:
- Linear scan O(n) where n = topic count
- Acceptable for typical MQTT deployments (hundreds to thousands of topics)
- Potential optimization: Cache search results, invalidate on topic tree changes

**Navigation Performance**:
- All operations O(1): index arithmetic, array access, property updates
- No allocations during `n`/`N`/`j`/`k` navigation
- UI updates via data binding (batched by framework)

**Memory Impact**:
- SearchContext: ~100 bytes + (8 bytes × match count) for reference list
- No additional message storage (navigation uses existing buffers)
- Status bar text: single string allocation per search/navigation event

---

## Testing Strategy

**Unit Tests**:
- SearchContext: match filtering, index wrapping, edge cases (empty list, single item)
- String matching: case sensitivity, substring detection, special characters
- Navigation state: boundary conditions, wrap-around correctness

**Integration Tests**:
- Keyboard event routing: global shortcuts, command palette suppression
- Search → UI update flow: topic selection, message history refresh
- Cross-component: command palette → search context → topic tree → message view

**Manual Testing Focus**:
- Keyboard shortcut conflicts with OS/framework
- Focus transitions (command palette ↔ main window)
- Visual feedback timing and clarity
- Accessibility (screen readers, keyboard-only navigation)

---

**All NEEDS CLARIFICATION items from Technical Context**: RESOLVED
- No outstanding unknowns for Phase 1 design
