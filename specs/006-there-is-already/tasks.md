# Tasks: Export All Messages from Topic History

**Input**: Design documents from `specs/006-there-is-already/`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → ✅ Loaded: C# desktop app, WPF/Avalonia, MQTTnet, Serilog
   → Tech stack: C# .NET, modular architecture (UI → BusinessLogic → Utils)
2. Load optional design documents:
   → ✅ data-model.md: ExportAllOperation, IMessageExporter extension
   → ✅ contracts/: 3 contracts (command, service, UI buttons)
   → ✅ research.md: 12 key decisions documented
   → ✅ quickstart.md: 4 user scenarios, acceptance checklist
3. Generate tasks by category:
   → Setup: No new project init needed (extends existing)
   → Tests: 3 contract tests + 6 integration tests
   → Core: Command parsing, exporters, UI commands
   → Integration: Export service, filename generation
   → Polish: Documentation, cross-platform validation
4. Apply task rules:
   → Contract tests [P] (different files)
   → Entity tasks [P] (ExportAllOperation is value object, no DB)
   → Export implementations sequential (modify JsonExporter, TextExporter)
   → UI tasks sequential (modify MainViewModel, MainView.axaml)
5. Number tasks sequentially (T001-T027)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness:
   → ✅ All 3 contracts have tests
   → ✅ All entities (ExportAllOperation) have tasks
   → ✅ All UI elements (2 buttons) have tasks
9. Return: SUCCESS (27 tasks ready for TDD execution)
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- **C# Desktop Application**: `src/UI/`, `src/BusinessLogic/`, `src/Utils/`, `src/MainApp/`
- **Tests**: `tests/UnitTests/`, `tests/integration/`, `tests/contract/`
- All paths reflect Crow's NestMQTT modular architecture with dependency flow UI → BusinessLogic → Utils

---

## Phase 3.1: Setup

- [x] **T001** Verify existing project structure supports export all feature (no new projects needed, extending existing)

---

## Phase 3.2: Contract Tests (TDD - Write Failing Tests First) ⚠️ MUST COMPLETE BEFORE 3.3

**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Command Parsing Contract Tests

- [x] **T002** [P] Write contract test for `:export all` command parsing with settings
  **File**: `tests/contract/ExportAllCommandContractTests.cs`
  **Test**: `ParseCommand_ExportAll_WithSettings_ReturnsSuccess()`
  **Expected**: Test creates CommandParserService with settings, calls `ParseCommand(":export all")`, asserts ParsedCommand has arguments `["all", "json", "/path"]`
  **Contract Reference**: `contracts/export-all-command.md` Test 1

- [x] **T003** [P] Write contract test for `:export all` command with explicit parameters
  **File**: `tests/contract/ExportAllCommandContractTests.cs`
  **Test**: `ParseCommand_ExportAll_WithExplicitParams_ReturnsSuccess()`
  **Expected**: Test calls `ParseCommand(":export all txt /custom/path")`, asserts arguments `["all", "txt", "/custom/path"]`
  **Contract Reference**: `contracts/export-all-command.md` Test 2

- [x] **T004** [P] Write contract test for `:export all` with invalid format
  **File**: `tests/contract/ExportAllCommandContractTests.cs`
  **Test**: `ParseCommand_ExportAll_InvalidFormat_ReturnsFailure()`
  **Expected**: Test calls `ParseCommand(":export all xml /path")`, asserts failure with error message "Invalid format"
  **Contract Reference**: `contracts/export-all-command.md` Test 3

- [x] **T005** [P] Write contract test for backward compatibility (`:export` still works)
  **File**: `tests/contract/ExportAllCommandContractTests.cs`
  **Test**: `ParseCommand_Export_WithoutAll_UsesExistingLogic()`
  **Expected**: Test calls `ParseCommand(":export")`, asserts no "all" parameter, arguments are `["json", "/path"]`
  **Contract Reference**: `contracts/export-all-command.md` Test 6

### Export Service Contract Tests

