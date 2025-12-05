# Tasks: Keyboard Navigation Enhancements

**Feature**: 004-improve-keyboard-navigation
**Input**: Design documents from `S:\rc\_priv\Crow-s-Nest-MQTT\specs\004-improve-keyboard-navigation\`
**Prerequisites**: ✅ plan.md, ✅ research.md, ✅ data-model.md, ✅ contracts/, ✅ quickstart.md

## Execution Flow (main)
```
1. Load plan.md from feature directory ✅
   → Tech stack: C# (.NET), WPF/Avalonia, MQTTnet, Serilog
   → Structure: src/UI/, src/BusinessLogic/, src/Utils/
2. Load design documents ✅
   → data-model.md: 5 entities extracted
   → contracts/: 3 contract files found
   → quickstart.md: 10 test scenarios identified
3. Generate tasks by category ✅
4. Apply TDD ordering: Tests before implementation ✅
5. Mark parallel tasks [P] for independent files ✅
6. Number tasks sequentially (T001-T027) ✅
7. SUCCESS: Tasks ready for execution
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions
- All paths are absolute from repository root

## Path Conventions
- **BusinessLogic**: `src/BusinessLogic/Navigation/`
- **UI Services**: `src/UI/Services/`
- **UI ViewModels**: `src/UI/ViewModels/`
- **Contract Tests**: `tests/contract/Navigation/`
- **Unit Tests**: `tests/UnitTests/Navigation/`
- **Integration Tests**: `tests/integration/Navigation/`

---

## Phase 3.1: Setup & Prerequisites

### T001: Verify project structure exists
**Description**: Verify that the existing Crow's Nest MQTT solution has the required project structure (src/UI/, src/BusinessLogic/, src/Utils/, tests/).
**File Path**: Repository root inspection
**Validation**: All required directories exist
**Dependencies**: None
**Estimated Time**: 5 minutes

### T002: Create Navigation namespace directories
**Description**: Create new directory structure for keyboard navigation components:
- `src/BusinessLogic/Navigation/` (for SearchContext, TopicReference, MessageNavigationState)
- `src/UI/Services/` (if not exists - for KeyboardNavigationService)
- `src/UI/ViewModels/` (if not exists - for SearchStatusViewModel)
- `tests/contract/Navigation/`
- `tests/UnitTests/Navigation/`
- `tests/integration/Navigation/`

**File Path**: Repository root
**Dependencies**: T001
**Estimated Time**: 5 minutes

### T003 [P]: Add required NuGet packages (if missing)
**Description**: Verify or add required NuGet packages to projects:
- `System.ComponentModel` (for INotifyPropertyChanged)
- Test framework: `xUnit` or `NUnit` (per CLAUDE.md)
- Test framework test runner packages

**File Path**: `.csproj` files in src/ and tests/
**Dependencies**: T001
**Estimated Time**: 10 minutes

---

## Phase 3.2: Contract Tests (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### T004 [P]: Write failing contract tests for ITopicSearchService
**Description**: Create comprehensive contract tests for `ITopicSearchService` interface based on `specs/004-improve-keyboard-navigation/contracts/ITopicSearchService.cs`:
- Test `ExecuteSearch()` with various search terms (case sensitivity, substring matching)
- Test empty search term (should throw ArgumentException)
- Test search with no matches (returns empty SearchContext)
- Test search with multiple matches (first auto-selected)
- Test `ClearSearch()` behavior
- Test `ActiveSearchContext` property updates

**File Path**: `tests/contract/Navigation/ITopicSearchServiceContractTests.cs`
**Expected Result**: All tests FAIL (no implementation yet)
**Dependencies**: T002
**Estimated Time**: 30 minutes

