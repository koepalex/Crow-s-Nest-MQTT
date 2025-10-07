# Tasks: Goto Response for MQTT V5 Request-Response

**Input**: Design documents from `/specs/003-feat-goto-response/`
**Prerequisites**: plan.md (required), research.md, data-model.md, contracts/

## Execution Flow (main)
```
1. Load plan.md from feature directory ✅
   → Tech stack: C# (.NET), MQTTnet, WPF/Avalonia UI, xUnit, Serilog
   → Structure: UI → BusinessLogic → Utils dependency flow
2. Load optional design documents ✅
   → data-model.md: Request Message, Response Message, Message Correlation, Response Status
   → contracts/: IMessageCorrelationService, IResponseNavigationService, IResponseIconService
   → research.md: MQTTnet library, ReactiveUI, SVG icons, in-memory correlation
3. Generate tasks by category ✅
   → Setup: C# project structure, NuGet packages
   → Tests: Contract tests for all 3 services, integration tests
   → Core: Entity models, service implementations, UI components
   → Integration: MQTT correlation, navigation, icon management
   → Polish: Unit tests, performance validation, cross-platform testing
4. Apply task rules ✅
   → Different files = mark [P] for parallel execution
   → Same file = sequential (no [P] marker)
   → Tests before implementation (TDD methodology)
5. Number tasks sequentially (T001, T002...) ✅
6. Generate dependency graph ✅
7. Create parallel execution examples ✅
8. Validate task completeness ✅
   → All 3 contracts have corresponding tests
   → All 4 entities have model creation tasks
   → All services have implementation tasks
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
- [x] T001 Create MQTT V5 correlation feature project structure following plan.md
- [x] T002 Add NuGet packages: MQTTnet, ReactiveUI, xUnit, Serilog per research.md
- [x] T003 [P] Configure EditorConfig and code analysis for C# correlation feature

## Phase 3.2: Contract Tests First (TDD) ⚠️ MUST COMPLETE BEFORE 3.3
**CRITICAL: These tests MUST be written and MUST FAIL before ANY implementation**
- [x] T004 [P] IMessageCorrelationService contract test in tests/contract/MessageCorrelationServiceTests.cs
- [x] T005 [P] IResponseNavigationService contract test in tests/contract/ResponseNavigationServiceTests.cs
- [x] T006 [P] IResponseIconService contract test in tests/contract/ResponseIconServiceTests.cs
- [x] T007 [P] MQTT V5 correlation-data integration test in tests/integration/CorrelationIntegrationTests.cs
- [x] T008 [P] Response topic navigation integration test in tests/integration/NavigationIntegrationTests.cs
- [x] T009 [P] Icon state transition integration test in tests/integration/IconTransitionTests.cs

## Phase 3.3: Entity Models (ONLY after tests are failing)
- [ ] T010 [P] Request Message entity in src/BusinessLogic/Models/RequestMessage.cs
- [ ] T011 [P] Response Message entity in src/BusinessLogic/Models/ResponseMessage.cs
- [ ] T012 [P] Message Correlation entity in src/BusinessLogic/Models/MessageCorrelation.cs
- [ ] T013 [P] Response Status entity in src/BusinessLogic/Models/ResponseStatus.cs
- [ ] T014 [P] Correlation key and entry records in src/BusinessLogic/Models/CorrelationTypes.cs

## Phase 3.4: Service Implementations
- [ ] T015 MessageCorrelationService implementation in src/BusinessLogic/Services/MessageCorrelationService.cs
- [ ] T016 ResponseNavigationService implementation in src/BusinessLogic/Services/ResponseNavigationService.cs
- [ ] T017 ResponseIconService implementation in src/UI/Services/ResponseIconService.cs
- [ ] T018 [P] Correlation cleanup timer in src/BusinessLogic/Services/CorrelationCleanupService.cs
- [ ] T019 [P] Navigation command registry in src/UI/Commands/ResponseNavigationCommands.cs

## Phase 3.5: UI Components
- [ ] T020 ResponseIconViewModel in src/UI/ViewModels/ResponseIconViewModel.cs
- [ ] T021 [P] Clock icon SVG resources in src/UI/Resources/Icons/ClockIcon.axaml
- [ ] T022 [P] Arrow icon SVG resources in src/UI/Resources/Icons/ArrowIcon.axaml
- [ ] T023 [P] Disabled clock icon SVG resources in src/UI/Resources/Icons/DisabledClockIcon.axaml
- [ ] T024 Icon click handlers and navigation triggers in src/UI/Controls/ResponseIconControl.axaml.cs

## Phase 3.6: Integration & Wiring
- [ ] T025 MQTT message processing integration for correlation-data extraction
- [ ] T026 Real-time icon status updates via ReactiveUI observable collections
- [ ] T027 Command palette integration for ":gotoresponse" commands
- [ ] T028 Topic subscription validation for navigation enablement
- [ ] T029 Cross-platform icon rendering validation (Windows, Linux, macOS)

## Phase 3.7: Performance & Polish
- [ ] T030 [P] Correlation service unit tests in tests/UnitTests/MessageCorrelationServiceTests.cs
- [ ] T031 [P] Navigation service unit tests in tests/UnitTests/ResponseNavigationServiceTests.cs
- [ ] T032 [P] Icon service unit tests in tests/UnitTests/ResponseIconServiceTests.cs
- [ ] T033 [P] High-volume correlation performance test (1000+ messages/second)
- [ ] T034 [P] Memory cleanup validation test with TTL expiration
- [ ] T035 [P] Correlation collision handling test with duplicate correlation-data
- [ ] T036 Manual testing scenarios from quickstart.md validation
- [ ] T037 Cross-platform compatibility verification per plan.md requirements

## Dependencies
**Critical Path**:
- Setup (T001-T003) → Contract Tests (T004-T009) → Entity Models (T010-T014) → Services (T015-T019) → UI (T020-T024) → Integration (T025-T029) → Polish (T030-T037)

**Specific Blocking Dependencies**:
- T004-T009 must FAIL before T010-T014 can begin (TDD requirement)
- T010-T014 (models) before T015-T019 (services that use models)
- T015-T016 (correlation + navigation services) before T017 (icon service that depends on them)
- T015-T019 (all services) before T025-T029 (integration that wires services)
- T021-T023 (icon resources) before T024 (icon controls that use resources)

## Parallel Example
```bash
# Launch T004-T006 contract tests together:
Task: "IMessageCorrelationService contract test in tests/contract/MessageCorrelationServiceTests.cs"
Task: "IResponseNavigationService contract test in tests/contract/ResponseNavigationServiceTests.cs"
Task: "IResponseIconService contract test in tests/contract/ResponseIconServiceTests.cs"