- [x] **T006** [P] Write contract test for ExportAllToFile with single message (JSON)
  **File**: `tests/contract/ExportAllServiceContractTests.cs`
  **Test**: `ExportAllToFile_SingleMessage_Json_CreatesValidArray()`
  **Expected**: Test creates JsonExporter, calls ExportAllToFile with 1 message, asserts file contains valid JSON array with 1 element
  **Contract Reference**: `contracts/export-all-service.md` Test 1

- [x] **T007** [P] Write contract test for ExportAllToFile with multiple messages (JSON)
  **File**: `tests/contract/ExportAllServiceContractTests.cs`
  **Test**: `ExportAllToFile_MultipleMessages_Json_CreatesArrayWithCorrectCount()`
  **Expected**: Test creates 10 messages, calls ExportAllToFile, asserts JSON array has 10 elements
  **Contract Reference**: `contracts/export-all-service.md` Test 2

- [x] **T008** [P] Write contract test for ExportAllToFile with empty collection
  **File**: `tests/contract/ExportAllServiceContractTests.cs`
  **Test**: `ExportAllToFile_EmptyCollection_ReturnsNull()`
  **Expected**: Test calls ExportAllToFile with empty array, asserts returns null, no file created
  **Contract Reference**: `contracts/export-all-service.md` Test 3

- [x] **T009** [P] Write contract test for ExportAllToFile with delimiter (TXT)
  **File**: `tests/contract/ExportAllServiceContractTests.cs`
  **Test**: `ExportAllToFile_MultipleMessages_Txt_ContainsDelimiters()`
  **Expected**: Test creates 3 messages, calls TextExporter.ExportAllToFile, asserts file contains 2 delimiters (`========`)
  **Contract Reference**: `contracts/export-all-service.md` Test 6

### UI Button Contract Tests

- [x] **T010** [P] Write contract test for Export All button enabled state
  **File**: `tests/contract/UiExportButtonsContractTests.cs`
  **Test**: `ExportAllButton_TopicAndMessagesExist_IsEnabled()`
  **Expected**: Test creates MainViewModel with selected topic and messages, asserts `IsExportAllButtonEnabled == true`
  **Contract Reference**: `contracts/ui-export-buttons.md` Test 3

- [x] **T011** [P] Write contract test for Export All button disabled when no topic
  **File**: `tests/contract/UiExportButtonsContractTests.cs`
  **Test**: `ExportAllButton_NoTopicSelected_IsDisabled()`
  **Expected**: Test sets `SelectedNode = null`, asserts `IsExportAllButtonEnabled == false`
  **Contract Reference**: `contracts/ui-export-buttons.md` Test 1

- [x] **T012** [P] Write contract test for per-message export button parameter passing
  **File**: `tests/contract/UiExportButtonsContractTests.cs`
  **Test**: `PerMessageExportButton_Click_PassesCorrectMessageViewModel()`
  **Expected**: Test executes ExportMessageCommand with MessageViewModel, asserts parameter received correctly
  **Contract Reference**: `contracts/ui-export-buttons.md` Test 5

---

## Phase 3.3: Core Implementation (ONLY after contract tests are failing)

### Command Parsing Extension

- [ ] **T013** Extend CommandParserService to recognize `:export all` parameter
  **File**: `src/BusinessLogic/Services/CommandParserService.cs`
  **Implementation**:
  - In `case "export":` block, check if `arguments[0].ToLowerInvariant() == "all"`
  - If "all" and 1 arg: use settings → return `ParsedCommand(Export, ["all", format, path])`
  - If "all" and 3 args: validate format, return `ParsedCommand(Export, ["all", args[1], args[2]])`
  - Existing `:export` logic unchanged (backward compatibility)
  **Contract Reference**: `contracts/export-all-command.md`
  **Dependencies**: T002-T005 must fail first

### Value Object Creation

- [ ] **T014** [P] Create ExportAllOperation value object
  **File**: `src/BusinessLogic/Models/ExportAllOperation.cs`
  **Implementation**:
  - Record with properties: TopicName, MessageCount, ExportedCount, ExportFormat, OutputFilePath, Timestamp, IsLimitExceeded
  - Add validation: MessageCount >= 0, ExportedCount <= min(MessageCount, 100), TopicName not empty
  - Add factory method: `Create(topic, messages, settings)` that auto-calculates counts
  **Data Model Reference**: `data-model.md` Section 2