### T005 [P]: Write failing contract tests for IKeyboardNavigationService
**Description**: Create comprehensive contract tests for `IKeyboardNavigationService` interface based on `specs/004-improve-keyboard-navigation/contracts/IKeyboardNavigationService.cs`:
- Test `HandleKeyPress()` with 'n', 'N', 'j', 'k' keys
- Test `ShouldSuppressShortcuts()` returns true when command palette focused
- Test `NavigateSearchNext()` wrap-around behavior
- Test `NavigateSearchPrevious()` wrap-around behavior
- Test `NavigateMessageDown()` wrap-around behavior
- Test `NavigateMessageUp()` wrap-around behavior
- Test no-op when no search active (for n/N) or no messages (for j/k)

**File Path**: `tests/contract/Navigation/IKeyboardNavigationServiceContractTests.cs`
**Expected Result**: All tests FAIL (no implementation yet)
**Dependencies**: T002
**Estimated Time**: 45 minutes

### T006 [P]: Write failing contract tests for ISearchStatusProvider
**Description**: Create comprehensive contract tests for `ISearchStatusProvider` interface based on `specs/004-improve-keyboard-navigation/contracts/ISearchStatusProvider.cs`:
- Test `StatusText` formatting for no search
- Test `StatusText` for no matches: "No topics matching '[term]'"
- Test `StatusText` for matches: "Search: '[term]' (X matches)"
- Test `StatusText` during navigation: "Search: '[term]' (match Y of X)"
- Test `IsVisible` property (true when active search, false otherwise)
- Test `UpdateFromContext()` triggers PropertyChanged events

**File Path**: `tests/contract/Navigation/ISearchStatusProviderContractTests.cs`
**Expected Result**: All tests FAIL (no implementation yet)
**Dependencies**: T002
**Estimated Time**: 20 minutes

---

## Phase 3.3: Entity/Model Creation (ONLY after tests are failing)

### T007 [P]: Implement TopicReference class
**Description**: Implement `TopicReference` class per `data-model.md` specifications:
- Properties: TopicPath (string), DisplayName (string), TopicId (Guid)
- Equality based on TopicId
- String representation returns TopicPath
- Immutable class (all properties read-only)

**File Path**: `src/BusinessLogic/Navigation/TopicReference.cs`
**Dependencies**: T004 (tests must be failing)
**Estimated Time**: 15 minutes

### T008 [P]: Implement SearchContext class
**Description**: Implement `SearchContext` class per `data-model.md` specifications:
- Properties: SearchTerm, Matches (IReadOnlyList<TopicReference>), CurrentIndex, TotalMatches, HasMatches, IsActive
- Implement INotifyPropertyChanged for CurrentIndex
- Methods: MoveNext() (wrap-around), MovePrevious() (wrap-around), GetCurrentMatch(), Clear()
- Validate invariants: empty matches → CurrentIndex = -1
- Constructor validates SearchTerm not null/whitespace

**File Path**: `src/BusinessLogic/Navigation/SearchContext.cs`
**Dependencies**: T004, T007 (TopicReference)
**Estimated Time**: 30 minutes

### T009 [P]: Implement MessageNavigationState class
**Description**: Implement `MessageNavigationState` class per `data-model.md` specifications:
- Properties: Messages (IReadOnlyList<Message>), SelectedIndex, HasMessages
- Implement INotifyPropertyChanged for SelectedIndex
- Methods: MoveDown() (wrap-around), MoveUp() (wrap-around), GetSelectedMessage(), UpdateMessages()
- Wrap-around logic: `(index + 1) % count` for down, `(index - 1 + count) % count` for up
- Handle empty message list gracefully (SelectedIndex = -1)

**File Path**: `src/BusinessLogic/Navigation/MessageNavigationState.cs`
**Dependencies**: T005 (tests must be failing)
**Estimated Time**: 25 minutes

---

## Phase 3.4: Service Implementation

### T010: Implement TopicSearchService
**Description**: Implement `ITopicSearchService` service class:
- `ExecuteSearch(string searchTerm)`: Filter topics using `StringComparison.OrdinalIgnoreCase`
- Create SearchContext with matches, set CurrentIndex = 0 if matches exist
- Store as ActiveSearchContext property
- `ClearSearch()`: Set ActiveSearchContext to null
- Inject topic repository/provider to get list of topics for filtering
- Constructor validates dependencies

