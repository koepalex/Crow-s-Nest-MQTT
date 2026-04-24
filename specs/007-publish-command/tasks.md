# Tasks: `:publish` Command Implementation

## Phase 1: Core Model & API (Foundation)

### T01: Create MqttPublishRequest model
- **File**: `src/BusinessLogic/Models/MqttPublishRequest.cs`
- **Action**: Create new DTO class with properties: Topic, Payload (byte[]), PayloadText (string?), QoS (default 1), Retain (default false), ContentType, PayloadFormatIndicator, ResponseTopic, CorrelationData, MessageExpiryInterval, UserProperties (List<MqttUserProperty>)
- **Depends on**: —

### T02: Extend IMqttService with V5-aware publish
- **File**: `src/BusinessLogic/IMqttService.cs`, new `src/BusinessLogic/Models/MqttPublishResult.cs`
- **Action**: Add `Task<MqttPublishResult> PublishAsync(MqttPublishRequest request, CancellationToken ct)` to interface. Create `MqttPublishResult` record with Success, ReasonCode, ErrorMessage.
- **Depends on**: T01

### T03: Implement V5-aware publish in MqttEngine
- **File**: `src/BusinessLogic/MqttEngine.cs`
- **Action**: Implement the new `PublishAsync(MqttPublishRequest)` using `MqttApplicationMessageBuilder` with all V5 properties. Store own client ID for message identification.
- **Depends on**: T02

### T04: Add own-message tracking
- **Files**: `src/BusinessLogic/MqttEngine.cs`, `src/UI/ViewModels/MainViewModel.cs`
- **Action**: Add `IsOwnMessage` to `IdentifiedMqttApplicationMessageReceivedEventArgs`. In `ProcessMessageBatchInternal()`, detect own messages by client ID. Pass to `MessageViewModel`.
- **Depends on**: T03

---

## Phase 2: Command Parsing

### T05: Add `Publish` to CommandType enum
- **File**: `src/BusinessLogic/Commands/CommandType.cs`
- **Action**: Add `/// <summary> Publish a message to a topic. </summary> Publish,` before `Unknown`.
- **Depends on**: —

### T06: Implement `:publish` parsing in CommandParserService
- **File**: `src/BusinessLogic/Services/CommandParserService.cs`
- **Action**: Add case for "publish" in `ParseCommand()`. Parse variants: no args (open dialog), topic only, topic + quoted text, topic + @filepath. Validate topic not empty for direct publish.
- **Depends on**: T05

---

## Phase 3: Publish Window UI

### T07: Create PublishViewModel
- **File**: `src/UI/ViewModels/PublishViewModel.cs`
- **Action**: Create ViewModel with reactive properties for all publish fields, commands (Publish, Clear, LoadFile, AddUserProperty, RemoveUserProperty), file autocomplete support, syntax highlighting mode derived from ContentType, publish history binding, validation logic.
- **Depends on**: T01, T16, T18

### T08: Create PublishWindow XAML
- **Files**: `src/UI/Views/PublishWindow.axaml`, `src/UI/Views/PublishWindow.axaml.cs`
- **Action**: Create non-modal Window with: Topic TextBox, AvaloniaEdit editor (editable, line numbers, syntax highlighting), collapsible V5 properties panel (QoS dropdown, Retain toggle, ContentType, PayloadFormatIndicator, ResponseTopic, CorrelationData, MessageExpiryInterval, UserProperties table), history dropdown, action bar (Publish/Clear/LoadFile buttons). Wire keyboard shortcuts (Escape, Ctrl+Enter, Alt+P).
- **Depends on**: T07

### T09: Create FileAutoCompleteService
- **Files**: `src/UI/Services/IFileAutoCompleteService.cs`, `src/UI/Services/FileAutoCompleteService.cs`
- **Action**: Implement filesystem scanning for `@` autocomplete. `GetSuggestions(partialPath, maxResults)` returns path suggestions. Debounced, depth-limited, with directory/file distinction.
- **Depends on**: —

### T10: Implement `@` file autocomplete in command line
- **Files**: `src/UI/Views/MainView.axaml.cs` or command autocomplete logic
- **Action**: Detect `@` in `:publish` command input, trigger file path autocomplete suggestions. On selection, replace `@partial` with `@fullpath`.
- **Depends on**: T09

---

## Phase 4: Integration with MainViewModel

### T11: Wire up publish command dispatch in MainViewModel
- **File**: `src/UI/ViewModels/MainViewModel.cs`
- **Action**: Add `OpenPublishWindowCommand`, publish window lifecycle management (open/close/toggle, track window instance). In `DispatchCommand()`, handle `CommandType.Publish` with all variants (open dialog, direct publish, file publish).
- **Depends on**: T06, T07, T08

### T12: Add Publish button to toolbar
- **File**: `src/UI/Views/MainView.axaml`
- **Action**: Add button after ExportAllButton with send/arrow-up icon, disabled when not connected, tooltip showing shortcut.
- **Depends on**: T11

