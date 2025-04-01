# TODO Checklist

A comprehensive list of tasks derived from the specification. Use this document as a roadmap to ensure all features are implemented and tested.

---

## 1. Project Setup & Architecture

- [ ] **Select and Set Up Technology Stack**  
  - [x] Use **.NET 9** as the backend/runtime.
  - [x] Use **AvaloniaUI** for the cross-platform GUI.
  - [x] Establish project structure (Core, UI, Services, Tests, etc.).
  - [ ] Set up build scripts and CI/CD pipelines for Windows, Linux, and macOS.

- [ ] **Dependencies & Tooling**  
  - [x] Add MQTTnet for MQTT v5 support.
  - [ ] Add JSON parsing library (System.Text.Json or Newtonsoft.Json).
  - [ ] Integrate OpenTelemetry for logging and metrics.
  - [ ] Set up SQLite/DuckDB integration (optional persistence).
  - [ ] Configure packaging as a global tool (dotnet tool) and self-update mechanism.

---

## 2. MQTT Client & Connection

- [ ] **Core MQTT Engine**  
  - [x] Implement MQTT v5 connect/disconnect using MQTTnet.
  - [ ] Support basic settings (broker hostname, port).
  - [ ] Support advanced settings: client ID, keepalive, session timeout, clean session vs. session expiry.
  - [ ] Automatically reconnect on failure.
  - [ ] Capture and expose detailed error info (reason string, error code).

- [ ] **Subscription & Message Handling**  
  - [x] Subscribe to `#` with QoS 1.
  - [ ] Handle message callbacks and propagate events to the UI.
  - [ ] Expose events for new messages, errors, and connection status changes.

---

## 3. Data Storage & Ring Buffer

- [ ] **In-Memory Ring Buffer**  
  - [x] Implement a ring buffer for each topic.
  - [x] Set a configurable maximum size (default 10MB per topic).
  - [x] Automatically remove oldest messages when the buffer is full.
  - [ ] Track and display memory usage for each topic in the UI.

- [ ] **Optional Persistent Storage**  
  - [ ] Integrate SQLite or DuckDB for optional message persistence.
  - [ ] Mirror ring buffer logic in persistent storage.
  - [ ] Manage cleanup of old messages to respect size limits.

---

## 4. User Interface

### 4.1 Layout & Panels

- [ ] **Main Window Layout**  
  - [ ] Left Panel: Topic tree (collapsed by default for performance) with message counters.
  - [ ] Right Panel: Split between Message History and Message Details.
  - [ ] Global Search/Command Bar at the top.
  - [ ] Diagnostics window accessible via command or button (hidden by default).

- [ ] **Theme & Styling**  
  - [ ] Implement a default dark mode using AvaloniaUI theming.
  - [ ] Ensure UI elements (panels, buttons, text) are styled for high contrast and clarity.

- [ ] **Resizing & Repositioning**  
  - [ ] Allow panels to be resizable and repositionable.
  - [ ] Save user layout preferences if possible.

### 4.2 Topic Tree & Message History

- [ ] **Topic Tree**  
  - [ ] Display topic names with real-time message counters.
  - [ ] Implement expand/collapse functionality for subtopics.
  - [ ] Optimize performance: only update visible branches.

- [ ] **Message History Panel**  
  - [ ] List messages for the selected topic with timestamps and payload previews.
  - [ ] Implement a global pause/resume feature for UI updates.
  - [ ] Show paused state, including number of messages buffered and ring-buffer usage.
  - [ ] Provide a global reset/clear button to reset counters and clear buffers.

### 4.3 Message Details

- [ ] **Metadata Display**  
  - [ ] Display message metadata: Message ID, content-type, correlation-data, user-properties, payload-format-indicator, retain flag, message-expiry-interval, timestamp.
  
- [ ] **Payload Rendering**  
  - [ ] Render payload as raw text or highlighted JSON (auto-detect based on content-type).
  - [ ] For JPEG/PNG content-type, render images with zoom and save functionality.
  - [ ] Implement diff mode to compare the current payload to the previous message on the same topic.
  
- [ ] **Copying & Exporting**  
  - [ ] Add buttons to copy payload, topic, or the entire message (with metadata) as JSON to the system clipboard.

### 4.4 Interactive JSON Element View

- [ ] **JSON Tree-View**  
  - [ ] Parse JSON payloads into a navigable tree.
  - [ ] Enable clicking on tree elements to add them to a live-updating table.

- [ ] **Live-Updating Table**  
  - [ ] Display selected JSON elements with the last known value.
  - [ ] Handle cases where not all messages contain the same elements.

### 4.5 Search & Command System

- [ ] **Global Search Bar**  
  - [ ] Implement fuzzy matching for topics and messages.
  - [ ] Support context-based search filters (e.g., `@correlation-data:`, `@message-id:`, etc.).

- [ ] **Command System (Colon-Prefixed)**  
  - [ ] Support commands: `:connect`, `:disconnect`, `:settings`, `:diagnostic`.
  - [ ] Support export commands: `:export [topic]`, `:export-all`.
  - [ ] Support additional commands: `:verify` (check for message-id gaps), `:stats` (display messages/second and average size).
  - [ ] Provide auto-completion for commands.
  - [ ] On error, display transient toast notifications with "Retry" and "Jump to Diagnostics" options.

### 4.6 Keyboard Shortcuts (Vim-like)