**File Path**: `src/BusinessLogic/Navigation/TopicSearchService.cs`
**Dependencies**: T004 (contract tests), T007 (TopicReference), T008 (SearchContext)
**Estimated Time**: 40 minutes

### T011: Implement KeyboardNavigationService
**Description**: Implement `IKeyboardNavigationService` service class:
- `HandleKeyPress(Key, ModifierKeys)`: Route to appropriate navigation method
- `ShouldSuppressShortcuts()`: Check if `Keyboard.FocusedElement` is command palette TextBox
- `NavigateSearchNext()`: Call ActiveSearchContext.MoveNext(), update topic selection
- `NavigateSearchPrevious()`: Call ActiveSearchContext.MovePrevious(), update topic selection
- `NavigateMessageDown()`: Call MessageNavigation.MoveDown(), update message selection
- `NavigateMessageUp()`: Call MessageNavigation.MoveUp(), update message selection
- Properties: ActiveSearchContext, MessageNavigation
- Inject topic selection service and message view service

**File Path**: `src/UI/Services/KeyboardNavigationService.cs`
**Dependencies**: T005 (contract tests), T008 (SearchContext), T009 (MessageNavigationState)
**Estimated Time**: 60 minutes

### T012: Implement SearchStatusViewModel
**Description**: Implement `ISearchStatusProvider` view model class:
- Properties: StatusText (string), IsVisible (bool)
- Implement INotifyPropertyChanged
- `UpdateFromContext(SearchContext? context)`: Format status text based on context state
  - null context → empty string, IsVisible = false
  - No matches → "No topics matching '[term]'"
  - Has matches, CurrentIndex = 0 → "Search: '[term]' (X matches)"
  - Navigating → "Search: '[term]' (match Y of X)" where Y = CurrentIndex + 1
- Subscribe to SearchContext.PropertyChanged to auto-update

**File Path**: `src/UI/ViewModels/SearchStatusViewModel.cs`
**Dependencies**: T006 (contract tests), T008 (SearchContext)
**Estimated Time**: 30 minutes

---

## Phase 3.5: UI Integration

### T013: Extend command palette parser for /[term] syntax
**Description**: Modify existing command palette command parser to handle `/` prefix:
- Detect if command text starts with `/`
- Extract search term (everything after `/`)
- Create/dispatch TopicSearchCommand with search term
- Wire to TopicSearchService.ExecuteSearch()
- Ensure existing colon-prefixed commands (`:connect`, `:filter`, etc.) still work

**File Path**: Existing command parser file (likely `src/UI/Commands/CommandParser.cs` or similar)
**Dependencies**: T010 (TopicSearchService)
**Estimated Time**: 30 minutes

### T014: Wire keyboard event routing in main window
**Description**: Add PreviewKeyDown event handler to main application window:
- Attach KeyboardNavigationService.HandleKeyPress() to window PreviewKeyDown
- Check ShouldSuppressShortcuts() before handling keys
- Set `e.Handled = true` when shortcut processed to prevent text input
- Handle keys: 'n', 'N' (with Shift modifier), 'j', 'k'
- Ensure events don't interfere with other UI interactions

**File Path**: Main window XAML and code-behind (likely `src/UI/MainWindow.xaml.cs`)
**Dependencies**: T011 (KeyboardNavigationService)
**Estimated Time**: 40 minutes

### T015: Add search status indicator to status bar
**Description**: Add search status display to existing status bar:
- Add TextBlock bound to SearchStatusViewModel.StatusText
- Bind Visibility to SearchStatusViewModel.IsVisible
- Position consistently in status bar (left or right side)
- Ensure status bar exists (create if needed)
- Wire SearchStatusViewModel to main window DataContext or dedicated property

**File Path**: Status bar XAML (likely `src/UI/MainWindow.xaml` or `src/UI/Views/StatusBar.xaml`)
**Dependencies**: T012 (SearchStatusViewModel)
**Estimated Time**: 20 minutes

