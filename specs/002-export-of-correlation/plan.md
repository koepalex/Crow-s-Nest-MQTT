
# Implementation Plan: Fix Correlation Data Export Encoding

**Branch**: `002-export-of-correlation` | **Date**: 2025-09-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-export-of-correlation/spec.md`

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
Fix the base64 encoding issue in correlation data export functionality. Currently, when users execute `:export` or `:copy` commands on correlation data from the metadata table, the output is unexpectedly base64 encoded instead of preserving the readable format displayed in the UI. This affects user workflow as exported correlation data becomes unreadable for external use.

## Technical Context
**Language/Version**: C# (.NET - latest stable version)
**Primary Dependencies**: WPF/Avalonia UI framework, MQTTnet library, Serilog logging
**Storage**: In-memory message buffers with configurable limits, file export capabilities
**Testing**: xUnit or NUnit for unit/integration testing
**Target Platform**: Cross-platform desktop (Windows, Linux, macOS)
**Project Type**: single - desktop application with modular architecture
**Performance Goals**: Handle 10,000+ messages/second, <100ms command response time
**Constraints**: Memory management for 1GB+ message buffers, real-time UI responsiveness
**Scale/Scope**: Enterprise MQTT monitoring tool, command-driven interface with GUI support

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Initial Check (Pre-Research)
**I. User-Centric Interface**: ✅ PASS - Feature affects existing `:export` and `:copy` commands which are discoverable through command palette and accessible via colon-prefixed syntax.

**II. Real-Time Performance**: ✅ PASS - This is a data formatting fix that doesn't impact message processing throughput. Export/copy operations are user-initiated and won't block real-time message streaming.

**III. Test-Driven Development**: ✅ PASS - Will write failing tests first for export/copy functionality, then implement fixes. Integration tests needed for command processing.

**IV. Modular Architecture**: ✅ PASS - Changes will be isolated to export/copy logic in BusinessLogic layer, with UI commands triggering the functionality. No circular dependencies introduced.

**V. Cross-Platform Compatibility**: ✅ PASS - String encoding/formatting is platform-agnostic in C#. No platform-specific code required for this data formatting fix.

**Technical Standards**: ✅ PASS - Feature doesn't affect MQTT protocol compliance. Command response time will remain well under 100ms for export/copy operations.

**Quality Assurance**: ✅ PASS - Acceptance criteria defined in spec (readable format matching metadata table display). Manual testing approach documented.

### Post-Design Check (After Phase 1)
**I. User-Centric Interface**: ✅ PASS - Design maintains command-driven approach. Export/copy contracts support existing command syntax without changes.

**II. Real-Time Performance**: ✅ PASS - Design shows format processing in BusinessLogic layer without affecting UI responsiveness. Async operations maintain non-blocking performance.

**III. Test-Driven Development**: ✅ PASS - Contract specifications include comprehensive test requirements. TDD approach documented in quickstart scenarios.

**IV. Modular Architecture**: ✅ PASS - Design respects UI → BusinessLogic → Utils flow. Clear interface contracts defined for each layer without circular dependencies.

**V. Cross-Platform Compatibility**: ✅ PASS - Design includes platform-specific considerations for clipboard/file handling while maintaining consistent behavior.

**Technical Standards**: ✅ PASS - Performance targets maintained (<100ms commands). MQTT protocol unchanged. UTF-8 encoding ensures cross-platform compatibility.

**Quality Assurance**: ✅ PASS - Comprehensive quickstart test scenarios defined. Manual and automated testing approaches documented with clear success criteria.

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
- Generate tasks from Phase 1 design docs (contracts, data model, quickstart)
- Create contract test tasks for export-command.md and copy-command.md [P]
- Generate data model tasks for CorrelationData entity [P]
- Extract test scenarios from quickstart.md as integration test tasks
- Implementation tasks to fix base64 encoding in export/copy pipeline

**Ordering Strategy**:
- TDD order: Failing contract tests first, then implementation to make them pass
- Dependency order:
  1. Contract tests (can run in parallel) [P]
  2. Data model updates (if needed)
  3. BusinessLogic layer fixes (export/copy formatting)
  4. UI layer integration (command handlers)
  5. Integration tests from quickstart scenarios
- Mark [P] for parallel execution (independent files/components)

**Specific Task Categories Expected**:
1. **Contract Tests** (2-3 tasks): Export command contract test, Copy command contract test
2. **Research Tasks** (1-2 tasks): Identify base64 encoding source, analyze current export pipeline
3. **Implementation Tasks** (3-4 tasks): Fix export formatting, fix copy formatting, update format consistency logic
4. **Integration Tasks** (2-3 tasks): UI command integration, cross-platform testing, quickstart validation
5. **Validation Tasks** (1-2 tasks): Performance testing, acceptance criteria verification

**Estimated Output**: 12-15 numbered, ordered tasks in tasks.md (smaller scope than typical features due to focused fix)

**Key Dependencies to Consider**:
- Export/copy functionality location in current codebase
- Metadata table display format implementation (reference for correct format)
- Existing command processing pipeline
- File I/O and clipboard service implementations

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