### Export Service Extension (JSON)

- [ ] **T015** Extend JsonExporter with ExportAllToFile method
  **File**: `src/BusinessLogic/Exporter/JsonExporter.cs`
  **Implementation**:
  - Add method: `string? ExportAllToFile(IEnumerable<MqttApplicationMessage> messages, IEnumerable<DateTime> timestamps, string outputFilePath)`
  - Validate: messages/timestamps not null, counts match, empty collection returns null
  - Map messages to `List<MqttMessageExportDto>` using Zip
  - Serialize with `JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true })`
  - Write to file with `File.WriteAllText(outputFilePath, json)`
  - Return outputFilePath on success, null on error (IOException handled)
  **Contract Reference**: `contracts/export-all-service.md`
  **Dependencies**: T006-T008 must fail first

### Export Service Extension (TXT)

- [ ] **T016** Extend TextExporter with ExportAllToFile method
  **File**: `src/BusinessLogic/Exporter/TextExporter.cs`
  **Implementation**:
  - Add method: `string? ExportAllToFile(IEnumerable<MqttApplicationMessage> messages, IEnumerable<DateTime> timestamps, string outputFilePath)`
  - Validate: messages/timestamps not null, counts match, empty returns null
  - For each message: call existing `GenerateDetailedTextFromMessage(msg, timestamp)`
  - Append content to StringBuilder
  - Insert delimiter between messages: `\n` + 80× `=` + `\n\n` (not after last message)
  - Write to file with `File.WriteAllText(outputFilePath, sb.ToString())`
  **Contract Reference**: `contracts/export-all-service.md`
  **Dependencies**: T009 must fail first

### Interface Update

- [ ] **T017** Add ExportAllToFile to IMessageExporter interface
  **File**: `src/BusinessLogic/Exporter/IMessageExporter.cs`
  **Implementation**:
  - Add method signature: `string? ExportAllToFile(IEnumerable<MqttApplicationMessage>, IEnumerable<DateTime>, string)`
  - Add XML documentation comments from contract
  - Ensure both JsonExporter and TextExporter implement (done in T015-T016)
  **Contract Reference**: `contracts/export-all-service.md`
  **Dependencies**: Must be done before or with T015-T016

### Filename Generation Utility

- [ ] **T018** [P] Create filename generation and topic sanitization utility
  **File**: `src/Utils/FilenameGenerator.cs`
  **Implementation**:
  - Method: `string SanitizeTopicName(string topic)` - replaces invalid chars `/:?*<>\|"` with `_`
  - Method: `string GenerateExportFilename(string topicName, DateTime timestamp, ExportTypes format)` - returns `{sanitized}_{yyyyMMdd_HHmmssfff}.{ext}`
  - Cross-platform safe using `Path.GetInvalidFileNameChars()` for sanitization
  **Research Reference**: `research.md` Section 4
  **Dependencies**: None (pure utility)

### UI Commands - Export All

- [ ] **T019** Add ExportAllCommand to MainViewModel
  **File**: `src/UI/ViewModels/MainViewModel.cs`
  **Implementation**:
  - Add property: `public ReactiveCommand<Unit, Unit> ExportAllCommand { get; }`
  - Add property: `public bool IsExportAllButtonEnabled =>` (SelectedNode != null && FilteredMessageHistory.Any())
  - Constructor: Create command with `ReactiveCommand.CreateFromTask(ExecuteExportAllAsync, ...)`
  - Method: `async Task ExecuteExportAllAsync()`:
    * Get messages: `FilteredMessageHistory.OrderByDescending(m => m.Timestamp).Take(100).ToList()`
    * Create ExportAllOperation
    * Check if count > 100, set IsLimitExceeded
    * Generate filename using FilenameGenerator (T018)
    * Instantiate exporter (JsonExporter or TextExporter based on settings)
    * Call `await Task.Run(() => exporter.ExportAllToFile(...))`
    * Update StatusBarText with result
  **Contract Reference**: `contracts/ui-export-buttons.md`
  **Dependencies**: T013, T015-T018 must be complete