### T016: Implement focus detection for command palette
**Description**: Add focus tracking logic to command palette TextBox:
- Name TextBox element (e.g., "CommandPaletteInput")
- Ensure KeyboardNavigationService can access focused element
- Test that `Keyboard.FocusedElement` correctly identifies command palette
- Verify shortcuts suppressed when typing in command palette

**File Path**: Command palette XAML and code-behind
**Dependencies**: T011 (KeyboardNavigationService), T014 (keyboard event routing)
**Estimated Time**: 20 minutes

---

## Phase 3.6: Integration Tests

### T017 [P]: Integration test - Topic search via command palette
**Description**: Implement integration test for quickstart scenario 1:
- Set up test broker with multiple topics (sensor/temperature, sensor/humidity, device/status)
- Simulate user typing `/sensor` in command palette
- Assert first topic containing "sensor" is selected
- Assert message history view updates
- Assert status bar shows "Search: 'sensor' (X matches)"

**File Path**: `tests/integration/Navigation/TopicSearchIntegrationTests.cs`
**Dependencies**: T013, T015 (UI integration complete)
**Estimated Time**: 45 minutes

### T018 [P]: Integration test - Navigate forward through search results
**Description**: Implement integration test for quickstart scenario 2:
- Perform search with multiple matches
- Simulate `n` key press
- Assert second topic selected
- Assert message history updates
- Assert status bar shows position: "Search: 'sensor' (match 2 of X)"
- Test wrap-around: press `n` at last match → wraps to first

**File Path**: `tests/integration/Navigation/SearchNavigationForwardTests.cs`
**Dependencies**: T014 (keyboard routing), T015 (status bar)
**Estimated Time**: 30 minutes

### T019 [P]: Integration test - Navigate backward through search results
**Description**: Implement integration test for quickstart scenario 3:
- Perform search (first match selected)
- Simulate `N` (Shift+n) key press
- Assert wraps to last match
- Assert status bar shows "Search: 'sensor' (match X of X)"
- Press `N` again, assert moves to second-to-last

**File Path**: `tests/integration/Navigation/SearchNavigationBackwardTests.cs`
**Dependencies**: T014, T015
**Estimated Time**: 25 minutes

### T020 [P]: Integration test - Message navigation with j/k keys
**Description**: Implement integration tests for quickstart scenarios 4-5:
- Select topic with multiple messages
- Test `j` key: moves down, wraps at bottom to top
- Test `k` key: moves up, wraps at top to bottom
- Assert visual highlight updates
- Test no-op when topic has no messages

**File Path**: `tests/integration/Navigation/MessageNavigationTests.cs`
**Dependencies**: T014
**Estimated Time**: 40 minutes

### T021 [P]: Integration test - Keyboard shortcut suppression
**Description**: Implement integration test for quickstart scenario 6:
- Open command palette
- Simulate typing characters 'n', 'j', 'k'
- Assert characters appear in command palette input
- Assert no navigation occurs
- Close command palette, press `n` → assert navigation works

**File Path**: `tests/integration/Navigation/ShortcutSuppressionTests.cs`
**Dependencies**: T016 (focus detection)
**Estimated Time**: 25 minutes

### T022 [P]: Integration test - Search with no matches
**Description**: Implement integration test for quickstart scenario 7:
- Perform search with non-existent term (e.g., `/nonexistenttopicxyz123`)
- Assert status bar shows: "No topics matching 'nonexistenttopicxyz123'"
- Assert current topic selection unchanged
- Assert no navigation occurs

**File Path**: `tests/integration/Navigation/NoMatchesFeedbackTests.cs`
**Dependencies**: T015, T010
**Estimated Time**: 20 minutes

### T023 [P]: Integration test - Rapid sequential navigation
**Description**: Implement integration test for quickstart scenario 8 (performance):
- Perform search with 5+ matches
- Simulate rapid `n` key presses (10 times in quick succession)
- Assert all inputs processed
- Assert UI remains responsive
- Measure response time <100ms per key press (95th percentile)
- Test rapid `j` key presses (20 times)

