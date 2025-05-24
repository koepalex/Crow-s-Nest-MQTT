Below is a comprehensive specification that reflects the current understanding of the implemented system, based on available code definitions. It outlines the requirements, architecture, data handling, error handling, and a suggested testing plan.

---

## 1. Overview

We are building a cross-platform MQTT v5 client application with a graphical user interface using AvaloniaUI. The primary focus is to **subscribe** to topics (defaulting to `#`), **monitor** messages, and **visualize** their details in a structured, user-friendly way. The tool is intended to run on **Windows, Linux, and macOS** and be packaged as self-contained tool.

### Key Points
- **MQTT v5** client (via `MQTTnet`).
- Subscribes to a configurable topic (defaults to `#`) with **QoS 1** by default.
- Cross-platform (Windows, Linux, macOS) using **AvaloniaUI**, with a **WasmApp** target.
- Focus on message handling and visualization.
- **Dark mode** by default (common AvaloniaUI capability, to be confirmed in UI implementation).
- Keyboard navigation.
- Expandable with advanced features in the future.

---

## 2. Requirements

### 2.1 Functional Requirements

1.  **MQTT Connection**
    *   **Broker Settings**:
        *   Hostname and Port (managed by `MqttConnectionSettings` and `SettingsData`).
        *   Advanced options: Client ID, Keepalive Interval, Clean Session, Session Expiry Interval (covered by `MqttConnectionSettings` and `SettingsData`).
    *   **Subscription**:
        *   Subscribes to a configurable topic (e.g., `#`) at QoS 1 by default (handled by `MqttEngine`).
    *   **Reconnect**:
        *   Automatically reconnect if disconnected (handled by `MqttEngine.ReconnectAsync`).
        *   Notifications on failure via `IStatusBarService` and `AppLogger`.

2.  **Topic & Message Display**
    *   **Topic List Panel**:
        *   Lists topics with an incremental message count (`NodeViewModel` with `MessageCount`, displayed in `MainViewModel.TopicNodes`).
        *   Expansion/collapse behavior is a UI implementation detail.
    *   **Message History Panel**:
        *   Displays a timestamped list of received messages for the selected topic (`MainViewModel.SelectedTopicMessages` of `MessageViewModel`).
        *   Allows pausing/resuming UI updates (`MainViewModel.TogglePause`, `IsPaused`).
        *   Allows global reset/clear (`MainViewModel.ClearHistory` calling `IMqttService.ClearAllBuffers`).

3.  **Message Details**
    *   **Metadata**:
        *   `MqttApplicationMessage` properties (ContentType, CorrelationData, UserProperties, PayloadFormatIndicator, Retain, MessageExpiryInterval).
        *   MessageID and Timestamp are associated with messages (e.g., via `BufferedMqttMessage` or `IdentifiedMqttApplicationMessageReceivedEventArgs`).
    *   **Payload Display**:
        *   **Raw** text or **highlighted JSON** (syntax highlighting managed by `MainViewModel.GuessSyntaxHighlighting`, raw view via `MainViewModel.SwitchPayloadView`).
        *   JSON tree view provided by `JsonViewerViewModel`.
        *   *(Speculative/Future: Image rendering, payload diffing - not confirmed in current definitions).*

