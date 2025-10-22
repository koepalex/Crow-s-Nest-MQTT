
# Implementation Plan: Keyboard Navigation Enhancements

**Branch**: `004-improve-keyboard-navigation` | **Date**: 2025-10-20 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `S:\rc\_priv\Crow-s-Nest-MQTT\specs\004-improve-keyboard-navigation\spec.md`

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
Implement comprehensive keyboard navigation for Crow's Nest MQTT to enable developers to efficiently navigate the topic tree and message history without using the mouse. Primary features include: (1) topic search via `/[term]` in command palette with case-insensitive substring matching, (2) navigation between search results using `n`/`N` keys with wrap-around behavior, (3) message history navigation using vim-style `j`/`k` keys with wrap-around, (4) global keyboard shortcuts (except when typing in command palette), and (5) visual feedback showing search term, match count, and current position in results.

## Technical Context
**Language/Version**: C# (.NET - latest stable version per CLAUDE.md)
**Primary Dependencies**: WPF/Avalonia (UI framework), MQTTnet (MQTT client library), Serilog (structured logging)
**Storage**: In-memory message buffers with configurable limits (existing architecture)
**Testing**: xUnit or NUnit for unit/integration testing (per CLAUDE.md)
**Target Platform**: Cross-platform (Windows, Linux, macOS) via WPF/Avalonia
**Project Type**: Desktop application (single)
**Performance Goals**: <100ms command response time, handle 10,000+ messages/second without UI blocking (per constitution)
**Constraints**: Non-blocking UI during navigation, global keyboard shortcuts with command palette exception, wrap-around navigation at boundaries
**Scale/Scope**: 4 new keyboard commands (`/[term]`, `n`, `N`, `j`, `k`), 3 new UI components (search context manager, visual feedback indicator, keyboard event router)

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**I. User-Centric Interface**: ✅ PASS
- Search triggered via `/[term]` command in command palette (colon-prefix pattern: `:search /[term]` equivalent)
- All navigation operations accessible via keyboard shortcuts (`n`, `N`, `j`, `k`)
- Operations are discoverable and follow established vim-style conventions

**II. Real-Time Performance**: ✅ PASS
- Navigation operates on already-loaded topic tree and message buffers (no additional I/O)
- Search filtering is in-memory operation with O(n) complexity on topic count
- No message stream processing changes; navigation uses existing buffer architecture
- UI updates are event-driven and non-blocking

**III. Test-Driven Development**: ✅ PASS (will enforce in Phase 1)
- Integration tests required for keyboard event routing and focus management
- Unit tests for search logic, navigation state management, and wrap-around behavior
- UI command processing tests for `/[term]` command parsing
- Cross-component tests for topic selection → message history view updates

**IV. Modular Architecture**: ✅ PASS
- Search logic → BusinessLogic layer (topic filtering, match tracking)
- Keyboard event handling → UI layer (event routing, focus detection)
- Navigation state management → BusinessLogic layer (search context, position tracking)
- Visual feedback → UI layer (status bar/indicator components)
- Dependency flow: UI (keyboard events) → BusinessLogic (search/navigation) → Utils (string matching)

**V. Cross-Platform Compatibility**: ✅ PASS
- Keyboard shortcuts work via WPF/Avalonia event abstractions (cross-platform)
- No platform-specific keyboard APIs required
- Case-insensitive string matching uses .NET standard library (culture-invariant)
- Visual feedback via standard UI components (status bar)

**Technical Standards**: ✅ PASS (not applicable / achievable)
- No MQTT 5.0 protocol changes (navigation feature only)
- <100ms command response achievable (in-memory search and navigation)
- No impact on 10k+ msg/sec throughput (read-only operations on existing data)

**Quality Assurance**: ✅ PASS
- 15 acceptance scenarios defined in spec.md (scenarios 1-15)
- Manual testing approach: keyboard interaction testing, visual feedback verification
- Quickstart documentation will be generated in Phase 1

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

## Phase 0: Outline & Research ✅ COMPLETE

**Research Areas Covered**:
1. ✅ Keyboard event handling patterns (WPF/Avalonia PreviewKeyDown)
2. ✅ Case-insensitive string matching (StringComparison.OrdinalIgnoreCase)
3. ✅ Search context state management (Observable BusinessLogic classes)
4. ✅ Visual feedback indicator design (Status bar text binding)
5. ✅ Message history navigation state (Index-based with modulo wrap-around)
6. ✅ Command palette `/` prefix handling (Parser enhancement)
7. ✅ Focus detection for shortcut suppression (Keyboard.FocusedElement API)

**Output**: ✅ [research.md](./research.md)
- All NEEDS CLARIFICATION items resolved
- 7 decisions documented with rationale and alternatives
- Technology choices justified with constitutional alignment
- Performance considerations documented
- Testing strategy outlined

## Phase 1: Design & Contracts ✅ COMPLETE