**File Path**: `tests/integration/Navigation/RapidNavigationPerformanceTests.cs`
**Dependencies**: T014
**Estimated Time**: 35 minutes

### T024 [P]: Integration test - Cross-component state sync
**Description**: Implement integration test for quickstart scenario 9:
- Perform search, navigate to second match with `n`
- Manually click different topic in tree (outside search results)
- Assert message history updates for clicked topic
- Press `n` again → assert returns to search results (third match)
- Assert `j`/`k` work on manually selected topic

**File Path**: `tests/integration/Navigation/CrossComponentSyncTests.cs`
**Dependencies**: T014, T010
**Estimated Time**: 30 minutes

### T025 [P]: Integration test - Visual feedback lifecycle
**Description**: Implement integration test for quickstart scenario 10:
- Verify status bar visible
- Perform search `/sensor` → assert indicator shows "Search: 'sensor' (X matches)"
- Press `n` → assert updates to "Search: 'sensor' (match 2 of X)"
- Perform new search `/device` → assert old search cleared, new indicator shown
- (If clear command implemented) Execute clear → assert indicator hidden

**File Path**: `tests/integration/Navigation/VisualFeedbackLifecycleTests.cs`
**Dependencies**: T015, T010
**Estimated Time**: 25 minutes

---

## Phase 3.7: Unit Tests & Polish

### T026 [P]: Unit tests for SearchContext edge cases
**Description**: Write comprehensive unit tests for SearchContext class:
- Test single match navigation (n/N wrap to same item)
- Test empty match list (CurrentIndex = -1, no-op on MoveNext/MovePrevious)
- Test boundary wrapping correctness
- Test PropertyChanged events fire correctly
- Test Clear() resets state

**File Path**: `tests/UnitTests/Navigation/SearchContextTests.cs`
**Dependencies**: T008 (SearchContext implementation)
**Estimated Time**: 30 minutes

### T027 [P]: Unit tests for MessageNavigationState edge cases
**Description**: Write comprehensive unit tests for MessageNavigationState class:
- Test empty message list (SelectedIndex = -1, no-op on MoveDown/MoveUp)
- Test single message navigation
- Test wrap-around modulo arithmetic correctness
- Test UpdateMessages() updates SelectedIndex appropriately
- Test PropertyChanged events

**File Path**: `tests/UnitTests/Navigation/MessageNavigationStateTests.cs`
**Dependencies**: T009 (MessageNavigationState implementation)
**Estimated Time**: 25 minutes

---

## Dependencies Graph

```
Setup Phase (T001-T003)
    ↓
Contract Tests Phase [PARALLEL] (T004-T006)
    ↓
Entity Implementation Phase [PARALLEL] (T007-T009)
    ↓
Service Implementation Phase [SEQUENTIAL] (T010-T012)
    ↓
UI Integration Phase [SEQUENTIAL] (T013-T016)
    ↓
Integration Tests Phase [PARALLEL] (T017-T025)
    ↓
Unit Tests & Polish Phase [PARALLEL] (T026-T027)
```

**Critical Path**: T001 → T002 → T004 → T008 → T010 → T013 → T017 (est. 4 hours)

---

## Parallel Execution Examples

### Batch 1: Contract Tests (after T002 complete)
```bash
# Run these 3 tasks concurrently:
Task 1: "Write failing contract tests for ITopicSearchService in tests/contract/Navigation/ITopicSearchServiceContractTests.cs"
Task 2: "Write failing contract tests for IKeyboardNavigationService in tests/contract/Navigation/IKeyboardNavigationServiceContractTests.cs"
Task 3: "Write failing contract tests for ISearchStatusProvider in tests/contract/Navigation/ISearchStatusProviderContractTests.cs"
```

