# Implementation Plan: Export All Messages from Topic History

**Branch**: `006-there-is-already` | **Date**: 2026-01-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/006-there-is-already/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → ✅ Loaded successfully
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → ✅ All technical context known from existing codebase
   → Project Type: C# desktop application (WPF/Avalonia)
   → Structure Decision: Modular architecture (UI → BusinessLogic → Utils)
3. Fill the Constitution Check section based on constitution document
   → ✅ Completed
4. Evaluate Constitution Check section
   → ✅ No violations found
   → Update Progress Tracking: Initial Constitution Check PASSED
5. Execute Phase 0 → research.md
   → ✅ Completed - Comprehensive codebase exploration performed
   → ✅ All research questions answered
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, CLAUDE.md
   → ✅ Completed - All artifacts generated
   → ✅ data-model.md created with entity definitions
   → ✅ contracts/ directory created with 3 contract files
   → ✅ quickstart.md created with usage examples
   → ✅ CLAUDE.md updated via update-agent-context script
7. Re-evaluate Constitution Check section
   → ✅ Completed - No new violations introduced
   → ✅ Design aligns with all constitutional principles
8. Plan Phase 2 → Describe task generation approach
   → ✅ Completed - Task generation strategy documented
9. STOP - Ready for /tasks command
   → ✅ Planning phase complete
   → ✅ Ready to proceed with /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 8. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
This feature extends the existing export functionality to support bulk export of all messages from a topic's history view. Users can trigger export via `:export all` command or a new UI button positioned next to the topic delete icon. Additionally, per-message export buttons are added to each row in the history view for quick single-message exports. The feature enforces a 100-message limit per bulk export, auto-generates filenames using `topic-name_timestamp.ext` pattern, and supports both JSON (as array) and TXT (delimited) formats.

## Technical Context
**Language/Version**: C# (.NET - latest stable version per CLAUDE.md)
**Primary Dependencies**: WPF/Avalonia (UI framework), MQTTnet (MQTT client), Serilog (structured logging)
**Storage**: In-memory message buffers with configurable limits, file system for export operations
**Testing**: xUnit or NUnit for unit/integration testing
**Target Platform**: Cross-platform (Windows, Linux, macOS via Avalonia)
**Project Type**: C# desktop application with modular architecture
**Performance Goals**: <100ms command response time, handle 10,000+ messages/second without UI blocking
**Constraints**: Export limit of 100 messages per operation, respect existing buffer limits, file operations must not block UI
**Scale/Scope**: Single feature extending existing export infrastructure, 3 new UI elements, 1 new command variant

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**I. User-Centric Interface**: ✅ PASS
- Feature provides `:export all` command accessible via command palette
- Existing `:export` behavior preserved
- GUI buttons (export all icon, per-message export buttons) support commands but don't replace them
- Clear, memorable syntax with "all" parameter

**II. Real-Time Performance**: ✅ PASS
- 100-message export limit prevents excessive processing
- File I/O operations will use async/await to avoid UI blocking
- Export operations don't interfere with message stream processing
- Buffer limits already established in existing architecture

**III. Test-Driven Development**: ✅ PASS (TO BE VERIFIED)
- Tests will be written first following TDD methodology
- Integration tests required for export operations with file system
- Contract tests for command parsing (`:export all` vs `:export`)
- UI command processing tests for new buttons
- Edge case tests for 100-message limit, empty history, file overwrites

**IV. Modular Architecture**: ✅ PASS
- Design follows UI → BusinessLogic → Utils flow
- Export logic resides in BusinessLogic layer (existing IMessageExporter)
- UI layer (MainViewModel, MainView.axaml) handles button commands
- Command parsing in BusinessLogic (CommandParserService)
- No circular dependencies introduced

**V. Cross-Platform Compatibility**: ✅ PASS
- File path handling will use Path.Combine for cross-platform compatibility
- Timestamp formatting will use ISO 8601 for consistent filenames
- UI buttons use Avalonia cross-platform controls
- No platform-specific code required

**Technical Standards**: ✅ PASS
- Leverages existing MQTT 5.0 message metadata (timestamp, QoS, retain flag, user properties, correlation data)
- Async/await for file I/O operations
- Dependency injection for IMessageExporter services
- Structured logging for export operations (success, failures, limit warnings)
- Command response <100ms (simple command parsing and async dispatch)

**Quality Assurance**: ✅ PASS
- Acceptance criteria defined in spec (10 scenarios)
- Manual testing scenarios: command execution, button clicks, file verification
- Edge cases documented: empty history, >100 messages, file conflicts
- Quickstart will demonstrate all three export modes

## Project Structure