### UI Commands - Per-Message Export

- [ ] **T020** Add ExportMessageCommand to MainViewModel
  **File**: `src/UI/ViewModels/MainViewModel.cs`
  **Implementation**:
  - Add property: `public ReactiveCommand<MessageViewModel, Unit> ExportMessageCommand { get; }`
  - Constructor: Create command with `ReactiveCommand.CreateFromTask<MessageViewModel>(ExecuteExportMessageAsync)`
  - Method: `async Task ExecuteExportMessageAsync(MessageViewModel msgVm)`:
    * Call `msgVm.GetFullMessage()`, handle null (message evicted)
    * Get format and path from Settings (same as existing `:export`)
    * Instantiate exporter
    * Call existing `exporter.ExportToFile(fullMessage, msgVm.Timestamp, path)`
    * Update StatusBarText
  **Contract Reference**: `contracts/ui-export-buttons.md`
  **Dependencies**: T012 must fail first, existing export infrastructure

### UI Elements - Export All Button

- [ ] **T021** Add Export All button to MainView.axaml toolbar
  **File**: `src/UI/Views/MainView.axaml`
  **Implementation**:
  - Add StreamGeometry resource for export icon (e.g., download icon)
  - Add Button after DeleteTopicButton (Grid Row 1, StackPanel):
    * Command: `{Binding ExportAllCommand}`
    * IsEnabled: `{Binding IsExportAllButtonEnabled}`
    * ToolTip: "Export all messages from selected topic (max 100)"
    * PathIcon with export icon, Width/Height 16
    * Styles for disabled state (opacity 0.5, ButtonForegroundDisabled)
  **Contract Reference**: `contracts/ui-export-buttons.md`
  **Dependencies**: T019 complete

### UI Elements - Per-Message Export Button

- [ ] **T022** Add per-message export button to history ListBox item template
  **File**: `src/UI/Views/MainView.axaml`
  **Implementation**:
  - In MessageHistoryListBox ItemTemplate (Grid Row 4):
    * Add Button docked right, before existing Copy button
    * Command: `{Binding $parent[ListBox].DataContext.ExportMessageCommand}`
    * CommandParameter: `{Binding}` (passes MessageViewModel)
    * ToolTip: "Export this message"
    * PathIcon with export icon, Width/Height 12
    * Padding/Margin match copy button style
  **Contract Reference**: `contracts/ui-export-buttons.md`
  **Dependencies**: T020 complete

---

## Phase 3.4: Integration Tests (Validate End-to-End Scenarios)

- [ ] **T023** [P] Integration test: Export all with 50 messages creates JSON array file
  **File**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
  **Test**: `ExportAll_50Messages_Json_CreatesValidFile()`
  **Implementation**:
  - Setup: Create MainViewModel, configure settings (json, /temp path)
  - Setup: Add 50 MessageViewModels to FilteredMessageHistory
  - Execute: `await viewModel.ExportAllCommand.Execute()`
  - Assert: File exists, contains JSON array with 50 elements
  - Assert: StatusBarText contains "Exported 50 messages"
  **Quickstart Reference**: Scenario 1

- [ ] **T024** [P] Integration test: Export all with 150 messages enforces 100 limit
  **File**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
  **Test**: `ExportAll_150Messages_EnforcesLimit_ShowsWarning()`
  **Implementation**:
  - Setup: Add 150 MessageViewModels
  - Execute: `await viewModel.ExportAllCommand.Execute()`
  - Assert: File contains exactly 100 messages (most recent)
  - Assert: StatusBarText contains "100 of 150 messages" and "limit enforced"
  **Quickstart Reference**: Scenario 2, Edge Case 3

