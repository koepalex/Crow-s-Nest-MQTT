# Crow's NestMQTT Constitution

## Core Principles

### I. User-Centric Interface
Every feature MUST prioritize developer experience through command-driven interaction. The command palette (Ctrl+Shift+P) serves as the primary interface. All operations MUST be accessible via colon-prefixed commands with clear, memorable syntax. GUI elements support commands but never replace them.

**Rationale**: Developers require efficient, keyboard-driven workflows. Command interfaces provide predictable, scriptable, and discoverable functionality that scales with user expertise.

### II. Real-Time Performance
The application MUST handle high-volume MQTT message streams without blocking the user interface. Message processing, filtering, and display operations MUST remain responsive under load. Buffer limits and pagination MUST prevent memory exhaustion while maintaining real-time visibility.

**Rationale**: MQTT environments often generate thousands of messages per second. Users need immediate feedback and the ability to monitor live systems without performance degradation.

### III. Test-Driven Development (NON-NEGOTIABLE)
All features MUST follow strict TDD methodology: Write failing tests first, implement minimal code to pass, then refactor. Integration tests are mandatory for MQTT protocol interactions, UI command processing, and cross-component communication. No feature ships without comprehensive test coverage.

**Rationale**: MQTT client reliability is critical for production debugging and monitoring. TDD ensures robust protocol handling, prevents regression in message processing, and maintains stability under various broker configurations.

### IV. Modular Architecture
Code MUST be organized into distinct layers: UI (presentation), BusinessLogic (domain logic), and Utils (shared utilities). Dependencies MUST flow inward (UI → BusinessLogic → Utils). No circular dependencies allowed. Each module MUST be independently testable and have clearly defined responsibilities.

**Rationale**: Separation of concerns enables independent testing, reduces coupling, and allows for UI framework changes or business logic reuse. Clear boundaries improve maintainability and team collaboration.

### V. Cross-Platform Compatibility
All features MUST function identically across Windows, Linux, and macOS. Platform-specific code MUST be isolated and abstracted. File paths, keyboard shortcuts, and system integrations MUST adapt to platform conventions while maintaining consistent user experience.

**Rationale**: Developers work across diverse environments. Consistent behavior reduces cognitive load and training overhead while maximizing adoption across development teams.

## Technical Standards

Application MUST comply with MQTT 5.0 specification including enhanced authentication, user properties, and message metadata. C# code MUST follow established conventions: async/await for I/O operations, dependency injection for testability, and structured logging for observability.

Performance targets: Handle 10,000+ messages/second without UI blocking, maintain <100ms response time for user commands, support message buffers up to 1GB per topic with graceful overflow handling.

## Quality Assurance

Every pull request MUST pass automated testing including unit tests (>90% coverage), integration tests with live MQTT brokers, and cross-platform compatibility validation. Manual testing scenarios MUST verify command interface functionality, message filtering accuracy, and export capabilities.

Code reviews MUST verify architectural compliance, performance considerations, and user experience consistency. No feature merges without demonstrating value through quickstart documentation and acceptance criteria validation.

## Governance

This constitution supersedes all other development practices and guidelines. All technical decisions MUST align with stated principles or document justified exceptions in complexity tracking.

Amendments require documentation of rationale, impact assessment across dependent templates, and validation that changes support rather than undermine core values. Use `.github/copilot-instructions.md` for AI-assisted development guidance that complements constitutional requirements.

**Version**: 1.1.0 | **Ratified**: 2026-02-03 | **Last Amended**: 2026-02-03
