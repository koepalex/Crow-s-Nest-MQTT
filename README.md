# Crow's NestMQTT

**Crow’s NestMQTT** is a versatile, cross-platform MQTT GUI client designed to simplify and enhance your messaging experience across Linux, Windows, and macOS. With an intuitive, polished interface, it serves as your lookout—monitoring, managing, and troubleshooting MQTT communications with precision and ease.

Whether you're a seasoned developer or a newcomer to IoT, Crow’s NestMQTT provides robust tools for subscribing to topics, and inspecting payloads in real time. The client offers customizable dashboards, seamless connection management, and advanced filtering options, ensuring you can quickly pinpoint critical data streams. Its cross-platform support means that no matter your operating system, you can harness the power of MQTT to orchestrate reliable and secure message flows. Welcome aboard, and let Crow’s NestMQTT guide you through the vast network of your IoT environment!

## Command Interface

Crow's Nest MQTT provides a command interface (likely accessible via a dedicated input field) for quick actions. Commands are typically prefixed with a colon (`:`). You can quickly access this input field using the `Ctrl + Shift + P` keyboard shortcut.

*   `:connect [<server:port>] [<username>] [<password>]` - Connect to an MQTT broker. If arguments are omitted, connection details are loaded from settings.
*   `:disconnect` - Disconnect from the current MQTT broker.
*   `:export <json|txt> <filepath>` - Export messages to a file in JSON or plain text format. If arguments are omitted, the path and format are loaded from settings.
*   `:filter [regex_pattern]` - Filter messages based on a regex pattern. Clears the filter if no pattern is provided.
*   `:clear` - Clear all messages from the display.
*   `:help [command]` - Show information about available commands.
*   `:copy` - Copy the selected messages to the clipboard.
*   `:pause` - Pause the display of new messages.
*   `:resume` - Resume the display of new messages.
*   `:expand` - Expand all nodes in the topic tree.
*   `:collapse` - Collapse all nodes in the topic tree.
*   `:view <raw|json>` - Set the payload view to either raw text or a formatted JSON tree.
*   `:settings` - Toggle the visibility of the settings panel.
*   `:setuser <username>` - Set the username for MQTT authentication.
*   `:setpass <password>` - Set the password for MQTT authentication.
*   `:setauthmode <anonymous|userpass|enhanced>` - Set the authentication mode.
*   `[search_term]` - Any text entered without a `:` prefix is treated as a search term to filter messages.

## Enhanced Authentication

Crow's Nest MQTT supports Enhanced Authentication, as defined in the MQTT 5.0 specification. This allows for more advanced authentication mechanisms, such as Challenge/Response Authentication.

To use Enhanced Authentication, you need to configure the following settings:

*   **`AuthenticationMethod`**: The name of the authentication method (e.g., `SCRAM-SHA-1`, `K8S-SAT`).
*   **`AuthenticationData`**: The authentication data, which is specific to the chosen authentication method.

You can set the authentication mode to `enhanced` using the `:setauthmode` command:

```
:setauthmode enhanced
```

When connecting to a broker with Enhanced Authentication, the client and broker will exchange authentication data until the authentication process is complete.

