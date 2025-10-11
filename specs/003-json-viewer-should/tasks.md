# Tasks: JSON Viewer Default Expansion

**Feature**: 003-json-viewer-should
**Branch**: `003-json-viewer-should`
**Input**: Design documents from `S:\rc\_priv\Crow-s-Nest-MQTT\specs\003-json-viewer-should\`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/JsonTreeBuilder.contract.md

## Execution Summary

This task list implements automatic JSON expansion (up to 5 levels deep) for all JSON displays in Crow's NestMQTT. Tasks follow TDD methodology: write failing tests first, then implement to make them pass.

**Key Entities**: JsonTreeNode (model), JsonTreeBuilder (utility), JsonViewerViewModel (UI layer)
**Tech Stack**: C#/.NET, WPF/Avalonia, System.Text.Json, xUnit/NUnit
**Architecture**: UI → BusinessLogic → Utils (no circular dependencies)

## Phase 3.1: Setup

- [ ] **T001** Verify existing project structure supports new Utils classes
  - **Path**: `src/Utils/`
  - **Action**: Confirm Utils/ directory exists and is referenced by UI project
  - **Output**: Project structure validated, ready for JsonTreeBuilder class
  - **Dependencies**: None
  - **Parallel**: N/A

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation in Phase 3.3**

- [ ] **T002 [P]** Write JsonTreeNode model unit tests
  - **Path**: `tests/UnitTests/Utils/JsonTreeNodeTests.cs`
  - **Action**: Create test file with tests for:
    - Constructor initializes properties correctly
    - IsExpanded follows depth <= 5 rule
    - IsExpandable set correctly for Object/Array vs scalar types
    - Parent-child relationships maintained
    - ObservableCollection<JsonTreeNode> for Children
  - **Expected**: All tests FAIL (class doesn't exist yet)
  - **Dependencies**: None
  - **Parallel**: Yes [P] - Independent test file

- [ ] **T003 [P]** Write JsonTreeBuilder.BuildTree() unit tests
  - **Path**: `tests/UnitTests/Utils/JsonTreeBuilderTests.cs`
  - **Action**: Create test file implementing contract tests from contracts/JsonTreeBuilder.contract.md:
    - `BuildTree_FlatJson_ReturnsRootOnly()` - 1 level JSON, no children
    - `BuildTree_3LevelNested_AllExpanded()` - All 3 levels have IsExpanded = true
    - `BuildTree_5LevelNested_AllExpanded()` - All 5 levels have IsExpanded = true
    - `BuildTree_6LevelNested_OnlyFirst5Expanded()` - Levels 1-5 expanded, level 6 collapsed
    - `BuildTree_7LevelNested_Depth6And7Collapsed()` - Verify lazy-loading beyond depth 5
    - `BuildTree_MixedTypes_CorrectExpansion()` - Arrays and objects both expand correctly
    - `BuildTree_EmptyObjectArray_NotExpandable()` - Empty containers not expandable
  - **Expected**: All tests FAIL (JsonTreeBuilder doesn't exist yet)
  - **Dependencies**: None
  - **Parallel**: Yes [P] - Independent test file

- [ ] **T004 [P]** Write JsonViewerViewModel unit tests
  - **Path**: `tests/UnitTests/UI/ViewModels/JsonViewerViewModelTests.cs`
  - **Action**: Create test file with tests for:
    - `LoadJsonAsync_ValidJson_BuildsTree()` - RootNode populated correctly
    - `LoadJsonAsync_MalformedJson_SetsErrorMessage()` - Graceful error handling
    - `RefreshTree_RebuildsFromCurrentMessage()` - State reset (FR-006)
    - `LoadJsonAsync_SetsIsLoadingDuringOperation()` - Loading indicator
    - Mock JsonTreeBuilder dependency via DI
  - **Expected**: All tests FAIL (ViewModel doesn't use JsonTreeBuilder yet)
  - **Dependencies**: None
  - **Parallel**: Yes [P] - Independent test file

- [ ] **T005 [P]** Write integration tests for cross-context consistency
  - **Path**: `tests/integration/JsonExpansionIntegrationTests.cs`
  - **Action**: Create integration test file for acceptance scenarios from spec.md:
    - AC-1: `:view json` displays all nested JSON expanded (up to depth 5)
    - AC-2: Message preview displays same expansion as `:view json`
    - AC-4: Manual collapse functionality works
    - AC-5: State resets when switching messages
    - AC-6: 5-level JSON fully expanded
    - AC-7: 6+ level JSON partially expanded (first 5 levels)
  - **Expected**: Tests FAIL (UI integration not updated yet)
  - **Dependencies**: None
  - **Parallel**: Yes [P] - Independent test file

## Phase 3.3: Core Implementation (ONLY after T002-T005 are failing)

**⚠️ DO NOT START until all tests in Phase 3.2 are written and failing**

- [ ] **T006 [P]** Implement JsonTreeNode model class
  - **Path**: `src/Utils/Models/JsonTreeNode.cs`
  - **Action**: Create JsonTreeNode class per data-model.md:
    - Properties: Key, Value, ValueKind, Depth, IsExpanded, IsExpandable, Children, Parent
    - INotifyPropertyChanged implementation for UI binding
    - ObservableCollection<JsonTreeNode> for Children
    - Constructor sets IsExpanded = (Depth <= 5 && IsExpandable)
  - **Goal**: Make T002 tests PASS
  - **Dependencies**: None
  - **Parallel**: Yes [P] - New file in Utils/

- [ ] **T007** Implement JsonTreeBuilder.BuildTree() method
  - **Path**: `src/Utils/JsonTreeBuilder.cs`
  - **Action**: Create JsonTreeBuilder class per research.md:
    - Const `MaxAutoExpandDepth = 5`
    - Public method `BuildTree(JsonDocument document)` → JsonTreeNode
    - Private method `BuildTreeRecursive(JsonElement element, string key, int depth, JsonTreeNode? parent)` → JsonTreeNode
    - Set IsExpanded during tree construction (not post-traversal)
    - Handle Object, Array, and scalar JsonValueKind correctly
    - Lazy-load children for depth > 5 (set HasChildren but don't populate)
  - **Goal**: Make T003 tests PASS
  - **Dependencies**: T006 (needs JsonTreeNode)
  - **Parallel**: No - Sequential after T006

- [ ] **T008** Update JsonViewerViewModel to use JsonTreeBuilder
  - **Path**: `src/UI/ViewModels/JsonViewerViewModel.cs` (or similar existing path)
  - **Action**: Modify existing JsonViewerViewModel:
    - Inject JsonTreeBuilder via constructor (DI)
    - Update `LoadJsonAsync(MqttMessage message)` to call `_treeBuilder.BuildTree()`
    - Set `RootNode = tree` from builder result
    - Add try-catch for JsonException → set ErrorMessage
    - Implement `RefreshTree()` → rebuild from CurrentMessage
    - Dispose old RootNode before replacing
  - **Goal**: Make T004 tests PASS
  - **Dependencies**: T007 (needs JsonTreeBuilder)
  - **Parallel**: No - Sequential after T007

## Phase 3.4: Integration

- [ ] **T009** Update `:view json` command to use new expansion logic
  - **Path**: `src/UI/Commands/ViewJsonCommand.cs` (or similar existing command handler)
  - **Action**: Verify `:view json` command calls JsonViewerViewModel.LoadJsonAsync
    - If command doesn't use ViewModel, refactor to use it
    - Ensure TreeView control binds to RootNode.Children
    - Bind TreeViewItem.IsExpanded to JsonTreeNode.IsExpanded property
  - **Goal**: `:view json` displays expanded JSON (AC-1)
  - **Dependencies**: T008
  - **Parallel**: No - Modifies existing command infrastructure

- [ ] **T010 [P]** Update message preview component to use JsonTreeBuilder
  - **Path**: `src/UI/Components/MessagePreview.xaml(.cs)` (or similar preview component)
  - **Action**: Update message preview component:
    - Use same JsonViewerViewModel or instantiate JsonTreeBuilder directly
    - Ensure consistent expansion behavior with `:view json`
    - Bind to same IsExpanded property for tree nodes
  - **Goal**: Preview displays same expansion as `:view json` (AC-2)
  - **Dependencies**: T008 (needs JsonViewerViewModel pattern)
  - **Parallel**: Yes [P] - Different component file

- [ ] **T011 [P]** Update command palette JSON display to use JsonTreeBuilder
  - **Path**: `src/UI/Components/CommandPalette.xaml(.cs)` (or similar palette component)
  - **Action**: Update command palette JSON rendering:
    - Use JsonTreeBuilder for any JSON previews in palette
    - Consistent expansion behavior with other contexts
  - **Goal**: All JSON contexts use same expansion logic (FR-004)
  - **Dependencies**: T008
  - **Parallel**: Yes [P] - Different component file

- [ ] **T012** Wire up state reset on message switch
  - **Path**: `src/UI/ViewModels/JsonViewerViewModel.cs` (or message selection handler)
  - **Action**: Ensure message selection change triggers tree rebuild:
    - Hook into message selection changed event
    - Call JsonViewerViewModel.LoadJsonAsync(newMessage) on selection change
    - Verify old tree is disposed (no memory leaks)
  - **Goal**: State resets between messages (AC-5, FR-006)
  - **Dependencies**: T008
  - **Parallel**: No - Modifies same ViewModel as T008

## Phase 3.5: Polish & Validation

- [ ] **T013 [P]** Performance test with large JSON payloads
  - **Path**: `tests/integration/JsonExpansionPerformanceTests.cs`
  - **Action**: Create performance test file:
    - Test 1000+ property flat JSON → verify <1s render time
    - Test 10-level deep nesting → verify partial expansion works
    - Test large array (1000+ items) → verify virtualization
    - Profile tree construction time with BenchmarkDotNet or Stopwatch
  - **Goal**: Verify <100ms for typical payloads, <1s for large payloads
  - **Dependencies**: T008
  - **Parallel**: Yes [P] - New test file

- [ ] **T014 [P]** Malformed JSON error handling test
  - **Path**: `tests/integration/JsonExpansionErrorTests.cs`
  - **Action**: Create error handling test:
    - Test malformed JSON → ErrorMessage set, no crash
    - Test empty string → graceful handling
    - Test null payload → graceful handling
  - **Goal**: Validate edge case handling from spec
  - **Dependencies**: T008
  - **Parallel**: Yes [P] - New test file

- [ ] **T015** Execute manual validation from quickstart.md
  - **Path**: `specs/003-json-viewer-should/quickstart.md`
  - **Action**: Run all 8 manual test scenarios from quickstart.md:
    - Test 1: `:view json` expansion
    - Test 2: 5-level depth limit
    - Test 3: 6+ level partial expansion
    - Test 4: Manual collapse functionality
    - Test 5: State reset on message switch
    - Test 6: Cross-context consistency (preview)
    - Test 7: Performance with large JSON
    - Test 8: Malformed JSON handling
    - Fill in "Actual Result" fields
  - **Goal**: All acceptance criteria pass
  - **Dependencies**: All previous tasks
  - **Parallel**: No - Final validation

- [ ] **T016 [P]** Cross-platform compatibility check
  - **Path**: Manual testing on Windows/Linux/macOS
  - **Action**: If possible, verify identical behavior across platforms:
    - TreeView expansion looks same
    - Performance similar
    - No platform-specific crashes
  - **Goal**: Validate constitutional requirement V (Cross-Platform Compatibility)
  - **Dependencies**: T015
  - **Parallel**: Yes [P] - Manual testing, can run simultaneously with doc updates

- [ ] **T017 [P]** Update CLAUDE.md with recent changes (if needed)
  - **Path**: `CLAUDE.md`
  - **Action**: Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude` if any new patterns emerged during implementation
  - **Goal**: Keep agent context current
  - **Dependencies**: None (can run anytime after implementation)
  - **Parallel**: Yes [P] - Documentation task