### Documentation (this feature)
```
specs/006-there-is-already/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
# C# Desktop Application (Crow's NestMQTT structure)
src/
├── UI/                   # Presentation layer (WPF/Avalonia)
│   ├── ViewModels/
│   │   └── MainViewModel.cs        # Add export all command, per-message export
│   └── Views/
│       └── MainView.axaml          # Add export all button, per-message buttons
├── BusinessLogic/        # Domain logic and MQTT handling
│   ├── Commands/
│   │   └── CommandType.cs          # Already has Export command
│   ├── Services/
│   │   └── CommandParserService.cs # Extend to parse `:export all`
│   └── Exporter/
│       ├── IMessageExporter.cs     # Existing interface
│       ├── JsonExporter.cs         # Extend for array format
│       └── TextExporter.cs         # Extend for delimited format
├── Utils/               # Shared utilities and common code
└── MainApp/             # Application entry point

tests/
├── UnitTests/           # Unit tests for all components
│   ├── Services/
│   │   └── CommandParserServiceTests.cs
│   ├── ViewModels/
│   │   └── MainViewModelTests.cs
│   └── Exporter/
│       ├── JsonExporterTests.cs
│       └── TextExporterTests.cs
├── integration/         # MQTT broker integration tests
│   └── ExportAllMessagesIntegrationTests.cs
└── contract/            # API contract tests
    └── ExportAllCommandContractTests.cs
```

**Structure Decision**: C# desktop application with modular architecture (existing structure preserved)

## Phase 0: Outline & Research
*Status: In Progress*

### Research Tasks

1. **Existing Export Infrastructure Analysis**
   - **Goal**: Understand current `:export` command implementation
   - **Files to examine**:
     - `src/BusinessLogic/Services/CommandParserService.cs`
     - `src/BusinessLogic/Exporter/IMessageExporter.cs`
     - `src/BusinessLogic/Exporter/JsonExporter.cs`
     - `src/BusinessLogic/Exporter/TextExporter.cs`
     - `src/UI/ViewModels/MainViewModel.cs`
   - **Questions**:
     - How does current `:export` command get parsed?
     - How does single-message export work (file naming, format)?
     - What's the interface contract for IMessageExporter?
     - How are export formats (JSON/TXT) currently handled?
     - Where is export path configuration stored?

2. **Message History Access Patterns**
   - **Goal**: Understand how to access topic's message collection
   - **Files to examine**:
     - `src/UI/ViewModels/MainViewModel.cs` (FilteredMessageHistory property)
     - `src/UI/ViewModels/MessageViewModel.cs`
   - **Questions**:
     - How is FilteredMessageHistory populated?
     - What's the data structure (ObservableCollection, List)?
     - How to get "most recent 100 messages"?
     - What metadata is available on each MessageViewModel?

3. **UI Button Positioning and Styling**
   - **Goal**: Understand current button layout and icon usage
   - **Files to examine**:
     - `src/UI/Views/MainView.axaml` (delete topic button area, history view rows)
   - **Questions**:
     - Where exactly is the delete topic button defined?
     - What icon system is used (PathIcon, StreamGeometry)?
     - How to add icon next to delete button?
     - How to add button to each ListBox item template?
     - What's the binding pattern for per-row commands?

4. **Command Parameter Parsing**
   - **Goal**: Determine best approach for parsing `:export all` vs `:export`
   - **Files to examine**:
     - `src/BusinessLogic/Services/CommandParserService.cs`
   - **Questions**:
     - Does CommandParserService support parameters?
     - How are other parameterized commands handled (e.g., `:filter [regex]`)?
     - Should "all" be a parameter or separate command type?

5. **Filename Generation and Sanitization**
   - **Goal**: Safe filename generation from topic names
   - **Research needs**:
     - How to sanitize topic names for filesystem (e.g., `sensor/+` → `sensor_+`)
     - Cross-platform safe characters
     - Timestamp format for uniqueness (ISO 8601 format)
     - Handling topic names with hierarchy separators (`/`)

6. **JSON Array Format vs Single Object**
   - **Goal**: Ensure JSON array format is valid and parseable
   - **Current state**: Existing JsonExporter likely exports single message as object
   - **Questions**:
     - Does current JsonExporter create `{ "topic": ..., "payload": ... }` format?
     - Need to create wrapper: `[{msg1}, {msg2}, ...]` for export all
     - Preserve same message object structure within array

**Output**: research.md with findings from above investigations

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*
*Status: Pending Phase 0*

### Design Approach

1. **Extract entities from feature spec** → `data-model.md`:
   - **ExportAllOperation**: Encapsulates bulk export logic
     - Fields: TopicName, MessageCount, ExportFormat, FilePath, Timestamp
     - Validation: MessageCount ≤ 100, FilePath writable, TopicName not empty
   - **ExportConfiguration**: Existing entity (reuse)
     - Fields: ExportFormat (JSON/TXT), ExportPath
   - **MessageViewModel**: Existing entity (read-only access)
     - Fields: Timestamp, Topic, Payload, QoS, Retain, UserProperties, CorrelationData

