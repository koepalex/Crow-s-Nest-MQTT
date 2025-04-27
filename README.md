# Crow's NestMQTT

**Crow’s NestMQTT** is a versatile, cross-platform MQTT GUI client designed to simplify and enhance your messaging experience across Linux, Windows, and macOS. With an intuitive, polished interface, it serves as your lookout—monitoring, managing, and troubleshooting MQTT communications with precision and ease.

Whether you're a seasoned developer or a newcomer to IoT, Crow’s NestMQTT provides robust tools for subscribing to topics, and inspecting payloads in real time. The client offers customizable dashboards, seamless connection management, and advanced filtering options, ensuring you can quickly pinpoint critical data streams. Its cross-platform support means that no matter your operating system, you can harness the power of MQTT to orchestrate reliable and secure message flows. Welcome aboard, and let Crow’s NestMQTT guide you through the vast network of your IoT environment!

## Command Interface

Crow's Nest MQTT provides a command interface (likely accessible via a dedicated input field) for quick actions. Commands are typically prefixed with a colon (`:`). You can quickly access this input field using the `Ctrl + Shift + P` keyboard shortcut.

*   `:connect [arguments]` - Connect to an MQTT broker. (Arguments can be specified or are loaded from settings).
*   `:disconnect` - Disconnect from the current MQTT broker.
*   `:export [format filepath]` - Export messages to a file. (available formats are json and txt and file path, defaults are loaded from settings).
*   `:filter [regex_pattern]` - Filter displayed messages based on a regex pattern applied to topics or payloads.
*   `:clear` - Clear all displayed messages from the message view.
*   `:help` - Show diagnostic information or help about commands.
*   `:copy` - Copy selected messages to the clipboard.
*   `:pause` - Pause the display of new incoming messages.
*   `:resume` - Resume displaying new incoming messages if paused.
*   `:expand` - Expand all nodes in the topic tree view.
*   `:collapse` - Collapse all nodes in the topic tree view.
*   `:view raw` - Force the selected message's payload to be displayed as raw text.
*   `:view json` - Force the selected message's payload to be displayed as a formatted JSON tree.
*   `[search_term]` - Entering text without a `:` prefix likely performs a search/filter operation (equivalent to `:filter [search_term]` or a dedicated search function).

