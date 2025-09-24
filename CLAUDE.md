# Crow's NestMQTT Development Guidelines

Auto-generated from constitution v1.0.0. Last updated: 2025-09-23

## Active Technologies
- **Language**: C# (.NET)
- **UI Framework**: WPF/Avalonia (cross-platform)
- **MQTT Client**: MQTTnet or similar .NET MQTT library
- **Testing**: xUnit or NUnit for unit/integration testing
- **Logging**: Serilog for structured logging
- **Build**: .NET CLI, MSBuild

## Project Structure
```
src/
├── UI/                   # Presentation layer (commands, views, UI logic)
├── BusinessLogic/        # Domain logic (MQTT handling, message processing)
├── Utils/               # Shared utilities and common code
└── MainApp/             # Application entry point and composition root

tests/
├── UnitTests/           # Unit tests for all components
├── integration/         # MQTT broker integration tests
└── contract/            # API contract tests if applicable
```

## Commands
**Always follow command-driven development:**
- Every feature must expose colon-prefixed commands (`:connect`, `:filter`, etc.)
- Commands must be discoverable via Ctrl+Shift+P command palette
- GUI elements support but never replace command interface

**Key Commands to Support:**
- `:connect [server:port] [username] [password]` - MQTT broker connection
- `:filter [regex_pattern]` - Message filtering
- `:export <json|txt> <filepath>` - Data export
- `:view <raw|json|image|video|hex>` - Payload viewers
- `:settings` - Configuration access

## Code Style
**C# Conventions:**
- Use async/await for all I/O operations (MQTT, file operations)
- Dependency injection for testability and loose coupling
- SOLID principles with clear interface contracts
- Structured logging with contextual information
- ConfigureAwait(false) for library code

**Architecture Flow:**
- Dependencies flow: UI → BusinessLogic → Utils
- No circular dependencies allowed
- Each module independently testable
- Clear separation of concerns

**MQTT-Specific Guidelines:**
- Handle high-volume message streams without UI blocking
- Implement buffer limits and pagination for memory management
- Support MQTT 5.0 features (enhanced auth, user properties, metadata)
- Graceful handling of connection drops and reconnection

## Performance Requirements
- **Message Throughput**: Handle 10,000+ messages/second without UI blocking
- **Command Response**: <100ms response time for user commands
- **Memory Management**: Support message buffers up to 1GB per topic with overflow handling
- **Cross-Platform**: Identical behavior on Windows, Linux, and macOS

## Testing Requirements
**TDD Mandatory:**
- Write failing tests first, then implement
- Integration tests for MQTT protocol interactions
- Cross-component communication tests
- UI command processing tests

**Test Categories:**
- Unit tests: Business logic, validation, utilities
- Integration tests: MQTT broker communication, message processing
- Contract tests: API interfaces if applicable
- Performance tests: Message throughput, memory usage

## Recent Changes
1. Delete Topic Command (Feature 001) - Added `:deletetopic` command for clearing retained messages with parallel processing and error handling
2. Constitution v1.0.0 - Initial project governance and architectural principles
3. Spec-kit integration - Added .specify templates for structured development

<!-- MANUAL ADDITIONS START -->
**Constitutional Compliance Notes:**
- All features must pass constitutional checks before implementation
- Document any principle violations in complexity tracking
- Prefer simplicity; justify any architectural complexity
- Maintain command-driven interface as primary interaction method
<!-- MANUAL ADDITIONS END -->