2. **Generate API contracts** from functional requirements:
   - **Command Contract**: `:export all`
     - Input: Command string `:export all`
     - Preconditions: Topic selected, messages exist
     - Postconditions: File created with max 100 messages, status feedback shown
   - **UI Contract**: Export All Button
     - Event: Button click
     - Enabled when: Topic selected AND messages > 0
     - Action: Trigger same logic as `:export all` command
   - **UI Contract**: Per-Message Export Button
     - Event: Button click on specific row
     - Enabled when: Always (message exists in row)
     - Action: Export that message using existing `:export` logic
   - **Export Service Contract**: IMessageExporter extension
     - Method: `ExportAllAsync(IEnumerable<MessageViewModel> messages, string filePath, ExportFormat format)`
     - Returns: `Task<ExportResult>` with success/failure and message count
     - Throws: IOException, ArgumentException

3. **Generate contract tests**:
   - `contracts/export-all-command.md`: Command parsing contract
   - `contracts/export-all-service.md`: IMessageExporter extension contract
   - `contracts/ui-export-buttons.md`: UI button behavior contract

4. **Extract test scenarios** from user stories:
   - Scenario 1: `:export all` with 50 messages → JSON array file created
   - Scenario 2: Export all button click with 150 messages → only 100 exported, warning shown
   - Scenario 3: Per-message export button → single file using existing path
   - Scenario 4: `:export` command still works → backward compatibility
   - Scenario 5: Empty history → error message, no file created
   - Scenario 6: File exists → overwritten without prompt

5. **Update CLAUDE.md incrementally**:
   - Add export all command syntax to "Commands" section
   - Add 100-message limit to "Performance Requirements"
   - Add filename pattern to recent changes

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, CLAUDE.md

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
1. Load `.specify/templates/tasks-template.md` as base
2. Generate tasks from Phase 1 artifacts in TDD order:

**Contract Test Tasks** (write failing tests first):
- Task: Write contract test for `:export all` command parsing [P]
- Task: Write contract test for export all with 100-message limit [P]
- Task: Write contract test for JSON array format [P]
- Task: Write contract test for TXT delimiter format [P]

**Model/Service Tasks**:
- Task: Extend CommandParserService to recognize `:export all` parameter
- Task: Create ExportAllOperation value object for encapsulating bulk export state
- Task: Extend IMessageExporter with ExportAllAsync method
- Task: Implement JSON array format in JsonExporter
- Task: Implement TXT delimiter format in TextExporter
- Task: Add filename generation utility (topic sanitization + timestamp)

**UI Tasks**:
- Task: Add ExportAllCommand to MainViewModel
- Task: Add export all button to MainView.axaml (next to delete topic)
- Task: Add per-message export button to history ListBox item template
- Task: Bind per-message button to new ExportMessageCommand(parameter)
- Task: Add IsExportAllEnabled property (topic selected + messages > 0)

**Integration Test Tasks**:
- Task: Integration test: `:export all` with 50 messages → verify JSON array file
- Task: Integration test: `:export all` with 150 messages → verify 100 limit + warning
- Task: Integration test: Per-message export → verify existing `:export` behavior
- Task: Integration test: `:export` without "all" → verify backward compatibility
- Task: Integration test: Export all with empty history → verify error message
- Task: Integration test: Export all file exists → verify overwrite

**Ordering Strategy**:
- TDD order: Contract tests → Models → Services → UI → Integration tests
- Dependency order:
  1. Command parsing (enables all features)
  2. Export services (core logic)
  3. Filename generation (shared utility)
  4. UI commands and buttons (presentation)
  5. Integration tests (validate end-to-end)
- Mark [P] for parallel execution: Contract tests, model objects

**Estimated Output**: 25-30 numbered, ordered tasks in tasks.md

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional principles)
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

No violations detected. Feature aligns with all constitutional principles:
- User-centric: Command-driven with GUI support
- Performance: 100-message limit prevents blocking
- TDD: Tests written first
- Modular: Respects UI → BusinessLogic → Utils
- Cross-platform: File operations use standard .NET APIs

## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [x] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none)

**Artifacts Generated**:
- [x] research.md - Comprehensive research findings from codebase exploration
- [x] data-model.md - Entity definitions and data structures
- [x] contracts/export-all-command.md - Command parsing contract
- [x] contracts/export-all-service.md - IMessageExporter extension contract
- [x] contracts/ui-export-buttons.md - UI button behavior contracts
- [x] quickstart.md - User-facing usage examples and validation checklist
- [x] CLAUDE.md - Updated with new feature context
- [x] tasks.md - 33 implementation tasks in TDD order with dependency graph

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
