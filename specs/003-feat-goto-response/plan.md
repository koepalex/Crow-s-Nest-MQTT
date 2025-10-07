# Implementation Plan: Goto Response for MQTT V5 Request-Response

**Branch**: `003-feat-goto-response` | **Date**: 2025-09-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-feat-goto-response/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path ✅
   → Feature spec loaded successfully
2. Fill Technical Context (scan for NEEDS CLARIFICATION) ✅
   → Detect Project Type from context (C# desktop application)
   → Set Structure Decision based on project type
3. Fill the Constitution Check section based on the content of the constitution document. ✅
4. Evaluate Constitution Check section below ✅
   → No violations detected
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → Research artifacts to be generated
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file
7. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 8. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Enable users to navigate from MQTT V5 request messages to their corresponding response messages using visual indicators (clock for pending, arrow for received) with click-to-navigate functionality based on correlation-data matching.

## Technical Context
**Language/Version**: C# (.NET - latest stable version)
**Primary Dependencies**: MQTTnet, WPF/Avalonia UI framework, Serilog logging
**Storage**: In-memory message buffers with configurable limits
**Testing**: xUnit for unit/integration testing
**Target Platform**: Cross-platform (Windows, Linux, macOS)
**Project Type**: C# desktop application - determines source structure
**Performance Goals**: Handle 10,000+ messages/second without UI blocking, <100ms command response time
**Constraints**: <200ms p95 response time, support 1GB message buffers per topic, maintain real-time responsiveness
**Scale/Scope**: MQTT V5 request-response pattern navigation with visual feedback and message correlation

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**I. User-Centric Interface**: ✅ Feature will expose command-driven access via colon-prefixed commands for navigation. All operations will be discoverable through command palette.

**II. Real-Time Performance**: ✅ Feature handles message correlation without blocking UI. Uses in-memory buffers and real-time icon updates for response status.

**III. Test-Driven Development**: ✅ TDD methodology will be followed with failing tests written first. Integration tests for MQTT V5 correlation-data handling and UI navigation.

**IV. Modular Architecture**: ✅ Design respects UI → BusinessLogic → Utils dependency flow. Response correlation logic in BusinessLogic, UI navigation in UI layer.

**V. Cross-Platform Compatibility**: ✅ Uses cross-platform UI framework (Avalonia) and .NET runtime. No platform-specific dependencies.

**Technical Standards**: ✅ Complies with MQTT 5.0 correlation-data specification. Achievable performance targets with in-memory message correlation.

**Quality Assurance**: ✅ Acceptance criteria defined in spec. Manual testing approach will be documented in quickstart.md.

## Project Structure

### Documentation (this feature)
```
specs/003-feat-goto-response/
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
   - Research MQTT V5 correlation-data best practices
   - Research WPF/Avalonia icon rendering approaches
   - Research real-time UI update patterns for message streams

2. **Generate and dispatch research agents**:
   ```
   Task: "Research MQTT V5 correlation-data implementation patterns in .NET"
   Task: "Find best practices for real-time UI updates in WPF/Avalonia applications"
   Task: "Research icon state management for dynamic message status indicators"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all technical approach decisions resolved

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Request Message, Response Message, Message Correlation entities
   - Validation rules for correlation-data matching
   - State transitions for response status (pending → received)

2. **Generate API contracts** from functional requirements:
   - IMessageCorrelationService interface
   - IResponseNavigationService interface
   - Output service contracts to `/contracts/`

3. **Generate contract tests** from contracts:
   - MessageCorrelationServiceTests for correlation-data matching
   - ResponseNavigationServiceTests for topic navigation
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Clock icon display scenarios
   - Arrow navigation scenarios
   - Quickstart validation scenarios

5. **Update agent file incrementally** (O(1) operation):
   - Run `.specify/scripts/powershell/update-agent-context.ps1 -AgentType claude`
     **IMPORTANT**: Execute it exactly as specified above. Do not add or remove any arguments.
   - Add new tech from current plan (correlation-data handling)
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, CLAUDE.md

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
- Load `.specify/templates/tasks-template.md` as base
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- Each service contract → contract test task [P]
- Each entity (Request/Response Message, Correlation) → model creation task [P]
- Each user story → integration test task
- UI tasks for icon rendering and click navigation
- Implementation tasks to make tests pass

**Ordering Strategy**:
- TDD order: Tests before implementation
- Dependency order: Models before services before UI
- Mark [P] for parallel execution (independent files)
- Correlation service before navigation service
- UI components depend on business logic services

**Estimated Output**: 20-25 numbered, ordered tasks in tasks.md

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)
**Phase 4**: Implementation (execute tasks.md following constitutional principles)
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*No constitutional violations detected - section left empty*

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