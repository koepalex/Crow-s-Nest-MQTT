# Copilot Instructions for Crow's NestMQTT

## Build and Test Commands

### Build
```powershell
dotnet build CrowsNestMQTT.slnx --configuration Release
```

### Run All Tests
```powershell
dotnet test --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings --filter "Category!=LocalOnly"
```

### Run Single Test Project
```powershell
# Unit tests
dotnet test tests/UnitTests/UnitTests.csproj

# Integration tests
dotnet test tests/integration/Integration.Tests.csproj

# Contract tests
dotnet test tests/contract/Contract.Tests.csproj
```

### Run Single Test
```powershell
dotnet test tests/UnitTests/UnitTests.csproj --filter "ClassName.TestMethodName"
```

### Generate Coverage Report
```powershell
dotnet test --no-build --configuration Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:"Html;Cobertura;JsonSummary"
```

### Restore Dependencies
```powershell
dotnet restore
```

## Architecture Overview

### High-Level Structure
Crow's NestMQTT is a cross-platform MQTT GUI client built with **Avalonia** (cross-platform UI framework). The application follows a layered architecture with clear separation of concerns:

```
UI Layer (Avalonia)
    ↓
BusinessLogic Layer (MQTT handling, command processing)
    ↓
Utils Layer (Common utilities, shared models)
```

### Projects

- **CrowsNestMqtt.App** - Entry point; sets up DI, logging (Serilog), and initializes the Avalonia application
- **UI** - Presentation layer: Views, ViewModels, Commands, and UI Services using Avalonia/ReactiveUI
- **BusinessLogic** - Domain logic: MQTT engine, command parsing, message processing, export/import operations
- **Utils** - Shared utilities: buffer management, JSON tree building, logging helpers

### Key Components

#### MQTT Engine (BusinessLogic/MqttEngine.cs)
- Manages MQTT client lifecycle and reconnection logic
- Handles high-volume message streams with per-topic ring buffers
- Supports MQTT 5.0 features including enhanced authentication
- Manages message retention and correlation tracking
- `DefaultMaxTopicBufferSize = 1MB` (configurable per-topic)

#### Message Storage (Utils)
- `TopicRingBuffer` - Circular buffer for each topic with configurable size limits
- `BufferedMqttMessage` - Wraps MQTT messages with metadata and unique ID
- `TopicMessageStore` - Manages multi-level topic hierarchy with wildcard support

#### Command Processing (BusinessLogic/Commands)
- `CommandParserService` - Parses colon-prefixed commands (`:connect`, `:filter`, etc.)
- `CommandType` enum - Defines all supported commands
- Commands are executed through the UI Command Processor

#### UI Services (UI/Services)
- Dependency injection pattern for testability
- Services include: topic tree management, keyboard navigation, image/hex viewing, status bar updates
- ViewModels bind to services and expose reactive properties

