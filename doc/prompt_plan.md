
## 1. Overall Blueprint

**Project Overview:**  
Build a cross-platform MQTT v5 client application with a graphical user interface. The application subscribes to all topics (`#` at QoS 1), handles high message volumes, and provides structured visualization, a Vim-like keyboard interface, and a command/search system. The application is designed to be packaged as a global tool across Windows, Linux, and macOS.

**High-Level Phases:**

1. **Project Setup & Environment Initialization**  
   - Establish project structure and configuration.
   - Setup a unit-testing framework.
   - Create a minimal cross-platform GUI skeleton.

2. **MQTT Engine Implementation**  
   - Develop the core module for MQTT connection, subscription to `#`, and reconnection.
   - Integrate unit tests for connection, message handling, and error cases.

3. **Message Buffer & Storage**  
   - Build an in-memory ring buffer per topic.
   - Handle overflow and replacement logic.
   - Write tests to ensure correctness of the buffer operations.

4. **GUI Implementation**  
   - Create UI panels for topics, message history, and message details.
   - Implement dark theme, keyboard navigation, and interval-based UI updates.
   - Integrate MQTT engine with GUI components.

5. **Advanced Features**  
   - Implement command and search parser with fuzzy matching.
   - Create an interactive JSON tree view for payloads.
   - Support image rendering, diff mode, clipboard, and file saving.
   - Integrate diagnostics, logging, and error notifications.

6. **Packaging & Distribution**  
   - Package the app as a global tool (single binary if possible).
   - Integrate a self-update mechanism.

7. **Final Integration & Testing**  
   - Wire together all components.
   - Perform end-to-end tests (including high-throughput scenarios and cross-platform tests).

---

## 2. Iterative Breakdown into Prompts

Below are the iterative prompts for a code-generation LLM. Each prompt is designed to produce small, testable increments, and every step wires the new functionality into the existing project.

---

### **Prompt 1: Project Setup and Initial Structure**

```text
# Prompt 1: Project Setup and Initial Structure

Create a new project structure for our MQTT monitoring tool. The project should include:
- A main application entry point.
- Separate directories for the MQTT engine, GUI components, utilities, and tests.
- A basic configuration file (e.g., config.json or similar) for future settings.
- A README outlining the project purpose and instructions for running tests.

Please generate a scaffold with placeholder modules (files) that set up this structure. Also, add a basic unit test setup with an example test that always passes.
```

---

### **Prompt 2: MQTT Engine – Connection and Subscription**

```text
# Prompt 2: MQTT Engine – Connection and Subscription

Develop a module for the MQTT engine that:
- Connects to a given MQTT broker using MQTT v5.
- Subscribes to the wildcard topic "#" at QoS 1.
- Includes basic reconnect logic on disconnect.
- Emits events or callbacks for new messages and errors.

Also, write a unit test that mocks the MQTT broker to verify that:
- The connection is established.
- The subscription to "#" is performed.
- Reconnection logic is triggered on simulated disconnect.

Ensure that the module is testable and that all new code is integrated with the project setup from Prompt 1.
```

---

### **Prompt 3: Message Buffer – Ring Buffer Implementation**

```text
# Prompt 3: Message Buffer – Ring Buffer Implementation

Create a module for an in-memory ring buffer to store messages per topic. The ring buffer should:
- Have a configurable maximum size (e.g., 10MB per topic).
- Automatically remove the oldest messages when the buffer reaches its limit.
- Provide methods to insert a new message and to retrieve messages.

Include unit tests for:
- Inserting messages and maintaining the size limit.
- Retrieving messages.
- Correct behavior when the buffer overflows.

Integrate this module with the MQTT engine such that incoming messages are stored in the appropriate ring buffer.
```

---

### **Prompt 4: Basic GUI Skeleton with Panels**

