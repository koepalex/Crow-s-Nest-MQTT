# Data Model: Keyboard Navigation Enhancements

**Feature**: 004-improve-keyboard-navigation
**Date**: 2025-10-20

## Entity Definitions

### 1. SearchContext

**Purpose**: Manages active topic search session state, including search term, matching topics, and current navigation position.

**Location**: `src/BusinessLogic/Navigation/SearchContext.cs`

**Properties**:

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `SearchTerm` | `string` | Required, non-null | The search term entered by user (case-preserved) |
| `Matches` | `IReadOnlyList<TopicReference>` | Never null (empty list if no matches) | Immutable list of topics matching search term |
| `CurrentIndex` | `int` | `0 <= CurrentIndex < Matches.Count` or `-1` if empty | Zero-based index of currently selected match |
| `TotalMatches` | `int` | Computed: `Matches.Count` | Total number of matching topics |
| `HasMatches` | `bool` | Computed: `Matches.Count > 0` | Whether any topics matched the search |
| `IsActive` | `bool` | Computed: `HasMatches && CurrentIndex >= 0` | Whether search context is actively navigable |

**Behaviors**:
- `MoveNext()`: Increment `CurrentIndex` with wrap-around (if at last, go to 0)
- `MovePrevious()`: Decrement `CurrentIndex` with wrap-around (if at 0, go to last)
- `GetCurrentMatch()`: Returns `TopicReference` at `CurrentIndex`, or `null` if no matches
- `Clear()`: Resets context to empty state

**Invariants**:
- If `Matches.Count == 0`, then `CurrentIndex == -1`
- If `Matches.Count > 0`, then `0 <= CurrentIndex < Matches.Count`
- `Matches` list is immutable after construction (replace entire context for new search)

**Observable Properties** (for MVVM binding):
- `CurrentIndex` (triggers UI update when match selection changes)
- Raises `PropertyChanged` for: `CurrentIndex`, `IsActive`

---

### 2. TopicReference

**Purpose**: Lightweight reference to a topic in the MQTT topic tree, used for search results without duplicating full topic data.

**Location**: `src/BusinessLogic/Navigation/TopicReference.cs`

**Properties**:

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `TopicPath` | `string` | Required, non-null, non-empty | Full MQTT topic path (e.g., "sensor/temperature/bedroom") |
| `DisplayName` | `string` | Required, non-null | Human-readable topic name (may be same as TopicPath) |
| `TopicId` | `Guid` or `int` | Required, unique | Internal identifier linking to full Topic entity |

**Behaviors**:
- Equality based on `TopicId` (two references to same topic are equal)
- String representation returns `TopicPath`

**Relationships**:
- References → `Topic` entity in existing topic tree structure
- Used by → `SearchContext.Matches` collection

---

### 3. MessageNavigationState

**Purpose**: Tracks current position in message history view for keyboard navigation.

**Location**: `src/BusinessLogic/Navigation/MessageNavigationState.cs`

**Properties**:

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `Messages` | `IReadOnlyList<Message>` | Never null (may be empty) | Current topic's message list (read-only reference) |
| `SelectedIndex` | `int` | `-1` if no messages, else `0 <= SelectedIndex < Messages.Count` | Index of currently selected message |
| `HasMessages` | `bool` | Computed: `Messages.Count > 0` | Whether any messages exist for navigation |

**Behaviors**:
- `MoveDown()`: Increment `SelectedIndex` with wrap-around
- `MoveUp()`: Decrement `SelectedIndex` with wrap-around
- `GetSelectedMessage()`: Returns `Message` at `SelectedIndex`, or `null` if no messages
- `UpdateMessages(IReadOnlyList<Message>)`: Replace message list when topic changes

**Invariants**:
- If `Messages.Count == 0`, then `SelectedIndex == -1`
- If `Messages.Count > 0`, then `0 <= SelectedIndex < Messages.Count`

**Observable Properties** (for MVVM binding):
- `SelectedIndex` (triggers UI update when message selection changes)

---

### 4. KeyboardNavigationService

**Purpose**: Coordinates keyboard event handling, focus detection, and navigation command dispatch.

**Location**: `src/UI/Services/KeyboardNavigationService.cs`