### Configuration Management
- **Settings Storage** - `AppData\Local\CrowsNestMqtt\` (Serilog logs)
- **Settings Model** - `SettingsData` class with serialization support
- **Buffer Limits** - `TopicBufferLimit` for per-topic memory management
- **Connection Settings** - `MqttConnectionSettings` with TLS and auth mode support

## Command Interface Reference

Crow's NestMQTT is command-driven. Access the command palette with **Ctrl+Shift+P**. All commands use colon-prefixed syntax (`:command`). GUI elements support commands but do not replace them.

### Connection & Authentication Commands

- **`:connect [<server:port>]`** - Connect to MQTT broker. Use format `hostname:port` (e.g., `:connect mqtt.broker.com:1883`). If no arguments provided, uses connection settings from configuration.
- **`:disconnect`** - Disconnect from the current MQTT broker.
- **`:setuser <username>`** - Set MQTT authentication username.
- **`:setpass <password>`** - Set MQTT authentication password.
- **`:setauthmode <anonymous|userpass|enhanced>`** - Set authentication mode.
  - `anonymous` - No credentials required
  - `userpass` - Use username/password authentication
  - `enhanced` - MQTT 5.0 enhanced authentication with method-specific data
- **`:setauthmethod <method>`** - Set enhanced authentication method (e.g., `SCRAM-SHA-1`, `K8S-SAT`).
- **`:setauthdata <data>`** - Set enhanced authentication data (method-specific).
- **`:setusetls <true|false>`** - Enable/disable TLS encryption. When enabled, client allows untrusted certificates.

### Message Viewing & Filtering Commands

- **`:filter [regex_pattern]`** - Filter messages by regex pattern applied to payload. Omit pattern to clear filter.
- **`:view <raw|json|image|video|hex>`** - Switch payload viewer:
  - `raw` - Plain text view
  - `json` - Formatted JSON tree (when content-type is application/json)
  - `image` - Image viewer (when content-type indicates image)
  - `video` - Video player (when content-type indicates video)
  - `hex` - Hexadecimal viewer for binary data
- **`:copy`** - Copy the selected message (including metadata) to clipboard in JSON format.
- **`/[search_term]`** - Search topics by name (case-insensitive). Automatically expands topic tree to show matches. Use `n` to go to next match, `N` (Shift+n) for previous match.

### Topic Management Commands

- **`:deletetopic [<topic-pattern>] [--confirm]`** - Delete (clear) all retained messages from a topic and subtopics.
  - Example: `:deletetopic sensors/temperature` - Clears topic `sensors/temperature` and all subtopics (e.g., `sensors/temperature/living-room`)
  - Omit pattern to use currently selected topic
  - Supports MQTT wildcards: `+` (single level), `#` (multi-level)
  - Publishes empty retained messages to clear broker state
  - Default limit: 500 topics per operation (configurable)
  - Parallel processing for performance; skips topics without publish permission
- **`:expand`** - Expand all topic tree nodes.
- **`:collapse`** - Collapse all topic tree nodes.
- **`:settings`** - Toggle visibility of settings panel (shows connection, auth, export, and buffer limit configuration).

### Message Display Commands

- **`:pause`** - Pause reception of new messages (pause message display updates).
- **`:resume`** - Resume message display after pause.
- **`:clear`** - Clear all messages from the display (does not affect retained messages on broker).

### Export & Data Commands

- **`:export <json|txt> <filepath>`** - Export currently displayed messages to file.
  - Format: `json` or `txt`
  - Example: `:export json /home/user/mqtt-export.json`
  - If arguments omitted, uses format and path from settings
- **`:export all [<json|txt>] [<filepath>]`** - Export all messages from selected topic to single JSON file.
  - Creates array of all messages from topic
  - If format/path omitted, uses settings defaults

### MQTT v5 Request/Response Commands

- **`:gotoresponse`** - Navigate to the response message for currently selected MQTT v5 request message.
  - Shows response topic, correlation ID, and metadata
  - Only works on request messages (messages with `response-topic` header)

### Utility Commands

- **`:help [command]`** - Display help for all commands or specific command. Example: `:help connect`
- **`[search_term]`** - Any text without `:` prefix filters message payloads by search term (not regex; simple substring match).

### Keyboard Shortcuts (complementary to commands)

- **`Ctrl+Shift+P`** - Open command palette
- **`/[term]`** then **`n`/`N`** - Topic search and navigation
- **`j`/`k`** - Vim-style message history navigation (j = down, k = up)

### Command Examples

```
# Connect to broker and set credentials
:connect mqtt.broker.com:1883
:setuser myuser
:setpass mypass

# Filter and view messages
:filter "temperature.*"
:view json

# Export messages
:export all json /tmp/mqtt-dump.json

# Clear retained messages
:deletetopic sensor/+
:deletetopic device/# --confirm

# Find and navigate topics
/temperature
n
N
```

## Code Conventions

### C# Style (from .clinerules-code)
- **Naming**: PascalCase for classes/methods, camelCase for local variables, UPPERCASE for constants
- **Interfaces**: Prefix with `I` (e.g., `IMqttService`, `ICommandProcessor`)
- **Namespaces**: File-scoped with `namespace CrowsNestMqtt.{Layer};`
- **Async/Await**: Use for all I/O operations (MQTT, file operations); call `ConfigureAwait(false)` in library code
- **Null Handling**: Use null-coalescing operators (`??`, `?.`) and pattern matching

