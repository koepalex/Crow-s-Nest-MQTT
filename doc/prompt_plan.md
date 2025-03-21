## 1. Overall Blueprint (Updated)

**Project Overview:**  
Build a cross-platform MQTT v5 client application with a graphical user interface using .NET 9 and Avalonia UI. The application subscribes to all topics (`#` at QoS 1), handles high message volumes, and provides structured visualization, a Vim-like keyboard interface, and a command/search system. The application is designed to be packaged as a global tool on Windows, Linux, and macOS.

**High-Level Phases:**

1. **Project Setup & Environment Initialization**  
   - Establish the .NET 9 project structure.
   - Setup Avalonia UI for cross-platform GUI.
   - Create directories for the MQTT engine, UI components, utilities, and tests.
   - Create a basic configuration file (e.g., appsettings.json) for future settings.
   - Write a README with project purpose and instructions for running tests.

2. **MQTT Engine Implementation**  
   - Develop a .NET module for the MQTT engine using MQTT v5 libraries.
   - Implement connection to the broker, subscribing to `#` at QoS 1, and reconnect logic.
   - Include unit tests to verify connection, message handling, and error cases.

3. **Message Buffer & Storage**  
   - Build an in-memory ring buffer per topic with a configurable maximum size (e.g., 10MB).
   - Remove the oldest messages when the buffer is full.
   - Write tests for insertion, retrieval, and overflow behavior.
   - Integrate the ring buffer with the MQTT engine.

4. **GUI Implementation with Avalonia UI**  
   - Develop UI panels using Avalonia:
     - Left panel for topic list.
     - Right panel split between message history and message details.
     - Top search/command bar.
   - Implement a dark theme as default and Vim-like keyboard navigation.
   - Use .NET’s timer mechanisms for interval-based UI updates.
   - Write tests (or integration test scripts) to verify that the UI loads correctly.

5. **Advanced Features**  
   - Create a command parser module to process colon-prefixed commands (e.g., :connect, :export, etc.) and fuzzy search for non-prefixed input.
   - Implement an interactive JSON view to parse and render JSON payloads in a tree view.
   - Support image rendering (for JPEG/PNG payloads), diff mode for comparing consecutive messages, clipboard functionality, and file saving.
   - Integrate these into the Avalonia UI.

6. **Diagnostics, Logging, and Error Handling**  
   - Use .NET logging (with OpenTelemetry-compatible format) for connection events, errors, and performance metrics.
   - Implement transient toast notifications and a diagnostics view accessible via commands (e.g., :diagnostic).
   - Write tests simulating errors to verify logging and diagnostics.

7. **Packaging & Distribution**  
   - Configure the project for single-binary distribution as a global tool (via dotnet tool packaging).
   - Implement a self-update mechanism that checks for newer versions on startup or user command.
   - Integrate self-update notifications into the UI.
   - Run final end-to-end tests for overall functionality.

---

## 2. Iterative Breakdown into Prompts (Updated)

Each prompt below now assumes .NET 9 and Avalonia UI as the foundational technologies.

---

### **Prompt 1: Project Setup and Initial Structure (dotnet9 + Avalonia UI)**

```text
# Prompt 1: Project Setup and Initial Structure (dotnet9 + Avalonia UI)

Create a new .NET 9 solution for our MQTT monitoring tool using Avalonia UI. The project should include:
- A main application entry point using Avalonia AppBuilder.
- Separate directories/projects for the MQTT engine, Avalonia UI components, utilities, and tests.
- A basic configuration file (appsettings.json) for future settings.
- A README outlining the project purpose and instructions for running tests.

Generate a scaffold with placeholder modules that set up this structure, and add a basic unit test project with an example test that always passes.
```

---

### **Prompt 2: MQTT Engine – Connection and Subscription**

```text
# Prompt 2: MQTT Engine – Connection and Subscription

Develop a .NET module for the MQTT engine that:
- Connects to an MQTT broker using MQTT v5 libraries available in .NET.
- Subscribes to the wildcard topic "#" at QoS 1.
- Implements reconnect logic on disconnect.
- Emits events or callbacks for new messages and errors.

Also, create a unit test that mocks the MQTT broker to verify that:
- The connection is successfully established.
- Subscription to "#" is executed.
- Reconnection logic is triggered when simulating a disconnect.

Ensure this module is integrated within the solution structure set up in Prompt 1.
```

---

### **Prompt 3: Message Buffer – Ring Buffer Implementation**