**Properties**:

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `IsCommandPaletteActive` | `bool` | Read-only | Whether command palette text input currently has focus |
| `ActiveSearchContext` | `SearchContext?` | Nullable | Current search session, or null if no active search |
| `MessageNavigation` | `MessageNavigationState` | Never null | Message history navigation state |

**Behaviors**:
- `HandleKeyPress(Key, ModifierKeys)`: Routes keyboard events to appropriate navigation handler
- `SuppressShortcut(Key)`: Determines if shortcut should be suppressed (based on focus)
- `ExecuteSearchCommand(string searchTerm)`: Initiates topic search
- `NavigateSearchNext()`: Advance to next search match
- `NavigateSearchPrevious()`: Move to previous search match
- `NavigateMessageDown()`: Move to next message in history
- `NavigateMessageUp()`: Move to previous message in history

**Dependencies**:
- Uses → `SearchContext` for topic search navigation
- Uses → `MessageNavigationState` for message history navigation
- Notifies → `TopicSelectionService` when search match changes
- Notifies → `MessageViewService` when message selection changes

---

### 5. SearchStatusViewModel

**Purpose**: View model providing formatted search status text for UI display (status bar).

**Location**: `src/UI/ViewModels/SearchStatusViewModel.cs`

**Properties**:

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `StatusText` | `string` | Never null (empty if no search) | Formatted status message for display |
| `IsVisible` | `bool` | Computed | Whether status should be shown |

**Behaviors**:
- `UpdateFromContext(SearchContext)`: Refresh status text based on search context
- Formats status as:
  - No matches: `"No topics matching 'searchTerm'"`
  - With matches (not navigating): `"Search: 'searchTerm' (X matches)"`
  - Navigating: `"Search: 'searchTerm' (match Y of X)"`

**Observable Properties**:
- `StatusText` (auto-updates UI when search context changes)
- `IsVisible` (controls status bar visibility)

---

## Entity Relationships

```
KeyboardNavigationService
├── has → ActiveSearchContext (0..1 SearchContext)
├── has → MessageNavigation (1 MessageNavigationState)
└── uses → TopicSelectionService (existing)
    └── uses → MessageViewService (existing)

SearchContext
├── contains → Matches (0..* TopicReference)
└── references → CurrentMatch (0..1 TopicReference)

TopicReference
└── links to → Topic (existing entity)

MessageNavigationState
├── references → Messages (0..* Message from existing entity)
└── references → SelectedMessage (0..1 Message)

SearchStatusViewModel
└── observes → SearchContext (reads for formatting)
```

---

## State Transitions

### Search Context Lifecycle

```
[No Search]
    ↓ (user enters /searchterm)
[Searching]
    ↓ (matches found)
[Active with Matches] ←→ (n/N navigation)
    ↓ (new search or clear)
[No Search]

Alternative path:
[Searching]
    ↓ (no matches)
[Active without Matches]
    ↓ (show error feedback)
[No Search]
```

**State Definitions**:
- **No Search**: `ActiveSearchContext == null`
- **Searching**: Transient (during match filtering)
- **Active with Matches**: `ActiveSearchContext != null && ActiveSearchContext.HasMatches`
- **Active without Matches**: `ActiveSearchContext != null && !ActiveSearchContext.HasMatches`

### Message Navigation State

```
[No Messages] (SelectedIndex = -1)
    ↓ (topic selected with messages)
[First Message Selected] (SelectedIndex = 0)
    ↓ (j key)
[Message N Selected]
    ↓ (j at last message)
[First Message Selected] (wrap-around)

    ↓ (k key)
[Last Message Selected] (wrap-around)
```

---

## Validation Rules

### SearchContext
- **R1**: `SearchTerm` cannot be null or whitespace when context is created
- **R2**: `Matches` list must be immutable after construction
- **R3**: `CurrentIndex` must update atomically with observable notification

### TopicReference
- **R4**: `TopicPath` must match format of existing Topic entity paths
- **R5**: `TopicId` must reference existing Topic (referential integrity)

### MessageNavigationState
- **R6**: `SelectedIndex` must be `-1` when `Messages.Count == 0`
- **R7**: Wrap-around arithmetic must handle empty list gracefully (no operation)

