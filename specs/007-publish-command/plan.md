# Implementation Plan: `:publish` Command

## Technical Context

### Current State
- **Command System**: 27 commands in `CommandType` enum, parsed by `CommandParserService`, dispatched by `MainViewModel.DispatchCommand()`
- **Publish API**: `IMqttService.PublishAsync()` supports topic, payload (string/byte[]), QoS, retain — but NO V5 properties
- **MqttEngine**: `MqttApplicationMessageBuilder` used internally, only basic properties wired
- **AvaloniaEdit**: Already in the project — used for raw payload display with TextMate syntax highlighting
- **Own Messages**: No mechanism to distinguish published from received messages
- **UI Pattern**: Settings panel uses `IsSettingsVisible` toggle, embedded overlay — but this feature needs a separate `Window`
- **Testing**: xUnit 3, NSubstitute, embedded broker via `MqttBrokerFixture`, Avalonia headless tests

### Architecture Decisions

1. **Separate Window**: Use an Avalonia `Window` for the publish dialog (not an overlay panel) to achieve true non-modal behavior and multi-monitor support
2. **New ViewModel**: `PublishViewModel` as a standalone ViewModel, receiving `IMqttService` via constructor injection
3. **Extended Publish API**: Add `PublishAsync(MqttPublishRequest)` overload to `IMqttService`/`MqttEngine` that accepts a full V5 property model
4. **Own Message Tracking**: Add `IsOwnMessage` flag to `IdentifiedMqttApplicationMessageReceivedEventArgs` — set when client ID matches the engine's own ID
5. **Publish History**: JSON file at `%LocalAppData%\CrowsNestMqtt\publish-history.json`, managed by `IPublishHistoryService`

## Design Overview

### New Files

| File | Purpose |
|------|---------|
| `src/BusinessLogic/Models/MqttPublishRequest.cs` | DTO for publish with all V5 properties |
| `src/BusinessLogic/Services/IPublishHistoryService.cs` | Interface for publish history persistence |
| `src/BusinessLogic/Services/PublishHistoryService.cs` | Implementation: load/save/query publish history |
| `src/UI/ViewModels/PublishViewModel.cs` | ViewModel for publish window |
| `src/UI/Views/PublishWindow.axaml` | Publish window XAML layout |
| `src/UI/Views/PublishWindow.axaml.cs` | Publish window code-behind |
| `src/UI/Services/IFileAutoCompleteService.cs` | Interface for `@` file autocomplete |
| `src/UI/Services/FileAutoCompleteService.cs` | File system scanning for autocomplete |
| `tests/UnitTests/ViewModels/PublishViewModelTests.cs` | Unit tests for PublishViewModel |
| `tests/UnitTests/Services/PublishHistoryServiceTests.cs` | Unit tests for history service |
| `tests/UnitTests/Services/FileAutoCompleteServiceTests.cs` | Unit tests for file autocomplete |
| `tests/UnitTests/BusinessLogic/PublishCommandParserTests.cs` | Tests for `:publish` command parsing |
| `tests/UnitTests/UI/PublishWindowTests.cs` | Avalonia headless tests |
| `tests/integration/PublishIntegrationTests.cs` | End-to-end publish with embedded broker |
| `tests/contract/PublishCommandContractTests.cs` | Contract tests for publish behavior |

### Modified Files

| File | Changes |
|------|---------|
| `src/BusinessLogic/Commands/CommandType.cs` | Add `Publish` enum value |
| `src/BusinessLogic/Services/CommandParserService.cs` | Add `:publish` parsing with `@` detection |
| `src/BusinessLogic/IMqttService.cs` | Add `PublishAsync(MqttPublishRequest)` overload |
| `src/BusinessLogic/MqttEngine.cs` | Implement V5-aware publish + own-message tracking |
| `src/UI/ViewModels/MainViewModel.cs` | Add `PublishCommand`, window management, own-message flag |
| `src/UI/Views/MainView.axaml` | Add Publish button in toolbar |
| `src/UI/Views/MainView.axaml.cs` | Keyboard shortcut for `Ctrl+Shift+M` |
| `src/MainApp/Program.cs` | Wire up new services (PublishHistoryService, FileAutoCompleteService) |
| `src/UI/Services/KeyboardNavigationService.cs` | Add `Ctrl+Shift+M` shortcut handling |

## Task Breakdown

### Phase 1: Core Model & API (Foundation)

**T01: Create MqttPublishRequest model**
- New file: `src/BusinessLogic/Models/MqttPublishRequest.cs`
- Properties: Topic, Payload (byte[]), PayloadText (string?), QoS, Retain, ContentType, PayloadFormatIndicator, ResponseTopic, CorrelationData, MessageExpiryInterval, UserProperties
- Default values: QoS=1, Retain=false, MessageExpiryInterval=0