## Dependencies Graph

```
T001 (Setup)
  ↓
T002-T005 (Tests - Parallel) → MUST ALL FAIL
  ↓
T006 (JsonTreeNode) [P]
  ↓
T007 (JsonTreeBuilder)
  ↓
T008 (JsonViewerViewModel)
  ↓
T009 (`:view json` command)
  ↓
T010, T011 (Preview, Palette) [P]
  ↓
T012 (State reset)
  ↓
T013, T014 (Performance, Error tests) [P]
  ↓
T015 (Manual validation)
  ↓
T016, T017 (Cross-platform, Docs) [P]
```

## Parallel Execution Examples

**Phase 3.2 - All tests in parallel**:
```
T002, T003, T004, T005 can all run concurrently (independent test files)
```

**Phase 3.4 - UI component updates**:
```
T010, T011 can run in parallel (different component files)
```

**Phase 3.5 - Final validation**:
```
T013, T014 can run in parallel (independent test files)
T016, T017 can run in parallel (manual + docs)
```

## Notes

- **TDD Critical**: T002-T005 MUST fail before starting T006
- **[P] Marker**: Tasks marked [P] touch independent files and can run in parallel
- **File Paths**: Adjust exact paths based on actual project structure (paths are indicative)
- **Constitution**: All tasks respect UI → BusinessLogic → Utils dependency flow
- **Commit Strategy**: Commit after each task or logical group (T002-T005 together, T006+T007 together)