### Testing Conventions 
- **TDD Mandatory**: Write failing tests first
- **Framework**: xUnit with `[Fact]` and `[Theory]` attributes
- **Mocking**: MQTTnet client is typically mocked in unit tests
- **Test Organization**: `UnitTests/`, `Integration.Tests/`, `Contract.Tests/`
- **Categories**: Use `[Trait("Category", "LocalOnly")]` to mark tests requiring local MQTT broker
- **Coverage**: Aim for >80% coverage (warnings >60%)

### Dependency Injection
- Registered in `Program.cs` via `ServiceCollection`
- Constructor injection pattern throughout
- Services implement interfaces for loose coupling
- No direct instantiation of MQTT client outside engine

### MQTT-Specific Guidelines
- All MQTT operations are async; use `CancellationToken` for lifecycle management
- Ring buffers prevent unbounded memory growth
- Message correlation uses `CorrelationKey` (topic + correlation ID)
- Enhanced authentication is handled by `EnhancedAuthenticationHandler`
- Graceful reconnection with exponential backoff (details in MqttEngine)

### Command Interface Design
- **All feature commands must be exposed via colon-prefixed commands** (`:connect`, `:filter`, etc.)
- Commands must be discoverable via `Ctrl+Shift+P` command palette
- GUI supports commands but does not replace them
- Parse commands in `CommandParserService`, execute in `ICommandProcessor`
- Update status bar for long-running operations

### Error Handling
- Log errors with context using `ILogger` (Serilog)
- Propagate critical errors to UI via status bar
- Handle MQTT disconnections gracefully with reconnection attempts
- Validate user input before processing (see `ValidationResult` model)

### Avalonia UI Specifics
- Use **ReactiveUI** for MVVM pattern in ViewModels
- Bind properties via `WhenActivated` and `ObservableAsPropertyHelper`
- UI bindings are strongly typed (not string-based)
- Custom controls inherit from `UserControl` or `Control`
- `.axaml` files paired with `.axaml.cs` code-behind (minimal logic)
- ViewLocator auto-resolves Views from ViewModels by naming convention

## Performance Considerations

- **Message Throughput**: Handle 10,000+ messages/second without UI blocking
- **Memory Management**: Ring buffers with 1MB per-topic default; respects configured limits
- **UI Thread**: All UI updates marshal through Avalonia dispatcher
- **GC Optimization**: Timer-based GC collection (see Program.cs) to minimize stalls

## File Exclusions from Testing

Tests marked with `Category!=LocalOnly` filter are excluded from CI. This includes:
- Tests requiring a live MQTT broker
- Tests with external dependencies
- Long-running integration tests

Local developers can run these with: `dotnet test --filter "Category==LocalOnly"`

## Dependencies

Key NuGet packages:
- **Avalonia** - Cross-platform UI framework
- **MQTTnet** - MQTT client library
- **Serilog** - Structured logging
- **ReactiveUI** - MVVM framework for Avalonia
- **xUnit** - Testing framework

See `Directory.Packages.props` for pinned versions.

## Project Files

- **CrowsNestMQTT.slnx** - Solution file (Rider format)
- **global.json** - Specifies .NET 10.0.100 SDK
- **coverlet.runsettings** - Code coverage configuration
- **.editorconfig** - Code style settings (enforced by tools)

## Spec-Kit System (.specify)

This repository uses the **Spec-Kit** system for structured feature development. All features are documented in the `specs/` directory with a standardized template structure.

### Spec-Kit Directory Structure
Each feature gets its own folder (e.g., `specs/001-there-should-be/`) containing:

- **spec.md** - Feature specification with user stories, requirements, acceptance scenarios, edge cases, and clarifications
- **plan.md** - Implementation plan, technical context, constitution check, and design decisions
- **tasks.md** - Ordered task list with dependencies for implementation
- **research.md** - Research findings and unknowns to resolve before implementation
- **quickstart.md** - Quick reference guide for developers implementing the feature
- **data-model.md** - Data structures and models used by the feature
- **contracts/** - Interface definitions and API contracts (.cs or .md files)

### Workflow
1. **spec.md** captures requirements and acceptance criteria
2. **plan.md** designs the solution and validates against project constitution
3. **tasks.md** breaks down work into executable steps
4. Feature implementation references these artifacts

### Key Features Using Spec-Kit
- **001-there-should-be** - `:deletetopic` command for clearing retained messages
- **002-export-of-correlation** - Message export and correlation features
- **003-json-viewer-should** - JSON payload viewer with tree navigation
- **003-feat-goto-response** - MQTT v5 request/response navigation (`:gotoresponse`)
- **004-improve-keyboard-navigation** - Vim-style keyboard shortcuts and topic search
- **006-there-is-already** - Export all messages to JSON file

### Using Spec-Kit as a Copilot
When working on a feature:
1. Read the relevant `specs/{feature}/spec.md` for requirements and context
2. Check `specs/{feature}/plan.md` for design decisions and technical approach
3. Review `specs/{feature}/contracts/` for interface contracts
4. Follow the tasks in `specs/{feature}/tasks.md`
5. Reference `specs/{feature}/quickstart.md` for quick implementation tips

The `.specify/` directory contains templates and metadata used by the spec-kit system (do not edit).

## dotnet Aspire Integration

Crow's NestMQTT supports **dotnet Aspire**, allowing seamless integration into Aspire-orchestrated microservice environments.

### How It Works
When running inside an Aspire application, the client automatically discovers and connects to the MQTT broker endpoint via Aspire environment variables.

### Configuration
Aspire expects:
- **Service Name**: `mqtt`
- **Endpoint Name**: `default`
- **Environment Variable Format**: `services__mqtt__default__0` (contains the endpoint URL, e.g., `mqtt://localhost:42069`)

### Example: Adding Crow's NestMQTT to an Aspire App
```csharp
var mqttBroker = builder
    .AddMQTT("mqtt")  // Define MQTT broker service
    .WithEndpoint("default", e => e.Port = 1883);

var mqttViewerWorkingDirectory = @"path\to\CrowsNestMqtt";
builder
    .AddExecutable("mqtt-client", 
        Path.Combine(mqttViewerWorkingDirectory, "CrowsNestMqtt.App.exe"), 
        mqttViewerWorkingDirectory)
    .WithReference(mqttBroker)      // Reference the MQTT service
    .WaitFor(mqttBroker);            // Wait for broker to be ready
```

### Behavior
- On startup, the application checks for `services__mqtt__default__0` environment variable
- If found, it automatically connects to the broker without requiring manual configuration
- Falls back to settings-based connection if environment variable is not present

### Use Cases
- Local development with docker-compose + Aspire orchestration
- Integration testing with ephemeral MQTT brokers
- Debugging multi-service MQTT architectures

## Commands
**Always follow command-driven development:**
- Every feature must expose colon-prefixed commands (`:connect`, `:filter`, etc.)
- Commands must be discoverable via Ctrl+Shift+P command palette
- GUI elements support but never replace command interface

**Key Commands to Support:**
- `:connect [server:port]` - MQTT broker connection (uses settings if no parameters provided)
- `:setuser <username>` - Set MQTT username
- `:setpass <password>` - Set MQTT password
- `:setauthmode <anonymous|userpass|enhanced>` - Set authentication mode
- `:filter [regex_pattern]` - Message filtering
- `:export <json|txt> <filepath>` - Data export
- `:view <raw|json|image|video|hex>` - Payload viewers
- `:deletetopic [topic-pattern]` - Delete retained messages from topics
- `:settings` - Configuration access

## Important Notes

- **Cross-Platform**: App builds for Windows, Linux, and macOS via GitHub Actions
- **Versioning**: Uses GitVersion for semantic versioning
- **Export Format**: Exports MQTT messages as JSON or plain text; see `extract-payloads.ps1` in `tools/`
- **MQTT 5.0**: Full support for metadata, user properties, and enhanced authentication
- **Feature Documentation**: All features documented in `specs/` with spec-kit system
