# Tasks: Delete Topic Command

**Input**: Design documents from `S:\rc\_priv\Crow-s-Nest-MQTT\specs\001-there-should-be\`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- **C# Desktop Application**: `src/UI/`, `src/BusinessLogic/`, `src/Utils/`, `src/MainApp/`
- **Tests**: `tests/UnitTests/`, `tests/integration/`, `tests/contract/`
- All paths reflect Crow's NestMQTT modular architecture with dependency flow UI → BusinessLogic → Utils

## Phase 3.1: Setup
- [x] T001 Ensure project structure exists with proper dependency references for delete topic feature
- [x] T002 Add required NuGet packages: verify MQTTnet, xUnit, Serilog dependencies are available
- [x] T003 [P] Configure delete topic specific settings: MaxTopicLimit, ParallelismDegree, TimeoutPeriod in configuration

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**

### Contract Tests
- [x] T004 [P] Contract test for IDeleteTopicService.DeleteTopicAsync in tests/contract/DeleteTopicServiceTests.cs
- [x] T005 [P] Contract test for IDeleteTopicService.FindTopicsWithRetainedMessages in tests/contract/DeleteTopicServiceTests.cs
- [x] T006 [P] Contract test for IDeleteTopicService.ValidateDeleteOperation in tests/contract/DeleteTopicServiceTests.cs
- [x] T007 [P] Contract test for ICommandProcessor.ExecuteDeleteTopicCommand in tests/contract/CommandProcessorExtensionTests.cs

### Integration Tests
- [x] T008 [P] Integration test: Delete selected topic in tests/integration/DeleteTopicIntegrationTests.cs
- [x] T009 [P] Integration test: Delete with topic pattern in tests/integration/DeleteTopicIntegrationTests.cs
- [x] T010 [P] Integration test: Delete non-existent topic in tests/integration/DeleteTopicIntegrationTests.cs
- [x] T011 [P] Integration test: Large topic count with confirmation in tests/integration/DeleteTopicLimitTests.cs
- [x] T012 [P] Integration test: Permission denied scenario in tests/integration/DeleteTopicErrorTests.cs
- [x] T013 [P] Integration test: Broker disconnection during operation in tests/integration/DeleteTopicErrorTests.cs

### Performance Tests
- [x] T014 [P] Performance test: Parallel processing verification in tests/integration/DeleteTopicPerformanceTests.cs
- [x] T015 [P] Performance test: UI responsiveness during large operations in tests/integration/DeleteTopicPerformanceTests.cs

## Phase 3.3: Core Implementation (ONLY after tests are failing)

### Data Models
- [x] T016 [P] DeleteTopicCommand model in src/BusinessLogic/Models/DeleteTopicCommand.cs
- [x] T017 [P] DeleteTopicResult model in src/BusinessLogic/Models/DeleteTopicResult.cs
- [x] T018 [P] TopicDeletionFailure model in src/BusinessLogic/Models/TopicDeletionFailure.cs
- [x] T019 [P] DeleteOperationStatus and DeletionErrorType enums in src/BusinessLogic/Models/DeleteTopicEnums.cs
- [x] T020 [P] ValidationResult model for operation validation in src/Utils/Models/ValidationResult.cs

### Service Implementation
- [x] T021 DeleteTopicService implementation in src/BusinessLogic/Services/DeleteTopicService.cs
- [x] T022 Command processor extension for :deletetopic in src/UI/Commands/DeleteTopicCommandExtensions.cs

## Phase 3.4: Integration
- [x] T023 Connect DeleteTopicService to MQTTnet client for publishing operations
- [x] T024 Integrate delete topic command with existing CommandProcessor and command palette system
- [x] T025 Wire up real-time UI updates: topic tree count updates and status bar notifications
- [x] T026 Add configuration support for MaxTopicLimit, timeouts, and confirmation thresholds

## Phase 3.5: Polish
- [x] T027 [P] Unit tests for validation logic in tests/UnitTests/ValidationTests.cs
- [x] T028 [P] Unit tests for error handling and edge cases in tests/UnitTests/DeleteTopicServiceTests.cs
- [x] T029 Performance validation: 500+ topics in <5 seconds, parallel execution verification
- [x] T030 [P] Update CLAUDE.md and project documentation with delete topic command usage
- [x] T031 Manual testing execution following quickstart.md test scenarios
- [x] T032 Code cleanup: remove duplication, optimize parallel task handling

## Dependencies
- Setup (T001-T003) before everything
- All tests (T004-T015) before implementation (T016-T032)
- Models (T016-T020) before services (T021-T022)
- Core implementation (T021-T022) before integration (T023-T026)
- Integration before polish (T027-T032)

## Parallel Example
```
# Launch contract tests together (T004-T007):
Task: "Contract test for IDeleteTopicService.DeleteTopicAsync in tests/contract/DeleteTopicServiceTests.cs"
Task: "Contract test for IDeleteTopicService.FindTopicsWithRetainedMessages in tests/contract/DeleteTopicServiceTests.cs"
Task: "Contract test for IDeleteTopicService.ValidateDeleteOperation in tests/contract/DeleteTopicServiceTests.cs"
Task: "Contract test for ICommandProcessor.ExecuteDeleteTopicCommand in tests/contract/CommandProcessorExtensionTests.cs"

# Launch model creation together (T016-T020):
Task: "DeleteTopicCommand model in src/BusinessLogic/Models/DeleteTopicCommand.cs"
Task: "DeleteTopicResult model in src/BusinessLogic/Models/DeleteTopicResult.cs"
Task: "TopicDeletionFailure model in src/BusinessLogic/Models/TopicDeletionFailure.cs"
Task: "DeleteOperationStatus and DeletionErrorType enums in src/BusinessLogic/Models/DeleteTopicEnums.cs"
Task: "ValidationResult model for operation validation in src/Utils/Models/ValidationResult.cs"
```

## Task Generation Rules Applied
- **From Contracts**: Each contract method → contract test task [P] (T004-T007)
- **From Data Model**: Each entity → model creation task [P] (T016-T020)
- **From Quickstart**: Each test scenario → integration test [P] (T008-T015)
- **Implementation**: Service and command implementations (T021-T022)
- **Integration**: MQTT client integration, UI integration (T023-T026)

## Validation Checklist
- [x] All contracts have corresponding tests (IDeleteTopicService: T004-T006, ICommandProcessor: T007)
- [x] All entities have model tasks (DeleteTopicCommand: T016, DeleteTopicResult: T017, etc.)
- [x] All tests come before implementation (T004-T015 before T016-T032)
- [x] Parallel tasks truly independent (different files, no shared dependencies)
- [x] Each task specifies exact file path
- [x] No task modifies same file as another [P] task
- [x] Dependencies respect UI → BusinessLogic → Utils architecture flow

## Notes
- [P] tasks = different files, no dependencies between them
- Verify all tests fail before implementing (TDD requirement)
- Focus on parallel MQTT publishing for performance
- Maintain real-time UI updates throughout operation
- All tasks are immediately executable with specific file paths and clear requirements