### T13: Register keyboard shortcuts
- **Files**: `src/UI/Views/MainView.axaml.cs`, `src/UI/Services/KeyboardNavigationService.cs`
- **Action**: Register `Ctrl+Shift+M` to toggle publish window. Handle focus management between main window and publish window.
- **Depends on**: T11

---

## Phase 5: Own Message Display

### T14: Extend MessageViewModel for own-message display
- **Files**: `src/UI/ViewModels/MainViewModel.cs`, `src/UI/Views/MainView.axaml`
- **Action**: Add `IsOwnMessage` property to `MessageViewModel`. Add visual indicator (↑ icon, blue tint) in message history ListBox ItemTemplate.
- **Depends on**: T04

### T15: Add sent-message filter
- **File**: `src/UI/ViewModels/MainViewModel.cs`
- **Action**: Add filter support for sent-only messages (e.g., `:filter :sent` or UI toggle). Extend DynamicData filter pipeline.
- **Depends on**: T14

---

## Phase 6: Publish History

### T16: Implement PublishHistoryService
- **Files**: `src/BusinessLogic/Services/IPublishHistoryService.cs`, `src/BusinessLogic/Services/PublishHistoryService.cs`
- **Action**: Create service to manage publish history: add, get, clear, load/save from JSON file. Circular buffer of last 50 entries. Atomic write pattern (temp file → rename).
- **Depends on**: T01

### T17: Integrate publish history into PublishViewModel
- **File**: `src/UI/ViewModels/PublishViewModel.cs`
- **Action**: Populate history dropdown, selecting entry fills all form fields, auto-add on successful publish, show preview (topic, timestamp, truncated payload).
- **Depends on**: T07, T16

---

## Phase 7: Syntax Highlighting

### T18: Implement auto-detected syntax highlighting in publish editor
- **File**: `src/UI/ViewModels/PublishViewModel.cs`
- **Action**: Map ContentType → TextMate grammar (JSON, XML, HTML, plain text). React to ContentType changes. Reuse AvaloniaEdit/TextMate setup from existing codebase.
- **Depends on**: —

---

## Phase 8: DI & Wiring

### T19: Wire services in Program.cs
- **File**: `src/MainApp/Program.cs`
- **Action**: Create and pass `PublishHistoryService` and `FileAutoCompleteService` to MainViewModel.
- **Depends on**: T09, T16

---

## Phase 9: Documentation

### T20: Update command documentation
- **Files**: `README.md`, `src/UI/ViewModels/MainViewModel.cs` (CommandHelpDetails)
- **Action**: Add `:publish` to README command reference, in-app `:help` text, and copilot instructions.
- **Depends on**: T11

---

## Phase 10: Testing

### T22: Unit tests — CommandParserService
- **File**: `tests/UnitTests/BusinessLogic/PublishCommandParserTests.cs`
- **Action**: Test all `:publish` syntax variants, `@` detection, validation, edge cases.
- **Depends on**: T06

### T23: Unit tests — PublishViewModel
- **File**: `tests/UnitTests/ViewModels/PublishViewModelTests.cs`
- **Action**: Test property defaults, publish execution (mock IMqttService), validation, syntax highlighting switching, file loading, history integration.
- **Depends on**: T07

### T24: Unit tests — PublishHistoryService
- **File**: `tests/UnitTests/Services/PublishHistoryServiceTests.cs`
- **Action**: Test add/get/clear, persistence, 50-entry limit, V5 property serialization.
- **Depends on**: T16

### T25: Unit tests — FileAutoCompleteService
- **File**: `tests/UnitTests/Services/FileAutoCompleteServiceTests.cs`
- **Action**: Test path resolution, scanning, edge cases (non-existent paths, permissions).
- **Depends on**: T09

### T26: Unit tests — Own message tracking
- **File**: `tests/UnitTests/BusinessLogic/OwnMessageTrackingTests.cs`
- **Action**: Test `IsOwnMessage` flag, MessageViewModel display, sent-message filter.
- **Depends on**: T04, T14

### T27: GUI tests (Avalonia headless)
- **File**: `tests/UnitTests/UI/PublishWindowTests.cs`
- **Action**: Test PublishWindow instantiation, ViewModel binding, toolbar button, keyboard shortcuts.
- **Depends on**: T08, T12

### T28: Integration tests with embedded broker
- **File**: `tests/integration/PublishIntegrationTests.cs`
- **Action**: Full publish flow (compose → send → receive → verify), V5 property round-trip, file publish, history persistence, own-message detection.
- **Depends on**: T03, T11

### T29: Contract tests
- **File**: `tests/contract/PublishCommandContractTests.cs`
- **Action**: Publish behavior contracts, MqttPublishRequest → MqttApplicationMessage mapping, result code contracts.
- **Depends on**: T01, T02
