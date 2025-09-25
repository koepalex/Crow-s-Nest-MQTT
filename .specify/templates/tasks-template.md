# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)
```
1. Load plan.md from feature directory
   → If not found: ERROR "No implementation plan found"
   → Extract: tech stack, libraries, structure
2. Load optional design documents:
   → data-model.md: Extract entities → model tasks
   → contracts/: Each file → contract test task
   → research.md: Extract decisions → setup tasks
3. Generate tasks by category:
   → Setup: project init, dependencies, linting
   → Tests: contract tests, integration tests
   → Core: models, services, CLI commands
   → Integration: DB, middleware, logging
   → Polish: unit tests, performance, docs
4. Apply task rules:
   → Different files = mark [P] for parallel
   → Same file = sequential (no [P])
   → Tests before implementation (TDD)
5. Number tasks sequentially (T001, T002...)
6. Generate dependency graph
7. Create parallel execution examples
8. Validate task completeness:
   → All contracts have tests?
   → All entities have models?
   → All endpoints implemented?
9. Return: SUCCESS (tasks ready for execution)
```

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions

## Path Conventions
- **C# Desktop Application**: `src/UI/`, `src/BusinessLogic/`, `src/Utils/`, `src/MainApp/`
- **Tests**: `tests/UnitTests/`, `tests/integration/`, `tests/contract/`
- All paths reflect Crow's NestMQTT modular architecture with dependency flow UI → BusinessLogic → Utils

## Phase 3.1: Setup
- [ ] T001 Create C# project structure per implementation plan
- [ ] T002 Initialize .NET solution with required NuGet packages (MQTT, UI framework)
- [ ] T003 [P] Configure EditorConfig, code analysis rules, and formatting tools

## Phase 3.2: Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**
- [ ] T004 [P] MQTT connection test in tests/integration/MqttConnectionTests.cs
- [ ] T005 [P] Message processing test in tests/integration/MessageProcessingTests.cs
- [ ] T006 [P] Command execution test in tests/integration/CommandExecutionTests.cs
- [ ] T007 [P] UI component test in tests/UnitTests/UI/CommandPaletteTests.cs

## Phase 3.3: Core Implementation (ONLY after tests are failing)
- [ ] T008 [P] MQTT message model in src/BusinessLogic/Models/MqttMessage.cs
- [ ] T009 [P] MQTT service in src/BusinessLogic/Services/MqttService.cs
- [ ] T010 [P] Command processor in src/UI/Commands/CommandProcessor.cs
- [ ] T011 Message buffer management in src/BusinessLogic/Services/BufferService.cs
- [ ] T012 UI message display components
- [ ] T013 Input validation and error handling
- [ ] T014 Structured logging and observability

## Phase 3.4: Integration
- [ ] T015 Connect MQTT service to UI components
- [ ] T016 TLS connection handling and certificate validation
- [ ] T017 Message filtering and search functionality
- [ ] T018 Cross-platform compatibility validation

## Phase 3.5: Polish
- [ ] T019 [P] Unit tests for validation in tests/UnitTests/ValidationTests.cs
- [ ] T020 Performance tests (10k+ msg/sec, <100ms command response)
- [ ] T021 [P] Update README.md with new command documentation
- [ ] T022 Remove code duplication and refactor
- [ ] T023 Run cross-platform manual testing scenarios

## Dependencies
- Tests (T004-T007) before implementation (T008-T014)
- T008 blocks T009, T015
- T016 blocks T018
- Implementation before polish (T019-T023)

## Parallel Example
```
# Launch T004-T007 together:
Task: "MQTT connection test in tests/integration/MqttConnectionTests.cs"
Task: "Message processing test in tests/integration/MessageProcessingTests.cs"
Task: "Command execution test in tests/integration/CommandExecutionTests.cs"
Task: "UI component test in tests/UnitTests/UI/CommandPaletteTests.cs"
```

## Notes
- [P] tasks = different files, no dependencies
- Verify tests fail before implementing
- Commit after each task
- Avoid: vague tasks, same file conflicts

## Task Generation Rules
*Applied during main() execution*

1. **From Contracts**:
   - Each contract file → contract test task [P]
   - Each endpoint → implementation task
   
2. **From Data Model**:
   - Each MQTT entity (Message, Topic, Connection) → model creation task [P]
   - MQTT protocol interactions → service layer tasks

3. **From User Stories**:
   - Each command scenario → integration test [P]
   - Real-time message handling → performance validation tasks

4. **Ordering**:
   - Setup → Tests → Models → Services → UI Components → Integration → Polish
   - Follow dependency flow: Utils → BusinessLogic → UI

## Validation Checklist
*GATE: Checked by main() before returning*

- [ ] All contracts have corresponding tests
- [ ] All entities have model tasks
- [ ] All tests come before implementation
- [ ] Parallel tasks truly independent
- [ ] Each task specifies exact file path
- [ ] No task modifies same file as another [P] task