- [ ] **T025** [P] Integration test: Per-message export button exports single message
  **File**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
  **Test**: `PerMessageExport_SingleMessage_CreatesFile()`
  **Implementation**:
  - Setup: Add 10 MessageViewModels
  - Execute: `await viewModel.ExportMessageCommand.Execute(messageVm[5])`
  - Assert: File exists with single message (not array)
  - Assert: Filename matches existing single-export pattern `{timestamp}_{topic}.{ext}`
  **Quickstart Reference**: Scenario 3

- [ ] **T026** [P] Integration test: `:export` command still works (backward compatibility)
  **File**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
  **Test**: `Export_WithoutAll_ExportsSelectedMessage()`
  **Implementation**:
  - Setup: Add messages, select one message
  - Execute: `await viewModel.Export(ParsedCommand(Export, ["json", "/temp"]))`
  - Assert: File created with selected message only
  - Assert: Behavior unchanged from before feature
  **Quickstart Reference**: Scenario 4

- [ ] **T027** [P] Integration test: Export all with empty history shows error
  **File**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
  **Test**: `ExportAll_EmptyHistory_ShowsError()`
  **Implementation**:
  - Setup: SelectedNode set, but FilteredMessageHistory empty
  - Execute: `await viewModel.ExportAllCommand.Execute()`
  - Assert: No file created
  - Assert: StatusBarText contains "No messages to export"
  **Quickstart Reference**: Edge Case 2

- [ ] **T028** [P] Integration test: File overwrite happens without confirmation
  **File**: `tests/integration/ExportAllMessagesIntegrationTests.cs`
  **Test**: `ExportAll_FileExists_OverwritesSilently()`
  **Implementation**:
  - Setup: Create export file with "old content"
  - Execute: Export all to same path
  - Assert: File content replaced (no old content)
  - Assert: No confirmation dialog shown (silent overwrite)
  **Quickstart Reference**: Edge Case 4

---

## Phase 3.5: Polish & Validation

- [ ] **T029** [P] Add unit tests for FilenameGenerator sanitization
  **File**: `tests/UnitTests/FilenameGeneratorTests.cs`
  **Tests**:
  - `SanitizeTopicName_WithSlashes_ReplacesWithUnderscores()`
  - `SanitizeTopicName_WithWildcards_ReplacesWithUnderscores()`
  - `GenerateExportFilename_ValidInputs_ReturnsCorrectPattern()`

- [ ] **T030** [P] Update quickstart.md with actual file paths and test results
  **File**: `specs/006-there-is-already/quickstart.md`
  **Implementation**: Add section "Validation Results" with actual test execution output

- [ ] **T031** Cross-platform validation: Test on Windows, Linux, macOS
  **Manual Task**:
  - Run all integration tests on each platform
  - Verify filename generation works (Path.Combine, sanitization)
  - Verify file I/O works (async writes)
  - Document any platform-specific issues

- [ ] **T032** Performance validation: Export 100 messages < 1 second
  **File**: `tests/integration/ExportPerformanceTests.cs`
  **Test**: `ExportAll_100Messages_CompletesWithin1Second()`
  **Implementation**:
  - Setup: Create 100 messages with realistic payloads (~1KB each)
  - Execute: Measure time for `ExportAllCommand.Execute()`
  - Assert: Execution time < 1000ms
  - Assert: UI remains responsive (no freezing)

- [ ] **T033** Remove any code duplication between JsonExporter and TextExporter
  **Files**: `src/BusinessLogic/Exporter/JsonExporter.cs`, `src/BusinessLogic/Exporter/TextExporter.cs`
  **Implementation**:
  - Extract common validation logic (null checks, count matching) to shared helper
  - Extract common error handling (IOException, logging) to base class or helper
  - Ensure both exporters follow same patterns

---

## Dependencies

**Critical Path (TDD Order):**
```
T001 (Setup verification)
  ↓
T002-T012 (All contract tests - MUST FAIL before implementation) [P]
  ↓
T013 (Command parsing) ← blocks T019, T026
  ↓
T014 (ExportAllOperation) [P] ← used by T019
T017 (Interface update) ← blocks T015-T016
  ↓
T015 (JsonExporter) ← blocks T019, T023-T024
T016 (TextExporter) ← blocks T019, T025
T018 (FilenameGenerator) [P] ← blocks T019
  ↓
T019 (ExportAllCommand) ← blocks T021, T023-T025, T027-T028
T020 (ExportMessageCommand) ← blocks T022, T025
  ↓
T021 (Export All button) [P]
T022 (Per-message button) [P]
  ↓
T023-T028 (Integration tests) [P]
  ↓
T029-T033 (Polish) [P]
```

