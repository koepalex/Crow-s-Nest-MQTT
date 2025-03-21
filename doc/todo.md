# TODO Checklist

A comprehensive list of tasks derived from the specification. Use this document as a roadmap to ensure all features are implemented and tested.

---

## 1. Project Setup & Architecture

- [ ] **Select Cross-Platform Framework**  
  - [ ] Decide on UI framework (Qt, Electron, .NET MAUI, etc.)  
  - [ ] Establish project structure (core, UI, tests, etc.)

- [ ] **Basic Project Configuration**  
  - [ ] Set up build scripts for Windows, Linux, macOS  
  - [ ] Configure CI/CD pipelines (optional, but recommended)  
  - [ ] Add initial dependencies (MQTT library, JSON parser, etc.)

- [ ] **Logging & Diagnostics**  
  - [ ] Integrate OpenTelemetry-compatible logging  
  - [ ] Decide on log formats (JSON, CSV, or both)  
  - [ ] Implement diagnostics module (expose logs, metrics)

---

## 2. MQTT Client & Connection

- [ ] **Core MQTT Engine**  
  - [ ] Implement MQTT v5 connect/disconnect  
  - [ ] Handle session timeout, keepalive, clean session vs. session expiry  
  - [ ] Automatically reconnect on failure  
  - [ ] Capture detailed error info (reason string, error code)  

- [ ] **Subscription & Message Handling**  
  - [ ] Subscribe to `#` with QoS 1  
  - [ ] Verify subscription callbacks  
  - [ ] Expose events to the rest of the application (e.g., new message event)

- [ ] **Advanced Settings**  
  - [ ] UI for broker hostname/port (basic)  
  - [ ] UI for client ID, keepalive, session options (advanced)  
  - [ ] Persist all settings (reuse on next startup)

---

## 3. Data Storage & Ring Buffer

- [ ] **Ring Buffer Per Topic**  
  - [ ] Implement in-memory ring buffer structure  
  - [ ] Track total size (default 10MB per topic)  
  - [ ] Overwrite oldest messages when full  
  - [ ] Display memory usage in the UI  

- [ ] **Optional Persistent Storage**  
  - [ ] Integrate DuckDB/SQLite as a backend (toggle in advanced settings)  
  - [ ] Mirror ring buffer logic in persistent DB  
  - [ ] Clean up oldest messages to respect size limits

---

## 4. User Interface

### 4.1 Layout & Panels

- [ ] **Main Window Layout**  
  - [ ] Left panel: topic tree (collapsed by default for performance)  
  - [ ] Right panel split:  
    - [ ] Message history (list of timestamps, partial payloads)  
    - [ ] Message details (metadata, full payload, diff view)

- [ ] **Dark Mode Theme**  
  - [ ] Implement dark theme styling  
  - [ ] Ensure color contrast for text, icons, and backgrounds

- [ ] **Resizing & Repositioning**  
  - [ ] Make panels resizable  
  - [ ] Store user layout preferences if desired (stretch goal)

### 4.2 Topic Tree & Message History

- [ ] **Topic Tree**  
  - [ ] Show topic names with message counters  
  - [ ] Expand/collapse nodes for subtopics  
  - [ ] Only update counts for visible (expanded) branches to reduce overhead

- [ ] **Message History**  
  - [ ] Display list of messages (timestamp, partial payload preview)  
  - [ ] Implement pause/resume UI updates (with icon/badge)  
  - [ ] Global reset/clear to empty ring buffers

### 4.3 Message Details

- [ ] **Metadata Display**  
  - [ ] Message ID  
  - [ ] Content-Type  
  - [ ] Correlation-Data  
  - [ ] User-Properties  
  - [ ] Payload-Format-Indicator  
  - [ ] Retain Flag  
  - [ ] Message-Expiry-Interval  
  - [ ] Timestamp

- [ ] **Payload Rendering**  
  - [ ] Raw text view  
  - [ ] JSON highlighting (if content-type is JSON)  
  - [ ] Image rendering (JPEG/PNG) with zoom and save options  
  - [ ] Diff mode (compare with previous message on same topic)

- [ ] **Copying Payload/Metadata**  
  - [ ] Buttons to copy payload, topic, or full message as JSON

### 4.4 Interactive JSON Element View

- [ ] **JSON Tree-View**  
  - [ ] Parse JSON payload into a tree structure  
  - [ ] Allow user to click elements to add them to a live-updating table

- [ ] **Live-Updating Table**  
  - [ ] Shows last known value for each selected JSON path  
  - [ ] Handle missing fields gracefully (display “N/A” or blank)

### 4.5 Search & Commands

- [ ] **Global Search**  
  - [ ] Fuzzy matching on topics, message payloads  
  - [ ] Contextual searches (e.g., `@correlation-data:`)  
  - [ ] Implement performance optimizations for large data sets

- [ ] **Command System**  
  - [ ] Colon-prefixed commands (auto-completion)  
  - [ ] `:connect`, `:disconnect`, `:settings`, `:diagnostic`  
  - [ ] `:export [topic]`, `:export-all`  
  - [ ] `:verify` (check message-id gaps), `:stats` (messages/sec, avg size)