```text
# Prompt 4: Basic GUI Skeleton with Panels

Develop a basic cross-platform GUI skeleton for the application. The GUI should include:
- A left panel to list topics (initially empty).
- A right panel split into two sections: one for message history and one for message details.
- A top search/command bar that will later accept commands and search queries.
- A dark theme layout by default.

Wire this GUI skeleton into the main application, and add basic navigation (e.g., keyboard or button navigation) to switch between panels. Also, add a unit test (or integration test script) that verifies the GUI loads correctly and displays all panels.
```

---

### **Prompt 5: Integrating MQTT Engine with GUI**

```text
# Prompt 5: Integrating MQTT Engine with GUI

Integrate the MQTT engine with the GUI:
- When a new message is received via the MQTT engine, update the topics list on the left panel.
- Display the message (with a timestamp) in the message history panel when a topic is selected.
- Ensure that the UI updates at a configurable interval (default 1 second) to reflect new messages without freezing the interface.

Include tests or logging that confirms the integration is working (e.g., simulated message injection and UI update).
```

---

### **Prompt 6: Advanced Features – Command Parser and Interactive JSON View**

```text
# Prompt 6: Advanced Features – Command Parser and Interactive JSON View

Develop two additional modules:
1. A command parser that:
   - Processes colon-prefixed commands (e.g., :connect, :disconnect, :export, :diagnostic, etc.).
   - Performs fuzzy matching for searches when the colon is not used.
   - Returns appropriate responses or triggers actions.

2. An interactive JSON view:
   - Parses JSON payloads from messages.
   - Renders a tree-view for the JSON data.
   - Allows clicking on elements to add them to a live-updating table of last known values.

Integrate these features with the existing GUI:
- Wire the command parser to the top search/command bar.
- Make sure that JSON messages in the message details panel switch to the interactive view when applicable.

Add tests to simulate command input and validate JSON parsing and rendering.
```

---

### **Prompt 7: Additional GUI Enhancements – Image Rendering, Diff, and Clipboard**

```text
# Prompt 7: Additional GUI Enhancements – Image Rendering, Diff, and Clipboard

Enhance the GUI by adding:
- Image rendering for messages with content-type image/jpeg or image/png, including zoom and save options.
- A diff mode that compares the current payload with the previous message for the same topic.
- Clipboard functionality: allow copying of the current message (with metadata) as JSON and provide a command (e.g., :w to save to file).

Integrate these enhancements into the existing GUI panels, ensuring that each feature is wired into the message details view. Include tests or scripts to simulate image payloads, diff comparisons, and clipboard operations.
```

---

### **Prompt 8: Diagnostics, Logging, and Error Handling**

```text
# Prompt 8: Diagnostics, Logging, and Error Handling

Develop modules to handle:
- Logging and diagnostics: record events (connection events, errors, and performance metrics) in an OpenTelemetry-compliant format.
- Transient toast notifications for errors and command failures.
- A dedicated diagnostics view in the GUI that can be opened via a command (e.g., :diagnostic) to display logs and error details.

Wire these modules into both the MQTT engine and the GUI so that:
- Errors in connection or commands trigger toast notifications.
- The diagnostics view is updated in real time with the latest logs.

Include tests that simulate errors and verify that logs are captured and that the diagnostics view displays the correct information.
```

---

### **Prompt 9: Packaging, Self-Update Mechanism, and Final Integration**

```text
# Prompt 9: Packaging, Self-Update Mechanism, and Final Integration

Prepare the application for packaging and distribution:
- Create build scripts or configuration files for building a single-binary distribution or a global tool (via npm, dotnet tool, etc.).
- Implement a self-update mechanism that, on startup or on user request, checks for a new version and notifies the user if an update is available.
- Wire the self-update notifications into the diagnostics or settings panel.

Finally, ensure that all modules (MQTT engine, message buffer, GUI, command parser, diagnostics, etc.) are fully integrated and perform an end-to-end test:
- Simulate message flow from a local MQTT broker.
- Verify UI updates, command responses, error handling, and logging.

Include final integration tests that cover the overall functionality.
```

---

Each prompt is self-contained yet builds incrementally on the previous work. By following these steps, you can ensure early testing, incremental progress, and strong integration without any orphaned code.