**Parallel Execution Groups:**

- **Group 1 (Contract Tests)**: T002-T012 (all independent files)
- **Group 2 (Core Logic)**: T014 (value object), T018 (utility) - while T013 is in progress
- **Group 3 (UI Elements)**: T021, T022 - after T019-T020 complete
- **Group 4 (Integration)**: T023-T028 - after T019-T022 complete
- **Group 5 (Polish)**: T029-T033 - after all integration tests pass

---

## Parallel Execution Example

### Launch Contract Tests Together (Group 1):
```bash
# All contract tests can run in parallel (different files)
# T002-T012: 11 tests across 3 files

# Terminal 1:
task T002 "Write contract test for :export all with settings in tests/contract/ExportAllCommandContractTests.cs"

# Terminal 2:
task T006 "Write contract test for ExportAllToFile single message JSON in tests/contract/ExportAllServiceContractTests.cs"

# Terminal 3:
task T010 "Write contract test for Export All button enabled state in tests/contract/UiExportButtonsContractTests.cs"

# Continue with T003-T005, T007-T009, T011-T012 in parallel...
```

### Launch Integration Tests Together (Group 4):
```bash
# After T019-T022 complete, run all integration tests in parallel
# T023-T028: 6 tests in same file but different test methods

# Run all integration tests:
dotnet test tests/integration/ExportAllMessagesIntegrationTests.cs --parallel
```

---

## Notes

- **[P] = Parallel**: Can run simultaneously with other [P] tasks
- **TDD Mandatory**: All T002-T012 contract tests MUST fail before implementing T013-T022
- **File Conflicts**: T015-T016 modify different files (JsonExporter vs TextExporter) but T021-T022 modify same file (MainView.axaml) - run sequentially
- **Commit Strategy**: Commit after each task completes (or after each parallel group)
- **Test-First**: Run tests after each implementation task to verify they now pass

---

## Task Generation Rules Applied

1. **From Contracts** (3 files → 11 tests):
   - export-all-command.md → T002-T005 (4 tests)
   - export-all-service.md → T006-T009 (4 tests)
   - ui-export-buttons.md → T010-T012 (3 tests)

2. **From Data Model**:
   - ExportAllOperation → T014 (value object creation)
   - IMessageExporter extension → T015-T017 (interface + implementations)

3. **From User Stories** (quickstart.md):
   - Scenario 1 → T023 (export all 50 messages)
   - Scenario 2 → T024 (100 limit enforcement)
   - Scenario 3 → T025 (per-message export)
   - Scenario 4 → T026 (backward compatibility)
   - Edge cases → T027-T028 (empty history, file overwrite)

4. **Ordering**:
   - Setup (T001) → Tests (T002-T012) → Core (T013-T018) → UI (T019-T022) → Integration (T023-T028) → Polish (T029-T033)

---

## Validation Checklist
*GATE: Verify before executing*

- [x] All 3 contracts have corresponding tests (T002-T012)
- [x] ExportAllOperation entity has model task (T014)
- [x] All contract tests come before implementation (T002-T012 before T013-T022)
- [x] Parallel tasks are truly independent (verified file paths)
- [x] Each task specifies exact file path
- [x] No [P] task modifies same file as another [P] task
- [x] TDD order enforced: Tests fail → Implement → Tests pass
- [x] Dependencies clearly documented
- [x] Backward compatibility tested (T026)
- [x] Cross-platform validation included (T031)
- [x] Performance validation included (T032)

---

**Total Tasks**: 33
**Estimated Parallel Groups**: 5
**Critical Path Length**: ~15 sequential steps (with parallelization)

**Ready for execution**: Yes ✅
**Next Command**: Begin with `T001` or parallel execute `T002-T012` contract tests