**T02: Extend IMqttService with V5-aware publish**
- Add `Task<MqttPublishResult> PublishAsync(MqttPublishRequest request, CancellationToken ct)` to `IMqttService`
- Create `MqttPublishResult` record (Success, ReasonCode, ErrorMessage)

**T03: Implement V5-aware publish in MqttEngine**
- Implement the new `PublishAsync(MqttPublishRequest)` in `MqttEngine`
- Use `MqttApplicationMessageBuilder` with all V5 properties:
  `.WithContentType()`, `.WithPayloadFormatIndicator()`, `.WithResponseTopic()`,
  `.WithCorrelationData()`, `.WithMessageExpiryInterval()`, `.WithUserProperty()`
- Track own client ID for message identification

**T04: Add own-message tracking**
- Add `IsOwnMessage` property to `IdentifiedMqttApplicationMessageReceivedEventArgs`
- In `ProcessMessageBatchInternal()`, check if `ClientId` matches own client ID
- Pass through to `MessageViewModel` for UI display

### Phase 2: Command Parsing

**T05: Add `Publish` to CommandType enum**
- Add `/// <summary> Publish a message to a topic. </summary> Publish,` before `Unknown`

**T06: Implement `:publish` parsing in CommandParserService**
- Parse variants:
  - `:publish` → open dialog
  - `:publish topic` → open dialog with topic
  - `:publish topic "text"` → direct publish
  - `:publish topic @filepath` → file publish
- Detect `@` prefix for file references
- Validate topic is not empty for direct publish
- Return `ParsedCommand` with arguments array: `[topic?, payload?, isFileRef?]`

### Phase 3: Publish Window UI

**T07: Create PublishViewModel**
- Reactive properties: Topic, PayloadText, QoS, Retain, ContentType, PayloadFormatIndicator, ResponseTopic, CorrelationData, MessageExpiryInterval, UserProperties (ObservableCollection)
- Commands: PublishCommand (Alt+P), ClearCommand, LoadFileCommand, AddUserPropertyCommand, RemoveUserPropertyCommand
- File autocomplete: `FileAutoCompleteSuggestions` property updated on `@` input
- Syntax highlighting: auto-detect from ContentType, expose `PayloadSyntaxHighlighting`
- Publish history: bind to `IPublishHistoryService`
- Validation: disable publish when disconnected or topic empty
- Build `MqttPublishRequest` from current fields and call `IMqttService.PublishAsync()`

**T08: Create PublishWindow XAML**
- Non-modal `Window` with layout:
  - Topic TextBox (with selected topic default)
  - AvaloniaEdit TextEditor (line numbers, syntax highlighting, editable)
  - Collapsible V5 Properties panel (QoS dropdown, Retain toggle, ContentType, PayloadFormatIndicator, ResponseTopic, CorrelationData, MessageExpiryInterval, UserProperties table)
  - Publish History ComboBox/dropdown
  - Action bar: Publish (Alt+P), Clear, Load File buttons
- Window properties: `ShowInTaskbar=false`, `CanResize=true`, min size ~500x400
- Keyboard: `Escape` closes, `Ctrl+Enter` publishes, `Alt+P` publishes

**T09: Create FileAutoCompleteService**
- Interface `IFileAutoCompleteService` with `GetSuggestions(string partialPath, int maxResults)`
- Implementation scans filesystem relative to CWD or absolute paths
- Returns `List<FileAutoCompleteSuggestion>` (path, isDirectory, size, extension)
- Debounced scanning (avoid excessive filesystem calls on every keystroke)

**T10: Implement `@` file autocomplete in command line**
- Extend `CommandAutoCompleteBox` or `MainView.axaml.cs` to detect `@` in command input
- Show file path suggestions as autocomplete items
- On selection, replace `@partial` with `@fullpath`

### Phase 4: Integration with MainViewModel

**T11: Wire up publish command dispatch in MainViewModel**
- Add `ReactiveCommand<Unit, Unit> OpenPublishWindowCommand`
- Add `PublishWindow` management (open/close/toggle)
- In `DispatchCommand()`, handle `CommandType.Publish`:
  - No args → open publish window
  - With topic → open window with topic pre-filled
  - With topic + payload → direct publish via `IMqttService.PublishAsync()`
  - With topic + `@file` → load file, direct publish

**T12: Add Publish button to toolbar**
- Add button in `MainView.axaml` toolbar (after ExportAllButton)
- Icon: send/arrow-up icon (PathIcon with custom SVG data)
- Disabled when not connected
- Tooltip: "Open Publish Window (Ctrl+Shift+M)"

**T13: Register keyboard shortcuts**
- `Ctrl+Shift+M` → toggle publish window (in `KeyboardNavigationService` or `MainView.axaml.cs`)
- Forward `Alt+P` from main window if publish window is open
- Handle focus management between main window and publish window

