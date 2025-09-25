
# Implementation Plan: Delete Topic Command

**Branch**: `001-there-should-be` | **Date**: 2025-09-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `S:\rc\_priv\Crow-s-Nest-MQTT\specs\001-there-should-be\spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, `GEMINI.md` for Gemini CLI, `QWEN.md` for Qwen Code or `AGENTS.md` for opencode).
7. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Implement a `:deletetopic` command that clears retained messages from MQTT topics by publishing empty payloads with the retain flag. The command processes topics in parallel for performance, supports configurable limits (default 500 topics), provides minimal UI feedback via status bar, and handles errors gracefully including permission failures and broker disconnections.

## Technical Context
**Language/Version**: C# (.NET 8.0+)
**Primary Dependencies**: MQTTnet (MQTT client library), WPF/Avalonia (UI framework), Serilog (structured logging)
**Storage**: N/A (command operates on live MQTT broker data)
**Testing**: xUnit for unit tests, integration tests with MQTT broker
**Target Platform**: Cross-platform desktop (Windows, Linux, macOS)
**Project Type**: C# Desktop Application - modular architecture with UI → BusinessLogic → Utils dependency flow
**Performance Goals**: Process 500+ topics in parallel, <100ms command response time, handle 10k+ messages/sec throughput
**Constraints**: Must not block UI during operation, configurable topic limits for resource management, maintain real-time MQTT topic tree updates
**Scale/Scope**: Single desktop application, focus on developer-friendly MQTT tooling, command-driven interface

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**I. User-Centric Interface**: ✅ PASS - `:deletetopic` command accessible via command palette (Ctrl+Shift+P), colon-prefixed syntax, optional arguments, uses selected topic context

**II. Real-Time Performance**: ✅ PASS - Parallel processing for performance, configurable limits (500 topics default), non-blocking UI operation, real-time topic tree updates

**III. Test-Driven Development**: ✅ PASS - Will implement TDD with integration tests for MQTT broker interactions, command processing, and cross-component communication

**IV. Modular Architecture**: ✅ PASS - Command in UI layer, MQTT operations in BusinessLogic layer, shared utilities in Utils layer, clear dependency flow

**V. Cross-Platform Compatibility**: ✅ PASS - C# with cross-platform UI framework, MQTT protocol is platform-agnostic, no platform-specific dependencies

**Technical Standards**: ✅ PASS - Uses MQTT retain flag clearing mechanism (publish empty payload with retain=true), supports MQTT 5.0 features, meets <100ms response target

**Quality Assurance**: ✅ PASS - Clear acceptance criteria in spec, comprehensive edge case handling, structured logging for observability

**Post-Design Re-evaluation**: ✅ ALL PRINCIPLES CONFIRMED
- Design maintains command-first interface with ICommandProcessor extension
- Parallel processing architecture preserves real-time performance
- TDD approach with comprehensive contract tests and integration tests
- Clear modular boundaries: UI commands → BusinessLogic services → Utils
- Cross-platform MQTT protocol usage, no platform-specific dependencies
- Performance targets achievable with parallel Task-based implementation

## Project Structure

### Documentation (this feature)
```
specs/[###-feature]/
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
├── BusinessLogic/        # Domain logic and MQTT handling
├── Utils/               # Shared utilities and common code
└── MainApp/             # Application entry point

tests/
├── UnitTests/           # Unit tests for all components
├── integration/         # MQTT broker integration tests
└── contract/            # API contract tests if applicable
```

**Structure Decision**: C# desktop application with modular architecture

## Phase 0: Outline & Research
1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:
   ```
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude`
     **IMPORTANT**: Execute it exactly as specified above. Do not add or remove any arguments.
   - If exists: Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, agent-specific file

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
- Load `.specify/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (contracts, data-model.md, quickstart.md)
- **From Contracts**: IDeleteTopicService → contract test task [P], ICommandProcessor extension → integration test [P]
- **From Data Model**: DeleteTopicCommand, DeleteTopicResult, TopicDeletionFailure → model creation tasks [P]
- **From Quickstart**: Each test scenario → integration test task, performance validation → specific test task
- **Implementation Tasks**: DeleteTopicService, CommandProcessor extension, UI integration, configuration handling

**Ordering Strategy**:
- **TDD Order**: All tests written first (T004-T010), then implementation (T011-T020)
- **Dependency Order**: Utils → BusinessLogic → UI (respects modular architecture)
- **Parallel Opportunities**: Model classes [P], contract tests [P], unrelated components [P]

**Estimated Output**: ~22 numbered, ordered tasks in tasks.md
- Setup: 3 tasks (project structure, dependencies, configuration)
- Tests: 7 tasks (contract tests, integration tests, performance tests)
- Implementation: 8 tasks (models, services, commands, UI integration)
- Polish: 4 tasks (unit tests, documentation, manual testing, cleanup)

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)  
**Phase 4**: Implementation (execute tasks.md following constitutional principles)  
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |


## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none required)

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