- [ ] **Navigation & Commands**  
  - [ ] Implement standard vim navigation: `h, j, k, l` for movement.
  - [ ] `/` to focus the search bar.
  - [ ] `:w` to save the current message to a file.
  - [ ] `yy` to copy the current message with all metadata as JSON.
  - [ ] Ensure standard arrow keys and mouse interactions are also supported.

---

## 5. Logging & Diagnostics

- [ ] **OpenTelemetry Logging**  
  - [ ] Integrate OpenTelemetry for capturing logs and metrics.
  - [ ] Log connection events, errors, command executions, and performance metrics.
  - [ ] Support exporting logs/metrics as CSV or JSON.

- [ ] **Diagnostics View**  
  - [ ] Develop a diagnostics panel to display detailed logs and errors.
  - [ ] Allow filtering of logs by severity (info, warning, error).
  - [ ] Accessible via the `:diagnostic` command or dedicated UI button.

---

## 6. Performance & Resource Management

- [ ] **UI Update Optimization**  
  - [ ] Set default UI update interval to 1 second for visible content.
  - [ ] Allow configuration of the update interval under advanced settings.

- [ ] **Pause/Resume Functionality**  
  - [ ] Ensure that when paused, messages continue to populate ring buffers but the UI is not updated.
  - [ ] Clearly display paused state, including buffered message count and ring-buffer memory usage.

- [ ] **Memory Management**  
  - [ ] Monitor ring buffer sizes per topic.
  - [ ] Log and alert if memory usage nears configured limits (optional).

---

## 7. Packaging & Distribution

- [ ] **Global Tool Packaging**  
  - [ ] Package the application as a global tool (e.g., via `dotnet tool install -g`).
  - [ ] Ensure no admin/root privileges are required.
  - [ ] Provide instructions for installation via package managers (winget, brew, apt) in future releases.

- [ ] **Self-Update Mechanism**  
  - [ ] Implement a built-in self-update check.
  - [ ] Notify users of available updates.
  - [ ] Optionally support auto-download and install of updates.

---

## 8. Testing & QA

### 8.1 Unit Tests

- [ ] **MQTT Core & Connection Handling**  
  - [ ] Test MQTT connection/disconnection, reconnection logic, and error handling.
  - [ ] Validate proper subscription to `#` and message reception.

- [ ] **Ring Buffer Functionality**  
  - [x] Test insertion, overflow handling, and memory tracking.
  - [x] Validate correct removal of oldest messages when limits are exceeded.

- [ ] **Command Parsing and Execution**  
  - [ ] Test colon-prefixed commands and auto-completion.
  - [ ] Verify fuzzy search accuracy and error handling for invalid commands.

- [ ] **JSON Parsing & Diff Mode**  
  - [ ] Validate JSON parsing from payloads.
  - [ ] Test diff mode functionality between consecutive messages.

### 8.2 Integration Tests

- [ ] **End-to-End MQTT Communication**  
  - [ ] Set up a local MQTT broker (e.g., Mosquitto) for testing.
  - [ ] Verify full message lifecycle: connection, subscription, message reception, and UI update.

- [ ] **UI Interaction Tests**  
  - [ ] Simulate user interactions: topic selection, pausing/resuming UI, command execution.
  - [ ] Validate that keyboard shortcuts and vim-like navigation work as expected.

### 8.3 Performance Tests

- [ ] **High Throughput Scenarios**  
  - [ ] Simulate thousands of topics and hundreds of thousands of messages.
  - [ ] Confirm UI responsiveness with 1-second update intervals.
  - [ ] Monitor memory usage and ring buffer management under load.

### 8.4 Error Handling Tests

- [ ] **Simulated Connection Failures**  
  - [ ] Test reconnection logic and verify detailed error toasts.
  - [ ] Ensure diagnostics view logs all connection errors.

- [ ] **Invalid Command Scenarios**  
  - [ ] Verify transient toast messages for invalid or failed commands.
  - [ ] Test quick-action buttons for retry and diagnostics jump.

### 8.5 Cross-Platform Validation

- [ ] **Windows**  
  - [ ] Ensure the packaged .exe runs without requiring admin privileges.
  - [ ] Validate all UI and functionality aspects.

- [ ] **Linux**  
  - [ ] Test the single-file executable (or AppImage) on various distributions.
  - [ ] Validate UI rendering and file paths.

- [ ] **macOS**  
  - [ ] Confirm the packaged binary runs correctly on macOS.
  - [ ] Verify UI elements and cross-platform consistency.

---

## 9. Future Enhancements (Out of Scope for Initial Release)

- [ ] **Security Enhancements**: TLS/SSL, username/password, or certificate-based authentication.
- [ ] **User Management and Multi-Profile Support.**
- [ ] **Topic-Specific Settings**: Custom ring buffer sizes, QoS configurations.
- [ ] **Light Theme Option**.
- [ ] **Plugin System** for custom message actions.

---

## 10. Completion Criteria

- [ ] **Core Functionality**: MQTT connection, message handling, ring buffer, and UI rendering are fully implemented.
- [ ] **Performance Goals**: Responsive UI under heavy load with proper memory management.
- [ ] **Cross-Platform Compatibility**: Successful smoke tests on Windows, Linux, and macOS.
- [ ] **Testing Suite**: All unit, integration, and performance tests pass with no major issues.
- [ ] **Documentation**: Complete and up-to-date user and developer documentation, including help references and keyboard shortcuts.

---

Use this checklist as a living document to track progress and ensure all components are addressed during development.