### Phase 5: Own Message Display

**T14: Extend MessageViewModel for own-message display**
- Add `IsOwnMessage` property to `MessageViewModel`
- Set from `IdentifiedMqttApplicationMessageReceivedEventArgs.IsOwnMessage`
- Add visual indicator in message history ListBox ItemTemplate (↑ icon + color accent)

**T15: Add sent-message filter**
- Add `:filter :sent` support or UI toggle to show only sent messages
- Extend filter logic in MainViewModel's DynamicData pipeline

### Phase 6: Publish History

**T16: Implement PublishHistoryService**
- `IPublishHistoryService`: `AddEntry()`, `GetHistory()`, `ClearHistory()`, `LoadAsync()`, `SaveAsync()`
- JSON serialization to `%LocalAppData%\CrowsNestMqtt\publish-history.json`
- Circular buffer of last 50 entries
- Each entry: topic, payload, all V5 properties, timestamp

**T17: Integrate publish history into PublishViewModel**
- Populate history dropdown from service
- Selecting entry fills all form fields
- After successful publish, add to history
- Show topic, timestamp, and truncated payload preview in dropdown

### Phase 7: Syntax Highlighting

**T18: Implement auto-detected syntax highlighting in publish editor**
- Map ContentType → TextMate grammar:
  - `application/json` → JSON
  - `application/xml` / `text/xml` → XML
  - `text/html` → HTML
  - fallback → plain text
- React to ContentType field changes and update editor highlighting
- Reuse existing AvaloniaEdit/TextMate infrastructure from MainView

### Phase 8: DI & Wiring

**T19: Wire services in Program.cs**
- Register `IPublishHistoryService` → `PublishHistoryService`
- Register `IFileAutoCompleteService` → `FileAutoCompleteService`
- Pass to `MainViewModel` constructor (or use service locator pattern matching existing code)

### Phase 9: Documentation

**T20: Update command documentation**
- Add `:publish` to README.md command reference
- Add to in-app `:help` output (CommandHelpDetails dictionary)
- Update copilot instructions with `:publish` command syntax

**T21: Create spec-kit tasks.md**
- Generate `specs/007-publish-command/tasks.md` from this plan

### Phase 10: Testing

**T22: Unit tests — CommandParserService**
- Test all `:publish` syntax variants
- Test `@` file reference detection
- Test validation (empty topic, invalid file path)
- Test edge cases (quoted strings, special characters)

**T23: Unit tests — PublishViewModel**
- Test property binding and defaults
- Test publish command execution (mock IMqttService)
- Test validation (disabled when disconnected, empty topic)
- Test syntax highlighting mode switching
- Test file loading
- Test publish history integration

**T24: Unit tests — PublishHistoryService**
- Test add/get/clear operations
- Test persistence (save/load JSON)
- Test circular buffer (50 entry limit)
- Test serialization of all V5 properties

**T25: Unit tests — FileAutoCompleteService**
- Test path resolution (relative/absolute)
- Test directory scanning and filtering
- Test edge cases (non-existent paths, permissions)

**T26: Unit tests — Own message tracking**
- Test `IsOwnMessage` flag set correctly on published messages
- Test display in MessageViewModel
- Test filter for sent messages

**T27: GUI tests (Avalonia headless)**
- Test PublishWindow instantiation
- Test PublishWindow with ViewModel binding
- Test toolbar button presence
- Test keyboard shortcuts registration

**T28: Integration tests with embedded broker**
- Test full publish flow: compose → send → receive → verify
- Test V5 properties round-trip (publish with content-type, verify on receive)
- Test file-based publish
- Test publish history persistence across sessions
- Test own-message detection in received messages

**T29: Contract tests**
- Define publish behavior contracts
- Test MqttPublishRequest → MqttApplicationMessage mapping preserves all V5 properties
- Test publish result codes

## Dependencies Graph

```
T01 ──→ T02 ──→ T03 ──→ T04
              ↘
T05 ──→ T06     T11 ──→ T12 ──→ T13
                  ↑
T07 ──→ T08 ──→ T11
  ↑       ↑
T09 ──→ T10
T16 ──→ T17 ──→ T07
T18 ──→ T07
T19 (after T09, T16)
T14 ──→ T15 (after T04)
T20 (after all impl)
T22-T29 (parallel, after respective impl tasks)
```

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Floating window may not work well on all OS (Wayland/Linux) | Test on Windows first, add fallback to panel mode |
| `@` autocomplete performance on large filesystems | Debounce + limit depth + cache results |
| AvaloniaEdit in separate window may need fresh TextMate registration | Share TextMate installation from App or create per-window |
| Own-message detection unreliable if broker doesn't echo back | Also track locally published message IDs and match by content/timestamp |
| Publish history JSON corruption | Atomic write pattern (write temp → rename) |