### 4.6 Keyboard Shortcuts (Vim-like)

- [ ] **Navigation**  
  - [ ] `h, j, k, l` for navigation  
  - [ ] `/` to jump to search bar  
  - [ ] `:w` to save current message  
  - [ ] `yy` to copy current message as JSON  
  - [ ] Ensure standard arrow keys and mouse interactions also work

- [ ] **Error Notifications**  
  - [ ] Transient toast messages on command errors or MQTT failures  
  - [ ] Quick actions: “Retry” or “Jump to Diagnostics”

---

## 5. Logging & Diagnostics

- [ ] **OpenTelemetry Integration**  
  - [ ] Record logs for connection events, errors, performance metrics  
  - [ ] Expose logs in diagnostics view  
  - [ ] Export logs as CSV/JSON

- [ ] **Diagnostics View**  
  - [ ] Filter by severity (info, warning, error)  
  - [ ] Show command errors, MQTT connection issues  
  - [ ] Access via `:diagnostic` or dedicated UI button

---

## 6. Performance & Resource Management

- [ ] **Interval-Based UI Updates**  
  - [ ] Refresh visible data every 1 second by default  
  - [ ] Make interval configurable under advanced settings

- [ ] **Pause/Resume Mechanism**  
  - [ ] Continue buffering incoming messages  
  - [ ] Display “paused” state with message count in background

- [ ] **Memory Usage**  
  - [ ] Monitor ring buffer sizes per topic  
  - [ ] Log or alert if usage is near limits (optional debug feature)

---

## 7. Packaging & Distribution

- [ ] **Global Tool Packaging**  
  - [ ] Provide installation instructions (e.g., dotnet tool install, npm install -g, etc.)  
  - [ ] Avoid admin/root privileges

- [ ] **Self-Update Mechanism**  
  - [ ] Check for new versions at startup or on demand  
  - [ ] Notify user of available updates  
  - [ ] (Optional) Automated download and install

---

## 8. Testing & QA

### 8.1 Unit Tests

- [ ] **MQTT Core**  
  - [ ] Connection, reconnection logic  
  - [ ] Subscription to `#`, message reception events  
  - [ ] Error handling (session expiry, keepalive)

- [ ] **Ring Buffer**  
  - [ ] Insert messages, handle overflow correctly  
  - [ ] Respect 10MB limit (configurable)  
  - [ ] Verify oldest messages are removed

- [ ] **Command Parsing**  
  - [ ] Correctly handle colon-prefixed commands  
  - [ ] Fuzzy search logic for invalid commands

- [ ] **JSON Parsing & Diff**  
  - [ ] Validate correct parsing for JSON payloads  
  - [ ] Confirm diff logic is accurate

### 8.2 Integration Tests

- [ ] **Local MQTT Broker**  
  - [ ] Connect, subscribe, receive test messages  
  - [ ] Check message display and ring buffer rollover

- [ ] **UI Interaction**  
  - [ ] Simulate user selecting topics, viewing details, toggling diff mode  
  - [ ] Pause/resume, reset/clear ring buffer

### 8.3 Performance Tests

- [ ] **High Volume**  
  - [ ] Test thousands of topics, hundreds of thousands of messages  
  - [ ] Confirm UI remains responsive with 1-second update interval  
  - [ ] Check memory usage under load

### 8.4 Error Handling Tests

- [ ] **Simulated Broker Disconnection**  
  - [ ] Validate reconnection logic and error toasts  
  - [ ] Confirm diagnostics logs the error reason

- [ ] **Invalid Commands**  
  - [ ] Show transient toast with “Retry” or “Jump to Diagnostics”  
  - [ ] Confirm logs are captured in diagnostics

### 8.5 Cross-Platform Validation

- [ ] **Windows**  
  - [ ] Verify .exe or relevant binary runs without admin privileges  
  - [ ] Check UI and logging paths

- [ ] **Linux**  
  - [ ] Confirm .AppImage or other packaging approach  
  - [ ] Validate no root privileges required

- [ ] **macOS**  
  - [ ] Test .dmg or .pkg alternative (or .NET single file)  
  - [ ] Confirm all UI elements render correctly

---

## 9. Future Enhancements (Out of Scope for Initial Release)

- [ ] **TLS/SSL Support**  
- [ ] **Username/Password or Certificate-Based Auth**  
- [ ] **Topic-Specific Settings** (QoS, ring buffer size, etc.)  
- [ ] **Light Theme Toggle**  
- [ ] **Plugin System** for custom actions on messages

---

## 10. Completion Criteria

- [ ] **All Core Features** are implemented (MQTT connection, ring buffer, UI, commands).  
- [ ] **Basic Performance Goals** are met (responsive UI under heavy load).  
- [ ] **Cross-Platform Builds** pass smoke tests on Windows, Linux, macOS.  
- [ ] **Test Suite** (unit, integration, performance) passes with no major issues.  
- [ ] **Documentation** (help references, keyboard shortcuts, usage instructions) is up-to-date.

---

Use this checklist as a living document. Mark tasks as complete, add notes or references, and adjust priorities as needed throughout development.