4.  **Data Handling**
    *   **Ring Buffer**:
        *   Implemented with `TopicRingBuffer` per topic, used by `MqttEngine`.
        *   Oldest messages replaced when full.
        *   *(Speculative: Configurable max size per topic - `TopicRingBuffer` likely takes a size parameter or has a constant, but specific configurability from settings isn't confirmed).*
    *   **Storage**:
        *   Currently in-memory.
        *   *(Future: Option to serialize to local DB - not in current definitions).*

5.  **Search & Filtering**
    *   **Global Search/Input**:
        *   Input via `MainViewModel.InputText`, processed by `CommandParserService`.
        *   Topic filtering is implemented (`MainViewModel.ApplyTopicFilter`).
        *   *(Speculative: Fuzzy matching, contextual searches like `@correlation-data:` - specific implementation details not confirmed).*

6.  **Interactive JSON Element View**
    *   **Tree-view** of JSON payload provided by `JsonViewerViewModel`.
    *   *(Speculative/Future: Clicking elements to add them to a live-updating table - not confirmed).*

7.  **Command System via Global Search Bar**
    *   **Colon-prefixed commands** parsed by `CommandParserService`, dispatched by `MainViewModel`.
    *   Implemented Commands:
        *   `:connect [hostname:port] [clientid] ...`: `MainViewModel.ConnectToMqttBroker`.
        *   `:disconnect`: `MainViewModel.DisconnectFromMqttBroker`.
        *   `:settings`: `MainViewModel.OpenSettings`.
        *   `:export [topic] [format] [path]` / `:export-all [format] [path]`: `MainViewModel.Export` using `IMessageExporter`.
        *   `:clear`: `MainViewModel.ClearMessageHistory`.
        *   `:help [command]`: `MainViewModel.DisplayHelpInformation`.
    *   Ephemeral/inline error messages via `IStatusBarService`.
    *   *(Speculative/Future: `:diagnostic`, `:verify`, `:stats` - not confirmed as commands).*

8.  **User Interactions**
    *   **Keyboard Usage**:
        *   Input handled by `MainViewModel`.
    *   **UI Controls**:
        *   Button to copy payload (`MainViewModel.CopyPayloadToClipboardAsync`).
        *   Pause/resume toggle for message view (`MainViewModel.TogglePause`).
        *   Interval-based UI updates (`MainViewModel.StartTimer`, `UpdateTick`).
        *   Copy selected message details (`MainViewModel.CopySelectedMessageDetails`).

9.  **Error Handling & Diagnostics**
    *   **Transient Notifications**:
        *   Displayed via `IStatusBarService` for command errors or MQTT connection failures.
    *   **Logging**:
        *   Detailed logs via `AppLogger`.
        *   *(Speculative: Dedicated Diagnostics View UI - not confirmed, but logs are available).*

10. **Logging & Metrics**
    *   Logging via `AppLogger` (custom static logger).
    *   *(Speculative/Future: OpenTelemetry conformance, exportable logs/metrics - not confirmed).*

11. **Packaging & Distribution**
    *   Intended as a **global tool** (e.g., via `dotnet tool`).
    *   *(Speculative/Future: Distribution via winget, brew, apt; built-in self-update mechanism - not confirmed).*

---

## 3. Architecture

### 3.1 High-Level Components

1.  **Core MQTT Engine** (`src/Businesslogic`)
    *   `MqttEngine` manages connection, subscriptions, message reception, reconnection.
    *   Publishes events for new messages, errors.
2.  **Message Buffer & Storage** (`src/Utils`, `src/Businesslogic`)
    *   `TopicRingBuffer` for in-memory buffering per topic.
3.  **GUI Layer** (`src/UI`)
    *   Built with **AvaloniaUI**.
    *   `MainViewModel` is central to UI logic.
    *   Provides panels for topics, message history, details.
    *   Handles search input, commands, keyboard interactions.
4.  **Command & Search Parser** (`src/Businesslogic/Services`)
    *   `CommandParserService` interprets user input.
    *   Executes actions or initiates searches.
5.  **Diagnostics & Logging** (`src/Utils`, `src/UI/Services`)
    *   `AppLogger` for logging.
    *   `IStatusBarService` for status messages.

### 3.2 Data Flows

1.  **Message Ingestion**: MQTT Engine (`MqttEngine`) → Message Buffer (`TopicRingBuffer`) → UI Notification (`MainViewModel` events).
2.  **Search/Filter/Command**: User Input (`MainViewModel.InputText`) → `CommandParserService` → Action dispatch in `MainViewModel` or UI update.
3.  **Exporting**: Command → `MainViewModel.Export` → `IMessageExporter` → File.

---

## 4. Data Handling

1.  **Ring Buffer Implementation** (`TopicRingBuffer`)
    *   Each topic has a ring buffer. Oldest messages removed when full.
    *   *(Speculative: Max size configurability - current implementation detail of `TopicRingBuffer` not fully exposed by definitions).*
2.  **JSON Handling**
    *   `JsonViewerViewModel` for tree-view. `MainViewModel` for syntax highlighting.

---

## 5. UI/UX Details

1.  **Layout** (based on `MainViewModel` properties)
    *   Topic list panel (`TopicNodes`).
    *   Message history panel (`SelectedTopicMessages`).
    *   Message details panel (`SelectedMessageText`, `JsonPayloadViewer`).
    *   Search/Command bar (`InputText`).
2.  **Dark Theme**: AvaloniaUI capability, assumed default.
3.  **Keyboard Bindings**: General input handling in `MainViewModel`. Specific Vim-like bindings need confirmation.
4.  **Payload Rendering**: Raw text, highlighted JSON (`MainViewModel`, `JsonViewerViewModel`).
    *   *(Speculative: Image rendering, diff mode - not confirmed).*
5.  **Pause/Resume**: Implemented in `MainViewModel`.
6.  **Interval-Based Updates**: Implemented in `MainViewModel`.

---

## 6. Keyboard and Command System

1.  **Global Search/Command Bar**: `MainViewModel.InputText` and `CommandParserService`.
2.  **Commands**: See section 2.1.7 for implemented commands.
3.  **Clipboard & Saving**:
    *   Copy payload: `MainViewModel.CopyPayloadToClipboardAsync`.
    *   Copy message details: `MainViewModel.CopySelectedMessageDetails`.
    *   *(Speculative: `:w` to save current message - not confirmed).*

---

## 7. Logging and Diagnostics

1.  **Logging**: `AppLogger` provides logging capabilities.
    *   *(Speculative: OpenTelemetry conformance, exportable logs from UI - not confirmed).*
2.  **Error Notifications**: Via `IStatusBarService`.

---

## 8. Error Handling

1.  **Command Errors**: `CommandResult` and `IStatusBarService`.
2.  **MQTT Connection/Subscription Errors**: `MqttEngine` handles reconnection; `IStatusBarService` for notifications.
3.  **Ring Buffer Overflows**: `TopicRingBuffer` removes oldest messages.

---

## 9. Performance Considerations

1.  **High Message Volume**: Strategies include interval-based UI updates (`MainViewModel.UpdateTick`), pause/resume (`MainViewModel.IsPaused`), and efficient data structures (`TopicRingBuffer`).
2.  **Memory Management**: `TopicRingBuffer` manages message retention.

---

## 10. Packaging & Distribution

1.  **Global Tool Approach**: Project structure supports this (e.g., `dotnet tool`).
2.  *(Speculative/Future: Self-Update Mechanism - not confirmed).*

---

## 11. Testing Plan

A thorough testing approach should validate both **functionality** and **performance**:

1.  **Unit Tests**
    *   Core MQTT engine logic (`MqttEngine`).
    *   Ring buffer operations (`TopicRingBuffer`).
    *   Command parsing (`CommandParserService`).
    *   JSON parsing/viewing logic (`JsonViewerViewModel`).
    *   ViewModel logic (`MainViewModel`, `SettingsViewModel`).
2.  **Integration Tests**
    *   End-to-end test with a local MQTT broker.
    *   Validate subscription and message display.
    *   Check metadata extraction.
    *   Confirm ring buffer rollover, pause/resume.
3.  **UI Tests** (using Avalonia testing tools or manual)
    *   Verify panel interactions, theme, search bar, command execution.
    *   Keyboard navigation basics.
    *   Rendering of JSON payloads.
4.  **Performance Tests**
    *   High-throughput scenarios.
    *   UI responsiveness with interval updates.
    *   Memory usage.
5.  **Error Handling Tests**
    *   Simulate broker disconnections, invalid commands.
    *   Confirm status notifications and logging.
6.  **Cross-Platform Validation**: Windows, Linux, macOS, Wasm.

---

## 12. Future Extensions (Out of Scope for Initial Version, unless otherwise specified by current implementation)

- **Security**: Username/password or certificate-based authentication, TLS/SSL.
- **Advanced Search**: Fuzzy matching, contextual search operators.
- **Enhanced Payload Handling**: Image rendering, payload diffing.
- **Advanced UI**: Interactive JSON table, dedicated diagnostics view.
- **More Commands**: `:verify`, `:stats`, `:diagnostic` (as a direct command).
- **Advanced Keyboard**: Full Vim-like bindings (`:w`, `yy`).
- **Logging**: OpenTelemetry conformance, UI for log viewing/export.
- **Distribution**: Self-update, broader package manager support.
- **Topic-Specific Settings**: Different ring buffer sizes or QoS levels per topic.
- **Light Theme Toggle**.
- **Plugin System**.

---

## Conclusion

This specification outlines the **core requirements, architecture, data handling strategy, UI design, and testing plan** for the cross-platform MQTT v5 monitoring tool, updated to align with the current understanding of its implementation from code definitions.

If you have any questions or need clarifications, please reach out. Otherwise, this document should serve as an updated foundation for the project.