```text
# Prompt 3: Message Buffer – Ring Buffer Implementation

Create a .NET module for an in-memory ring buffer that stores messages per topic. The ring buffer should:
- Support a configurable maximum size (e.g., 10MB per topic).
- Automatically remove the oldest messages when the buffer limit is reached.
- Provide methods to insert a new message and to retrieve stored messages.

Include unit tests for:
- Insertion of messages with size constraint handling.
- Correct retrieval of messages.
- Proper behavior when the buffer overflows.

Wire this ring buffer module with the MQTT engine so that incoming messages are stored in the corresponding buffers.
```

---

### **Prompt 4: Basic GUI Skeleton with Avalonia UI Panels**

```text
# Prompt 4: Basic GUI Skeleton with Avalonia UI Panels

Develop a basic Avalonia UI skeleton for the application. The UI should include:
- A left panel for listing topics (initially empty).
- A right panel split into two sections: one for message history and one for message details.
- A top search/command bar for accepting commands and queries.
- A default dark theme.

Set up keyboard navigation (e.g., basic focus management for panels) and interval-based updates using .NET timers. Integrate this UI skeleton into the main application, and include a simple test or integration check to verify that the UI loads and displays all panels correctly.
```

---

### **Prompt 5: Integrating MQTT Engine with Avalonia UI**

```text
# Prompt 5: Integrating MQTT Engine with Avalonia UI

Integrate the MQTT engine with the Avalonia UI:
- Update the left topics panel when a new message is received.
- When a topic is selected, display the message (with a timestamp) in the message history panel.
- Use a .NET timer to update the UI at a configurable interval (default 1 second) without blocking the interface.

Include logging or tests (or create a demo mode) to simulate message injection and verify that the UI updates accordingly.
```

---

### **Prompt 6: Advanced Features – Command Parser and Interactive JSON View**

```text
# Prompt 6: Advanced Features – Command Parser and Interactive JSON View

Develop two additional modules:
1. A command parser that:
   - Processes colon-prefixed commands (e.g., :connect, :disconnect, :export, :diagnostic, etc.).
   - Supports fuzzy matching for non-command search queries.
   - Triggers appropriate actions or returns command responses.

2. An interactive JSON view:
   - Parses JSON payloads and renders them in a tree-view.
   - Allows users to click on JSON elements to add them to a live-updating table of last known values.

Integrate these features with the Avalonia UI:
- Wire the command parser to the top search/command bar.
- Ensure that when a JSON message is selected in the message details panel, the interactive JSON view is displayed.

Include tests that simulate command input and verify JSON parsing and rendering.
```

---

### **Prompt 7: Additional GUI Enhancements – Image Rendering, Diff, and Clipboard**

```text
# Prompt 7: Additional GUI Enhancements – Image Rendering, Diff, and Clipboard

Enhance the Avalonia UI by adding:
- Image rendering for messages with content-type image/jpeg or image/png, including features for zooming and saving.
- A diff mode that compares the current payload with the previous message for the same topic.
- Clipboard functionality to copy the current message (with metadata) as JSON, and a command (e.g., :w) to save the current message to file.

Integrate these enhancements into the message details panel. Include tests or demo scenarios to simulate image payloads, perform diff operations, and validate clipboard and file-saving functionality.
```

---

### **Prompt 8: Diagnostics, Logging, and Error Handling**

```text
# Prompt 8: Diagnostics, Logging, and Error Handling

Develop modules to handle:
- Logging and diagnostics using .NET logging libraries with OpenTelemetry-compatible output.
- Transient toast notifications for errors and command failures.
- A diagnostics view in the Avalonia UI that can be opened via a command (e.g., :diagnostic) to display logs and error details.

Integrate these modules with both the MQTT engine and the GUI so that:
- Connection errors, command errors, and other issues trigger toast notifications.
- The diagnostics view is updated in real-time with the latest log entries.

Write tests to simulate errors and verify that logs and notifications are correctly displayed.
```

---

### **Prompt 9: Packaging, Self-Update Mechanism, and Final Integration**

```text
# Prompt 9: Packaging, Self-Update Mechanism, and Final Integration

Prepare the application for packaging and distribution:
- Create build scripts/configurations for packaging as a global tool (e.g., a dotnet tool) with a single-binary distribution.
- Implement a self-update mechanism that checks for new versions on startup or upon user request, notifying the user of available updates.
- Integrate self-update notifications into the diagnostics or settings panel.

Finally, perform an end-to-end integration test that covers:
- Simulated MQTT message flow using a local broker.
- Complete UI updates including topic, message history, and message details.
- Command processing, error handling, logging, and self-update checks.

Include final integration tests to verify overall functionality across all components.
```
