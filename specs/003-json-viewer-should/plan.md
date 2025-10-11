
# Implementation Plan: JSON Viewer Default Expansion

**Branch**: `003-json-viewer-should` | **Date**: 2025-10-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `S:\rc\_priv\Crow-s-Nest-MQTT\specs\003-json-viewer-should\spec.md`

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
Modify JSON viewer component to display all JSON message payloads in expanded state by default (up to 5 nesting levels) across all application contexts (`:view json` command, message previews, command palette). Users can manually collapse nodes, but state resets when switching messages. No configuration options - always-expanded is the only mode.

## Technical Context
**Language/Version**: C# (.NET - latest stable version per CLAUDE.md)
**Primary Dependencies**: WPF/Avalonia (UI framework), MQTTnet (MQTT client), Serilog (logging)
**Storage**: In-memory message buffers with configurable limits (per CLAUDE.md)
**Testing**: xUnit or NUnit for unit/integration testing
**Target Platform**: Cross-platform (Windows, Linux, macOS)
**Project Type**: Desktop application (single codebase, modular architecture)
**Performance Goals**: <100ms UI response time, handle 10,000+ messages/second without blocking
**Constraints**: UI must remain responsive during JSON expansion, depth limit of 5 levels for auto-expansion
**Scale/Scope**: Single feature affecting JSON viewer component across multiple UI contexts (command viewer, previews, palette)

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**I. User-Centric Interface**: ✅ **PASS** - Feature affects existing `:view json` command (already command-driven). Expansion behavior is automatic, requiring no new commands. Maintains keyboard-driven UX.

**II. Real-Time Performance**: ✅ **PASS** - 5-level depth limit prevents UI blocking. Expansion occurs during message rendering (already async). Large JSON payloads handle gracefully with partial expansion.

**III. Test-Driven Development**: ✅ **PASS** - 7 acceptance scenarios defined in spec. Tests will verify: depth calculation, expansion state management, cross-context consistency, manual collapse functionality, state reset behavior.

**IV. Modular Architecture**: ✅ **PASS** - Changes isolated to UI layer (JSON viewer component). No BusinessLogic changes needed - expansion is presentation logic. Viewer component already independently testable.

**V. Cross-Platform Compatibility**: ✅ **PASS** - WPF/Avalonia handles tree expansion cross-platform. No platform-specific code needed. Behavior identical across Windows, Linux, macOS.

**Technical Standards**: ✅ **PASS** - No MQTT protocol changes. Performance targets achievable: expansion is O(n) limited to 5 levels, <100ms for typical payloads. Async rendering maintains UI responsiveness.

**Quality Assurance**: ✅ **PASS** - 7 acceptance criteria defined. Manual testing: verify expansion in `:view json`, command palette, message previews. Test 1-5 level nesting, 6+ level nesting, large payloads, state reset.

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
The /tasks command will generate an ordered task list following TDD methodology:

1. **Unit Test Tasks** (Foundation):
   - JsonTreeNode model creation with validation rules [P]
   - JsonTreeBuilder.BuildTree() tests for 1-level JSON (failing)
   - JsonTreeBuilder.BuildTree() tests for 5-level JSON (failing)
   - JsonTreeBuilder.BuildTree() tests for 6+ level JSON (failing)
   - JsonTreeBuilder.BuildTree() tests for mixed types (failing)

2. **Implementation Tasks** (Make tests pass):
   - Implement JsonTreeNode model class in Utils/
   - Implement JsonTreeBuilder.BuildTree() method
   - Implement JsonTreeBuilder.BuildTreeRecursive() with depth tracking
   - Implement lazy-loading logic for depth > 5

3. **Integration Test Tasks**:
   - JsonViewerViewModel.LoadJsonAsync() tests (all acceptance scenarios)
   - Cross-context consistency tests (`:view json`, previews, palette)
   - State reset tests (message switching)
   - Performance tests (large JSON, deep nesting)

4. **UI Integration Tasks**:
   - Update JsonViewerViewModel to use JsonTreeBuilder
   - Bind TreeView.IsExpanded to JsonTreeNode.IsExpanded
   - Update message preview component to use JsonTreeBuilder
   - Update command palette JSON display to use JsonTreeBuilder

5. **Manual Testing Tasks**:
   - Execute quickstart.md validation steps
   - Cross-platform testing (Windows/Linux/macOS if applicable)

**Ordering Strategy**:
- TDD order: Tests (unit) → Implementation → Tests (integration) → UI integration
- Dependency order: Models → Utils → ViewModels → Views
- Parallel tasks marked [P] (JsonTreeNode + tests can be done concurrently with other independent units)

**Estimated Output**: 18-22 numbered, dependency-ordered tasks in tasks.md

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
- [x] Phase 3: Tasks generated (/tasks command)
- [x] Phase 4: Core implementation complete (T001-T008)
- [ ] Phase 5: Validation and integration (T009-T017)

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved (via /clarify session)
- [x] Complexity deviations documented (N/A - no violations)

**Artifacts Generated**:
- [x] research.md - Technical decisions and best practices
- [x] data-model.md - Entity definitions and state management
- [x] contracts/JsonTreeBuilder.contract.md - Interface contracts
- [x] quickstart.md - Manual validation scenarios
- [x] CLAUDE.md - Updated with feature context

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