# Launch T010-T014 entity models together:
Task: "Request Message entity in src/BusinessLogic/Models/RequestMessage.cs"
Task: "Response Message entity in src/BusinessLogic/Models/ResponseMessage.cs"
Task: "Message Correlation entity in src/BusinessLogic/Models/MessageCorrelation.cs"
Task: "Response Status entity in src/BusinessLogic/Models/ResponseStatus.cs"

# Launch T021-T023 icon resources together:
Task: "Clock icon SVG resources in src/UI/Resources/Icons/ClockIcon.axaml"
Task: "Arrow icon SVG resources in src/UI/Resources/Icons/ArrowIcon.axaml"
Task: "Disabled clock icon SVG resources in src/UI/Resources/Icons/DisabledClockIcon.axaml"
```

## Notes
- [P] tasks = different files, no shared dependencies
- All contract tests (T004-T009) must fail before implementation begins
- Commit after each task completion for incremental progress
- Follow TDD: Red (failing test) → Green (minimal implementation) → Refactor
- Icon resources use Avalonia AXAML format per research.md decisions

## Task Generation Rules
*Applied during main() execution*

1. **From Contracts**:
   - IMessageCorrelationService → T004, T015, T030
   - IResponseNavigationService → T005, T016, T031
   - IResponseIconService → T006, T017, T032

2. **From Data Model**:
   - Request Message → T010
   - Response Message → T011
   - Message Correlation → T012
   - Response Status → T013

3. **From User Stories**:
   - Clock icon display → T021, T024
   - Arrow navigation → T022, T025, T027
   - Command palette integration → T019, T027

4. **Ordering**:
   - Setup → Tests → Models → Services → UI → Integration → Polish
   - Dependency flow: Utils → BusinessLogic → UI (no Utils tasks needed for this feature)

## Validation Checklist
*GATE: Checked by main() before returning*

- [x] All contracts have corresponding tests (T004-T006 for 3 contracts)
- [x] All entities have model tasks (T010-T014 for 4 entities + types)
- [x] All tests come before implementation (T004-T009 before T010+)
- [x] Parallel tasks truly independent (different files, no shared state)
- [x] Each task specifies exact file path with proper C# conventions
- [x] No task modifies same file as another [P] task
- [x] TDD methodology enforced with failing tests requirement
- [x] Constitutional compliance maintained (command-driven, real-time performance)

## Implementation Notes
- Feature implements MQTT V5 request-response navigation per spec.md requirements
- Uses correlation-data property for message linking per clarifications
- Supports real-time icon updates without UI blocking per plan.md performance goals
- Follows Crow's NestMQTT constitutional principles including command-driven interface
- Cross-platform compatibility via Avalonia UI framework per technical context