### Batch 2: Entity Implementations (after T004-T006 complete)
```bash
# Run these 3 tasks concurrently:
Task 1: "Implement TopicReference class in src/BusinessLogic/Navigation/TopicReference.cs per data-model.md"
Task 2: "Implement SearchContext class in src/BusinessLogic/Navigation/SearchContext.cs per data-model.md"
Task 3: "Implement MessageNavigationState class in src/BusinessLogic/Navigation/MessageNavigationState.cs per data-model.md"
```

### Batch 3: Integration Tests (after T013-T016 complete)
```bash
# Run all 9 integration test tasks concurrently (T017-T025):
Task 1: "Integration test - Topic search via command palette in tests/integration/Navigation/TopicSearchIntegrationTests.cs"
Task 2: "Integration test - Navigate forward through search results in tests/integration/Navigation/SearchNavigationForwardTests.cs"
Task 3: "Integration test - Navigate backward through search results in tests/integration/Navigation/SearchNavigationBackwardTests.cs"
Task 4: "Integration test - Message navigation with j/k keys in tests/integration/Navigation/MessageNavigationTests.cs"
Task 5: "Integration test - Keyboard shortcut suppression in tests/integration/Navigation/ShortcutSuppressionTests.cs"
Task 6: "Integration test - Search with no matches in tests/integration/Navigation/NoMatchesFeedbackTests.cs"
Task 7: "Integration test - Rapid sequential navigation in tests/integration/Navigation/RapidNavigationPerformanceTests.cs"
Task 8: "Integration test - Cross-component state sync in tests/integration/Navigation/CrossComponentSyncTests.cs"
Task 9: "Integration test - Visual feedback lifecycle in tests/integration/Navigation/VisualFeedbackLifecycleTests.cs"
```

### Batch 4: Unit Tests & Polish (after integration tests pass)
```bash
# Run these 2 tasks concurrently:
Task 1: "Unit tests for SearchContext edge cases in tests/UnitTests/Navigation/SearchContextTests.cs"
Task 2: "Unit tests for MessageNavigationState edge cases in tests/UnitTests/Navigation/MessageNavigationStateTests.cs"
```

---

## Task Summary

**Total Tasks**: 27
- **Setup**: 3 tasks (T001-T003)
- **Contract Tests**: 3 tasks [P] (T004-T006)
- **Entity Implementation**: 3 tasks [P] (T007-T009)
- **Service Implementation**: 3 tasks (T010-T012)
- **UI Integration**: 4 tasks (T013-T016)
- **Integration Tests**: 9 tasks [P] (T017-T025)
- **Unit Tests & Polish**: 2 tasks [P] (T026-T027)

**Parallelizable Tasks**: 17 tasks marked [P]
**Sequential Tasks**: 10 tasks with dependencies

**Estimated Total Time**: 12-14 hours (with parallelization: 6-8 hours)

---

## Validation Checklist
*GATE: All items must be checked before marking tasks complete*

- [x] All contracts (3) have corresponding test tasks (T004-T006)
- [x] All entities (5) have model/implementation tasks (T007-T012)
- [x] All integration scenarios (10) from quickstart.md have test tasks (T017-T025)
- [x] All tests come before implementation (T004-T006 before T007-T012)
- [x] Parallel tasks [P] operate on different files (verified)
- [x] Each task specifies exact file path (verified)
- [x] No [P] task modifies same file as another [P] task (verified)
- [x] TDD order enforced: Tests fail first, then implementation (verified)
- [x] Constitutional compliance: UI → BusinessLogic → Utils flow (verified)

---

## Notes

- **TDD Critical**: Contract tests (T004-T006) MUST FAIL before starting entity implementation
- **Commit Strategy**: Commit after each task completion for clean history
- **Cross-Platform**: Test on Windows, Linux, macOS (T023 performance tests especially)
- **Code Review**: Focus on keyboard event handling (T014), focus detection (T016), and wrap-around logic (T008, T009)
- **Performance**: T023 validates <100ms response time requirement from constitution

---

**Generated**: 2025-10-20
**Based on**: Constitution v1.0.0, Plan v1.0, Data Model v1.0, Contracts v1.0, Quickstart v1.0