## Task Generation Rules Applied

1. **From JsonTreeBuilder Contract**: T003 implements all test cases from contracts/JsonTreeBuilder.contract.md
2. **From Data Model**:
   - JsonTreeNode entity → T002 (tests), T006 (implementation)
   - JsonTreeBuilder utility → T003 (tests), T007 (implementation)
   - JsonViewerViewModel → T004 (tests), T008 (implementation)
3. **From Acceptance Scenarios**: T005 (integration tests for AC-1, AC-2, AC-4, AC-5, AC-6, AC-7)
4. **From Edge Cases**: T013 (performance), T014 (malformed JSON)
5. **From Quickstart**: T015 (manual validation)

## Validation Checklist

- [x] All contracts have corresponding tests (JsonTreeBuilder → T003)
- [x] All entities have model tasks (JsonTreeNode → T002/T006, JsonTreeBuilder → T003/T007)
- [x] All tests come before implementation (T002-T005 before T006-T012)
- [x] Parallel tasks truly independent (T002-T005 different files, T010-T011 different components)
- [x] Each task specifies file path (all tasks include Path field)
- [x] No [P] task modifies same file (T010 vs T011 are different components)

---

**Total Tasks**: 17
**Estimated Completion Time**: 6-8 hours (assuming familiarity with codebase)
**Ready for Execution**: Yes - Proceed with `/implement` command