### KeyboardNavigationService
- **R8**: Search navigation commands (`n`/`N`) must no-op if `ActiveSearchContext == null`
- **R9**: Message navigation commands (`j`/`k`) must no-op if `MessageNavigation.HasMessages == false`
- **R10**: Focus detection must be evaluated synchronously before handling shortcut

---

## Data Flow Diagrams

### Topic Search Flow

```
User types /searchterm → CommandPalette
    ↓
CommandParser.Parse()
    ↓
KeyboardNavigationService.ExecuteSearchCommand(searchTerm)
    ↓
BusinessLogic: Filter topics (case-insensitive substring match)
    ↓
Create SearchContext(searchTerm, matches)
    ↓
Set ActiveSearchContext (observable property)
    ↓
SearchStatusViewModel observes change
    ↓
UI: Status bar updates via binding
    ↓
If matches > 0: Auto-select first match
    ↓
TopicSelectionService.SelectTopic(matches[0])
    ↓
MessageViewService.LoadMessagesForTopic()
    ↓
UI: Topic tree highlights, message history displays
```

### Search Navigation Flow (`n` key)

```
User presses 'n' → Window.PreviewKeyDown
    ↓
KeyboardNavigationService.HandleKeyPress(Key.N)
    ↓
Check: !IsCommandPaletteActive? (yes)
    ↓
Check: ActiveSearchContext != null? (yes)
    ↓
ActiveSearchContext.MoveNext()
    ↓
CurrentIndex wraps: (index + 1) % TotalMatches
    ↓
PropertyChanged event fired
    ↓
TopicSelectionService.SelectTopic(newCurrentMatch)
    ↓
MessageViewService.LoadMessagesForTopic()
    ↓
UI updates: Topic selection + message history + status bar position
```

### Message Navigation Flow (`j` key)

```
User presses 'j' → Window.PreviewKeyDown
    ↓
KeyboardNavigationService.HandleKeyPress(Key.J)
    ↓
Check: !IsCommandPaletteActive? (yes)
    ↓
MessageNavigation.MoveDown()
    ↓
SelectedIndex wraps: (index + 1) % Messages.Count
    ↓
PropertyChanged event fired
    ↓
MessageView scrolls to selected message
    ↓
UI highlights selected message row
```

---

## Performance Characteristics

| Operation | Time Complexity | Space Complexity | Notes |
|-----------|-----------------|------------------|-------|
| Topic search (filtering) | O(n × m) | O(k) | n=topics, m=avg topic path length, k=matches |
| Search navigation (n/N) | O(1) | O(1) | Index arithmetic + array access |
| Message navigation (j/k) | O(1) | O(1) | Index arithmetic + array access |
| SearchContext creation | O(n) | O(k) | One-time cost during search initiation |
| Status text formatting | O(1) | O(1) | String interpolation (single allocation) |

**Memory Impact**:
- SearchContext: ~100 bytes + (8 bytes per match for TopicReference pointers)
- MessageNavigationState: 16 bytes (2 integers) + reference to existing message list
- No message duplication (navigation uses existing buffers)

---

## Testing Boundaries

### Unit Test Targets
- **SearchContext**: Match filtering correctness, wrap-around at boundaries, empty search handling
- **MessageNavigationState**: Wrap-around logic, empty message list safety, index bounds
- **TopicReference**: Equality semantics, string representation
- **SearchStatusViewModel**: Text formatting for all state combinations

### Integration Test Targets
- **KeyboardNavigationService**: Event routing, focus detection, cross-component coordination
- **Search → UI flow**: Command palette → search → topic selection → message display
- **Navigation → UI flow**: Keyboard event → state update → UI refresh

### Edge Cases to Cover
1. Search with no matches → status bar shows "No topics matching '...'"
2. Single match navigation → `n`/`N` wrap to same item (no-op visually)
3. Empty message history → `j`/`k` no-op silently
4. Rapid keyboard input → debouncing not required (operations are O(1))
5. Search during active search → replace context (memory cleanup of old context)
6. Topic added/removed during search → static snapshot (deferred for MVP)

---

**Design Completion**: All entities from spec.md Key Entities section are defined with properties, behaviors, and relationships.