**Entities Defined** (5 core entities):
1. ✅ SearchContext - search session state with observable navigation
2. ✅ TopicReference - lightweight topic pointers for search results
3. ✅ MessageNavigationState - message history cursor with wrap-around
4. ✅ KeyboardNavigationService - keyboard event coordinator
5. ✅ SearchStatusViewModel - formatted status text provider

**Contracts Generated** (3 service interfaces):
1. ✅ [ITopicSearchService.cs](./contracts/ITopicSearchService.cs) - Topic search and match filtering
2. ✅ [IKeyboardNavigationService.cs](./contracts/IKeyboardNavigationService.cs) - Keyboard event routing and navigation
3. ✅ [ISearchStatusProvider.cs](./contracts/ISearchStatusProvider.cs) - Status bar feedback formatting

**Design Documentation**:
- ✅ [data-model.md](./data-model.md) - 5 entities with properties, behaviors, relationships, validation rules
- ✅ State transitions documented (search lifecycle, message navigation)
- ✅ Data flow diagrams for search, navigation, and UI update flows
- ✅ Performance characteristics (all O(1) operations except initial search O(n))

**Test Scenarios**:
- ✅ [quickstart.md](./quickstart.md) - 10 test scenarios covering all 15 acceptance criteria
- ✅ Edge cases documented (single match, empty history, rapid input)
- ✅ Performance validation criteria (<100ms response, no throughput degradation)

**Output**: ✅ data-model.md, ✅ contracts/ (3 files), ✅ quickstart.md
- Next: Update CLAUDE.md with feature context

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

**Task Generation Strategy**:
1. **Contract Test Tasks** (TDD first - 3 tasks, parallelizable):
   - Write failing tests for ITopicSearchService contract [P]
   - Write failing tests for IKeyboardNavigationService contract [P]
   - Write failing tests for ISearchStatusProvider contract [P]

2. **Entity/Model Creation Tasks** (5 tasks, parallelizable after contracts):
   - Implement SearchContext class (BusinessLogic layer) [P]
   - Implement TopicReference class (BusinessLogic layer) [P]
   - Implement MessageNavigationState class (BusinessLogic layer) [P]
   - Implement KeyboardNavigationService class (UI layer) [P]
   - Implement SearchStatusViewModel class (UI layer) [P]

3. **Service Implementation Tasks** (3 tasks, sequential dependencies):
   - Implement TopicSearchService (depends on SearchContext, TopicReference)
   - Integrate keyboard event routing in main window (depends on KeyboardNavigationService)
   - Implement status bar binding (depends on SearchStatusViewModel)

4. **Integration Test Tasks** (10 tasks from quickstart scenarios):
   - Test: Topic search via command palette
   - Test: Navigate forward through search results (n key)
   - Test: Navigate backward through search results (N key)
   - Test: Message navigation down (j key)
   - Test: Message navigation up (k key)
   - Test: Shortcut suppression in command palette
   - Test: Search with no matches feedback
   - Test: Rapid sequential navigation performance
   - Test: Cross-component state synchronization
   - Test: Visual feedback indicator lifecycle

5. **Command Palette Integration** (2 tasks):
   - Extend command parser to handle `/[term]` syntax
   - Wire search command to TopicSearchService

6. **UI Enhancement Tasks** (2 tasks):
   - Add status bar search indicator component
   - Implement focus detection logic

**Ordering Strategy**:
- Phase A (Parallel): Contract tests (tasks 1-3)
- Phase B (Parallel): Entity implementations (tasks 4-8)
- Phase C (Sequential): Service wiring (tasks 9-11)
- Phase D (Parallel): Integration tests (tasks 12-21)
- Phase E (Sequential): Refinement and edge cases (tasks 22-25)

**Estimated Task Count**: 25 tasks total
- 11 tasks parallelizable [P]
- 14 tasks with dependencies
- TDD order enforced: Tests written before implementations

**Dependency Graph**:
```
Contract Tests [1-3] → Entity Implementations [4-8] → Service Wiring [9-11] → Integration Tests [12-21]
                                                    ↓
                                            Command Palette [22-23]
                                                    ↓
                                              UI Enhancements [24-25]
```

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
- [x] Phase 0: Research complete (/plan command) ✅ research.md generated
- [x] Phase 1: Design complete (/plan command) ✅ data-model.md, contracts/, quickstart.md generated
- [x] Phase 2: Task planning complete (/plan command - describe approach only) ✅ 25 tasks outlined
- [x] Phase 3: Tasks generated (/tasks command) ✅ 27 tasks in tasks.md - READY FOR IMPLEMENTATION
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS ✅ All principles validated
- [x] Post-Design Constitution Check: PASS ✅ Design maintains constitutional compliance
- [x] All NEEDS CLARIFICATION resolved ✅ No unknowns remain
- [x] Complexity deviations documented ✅ No violations (Complexity Tracking table empty)

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
