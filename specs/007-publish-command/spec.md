# Feature Spec: `:publish` Command

## Overview

Add a `:publish` command and companion non-modal floating publish window to Crow's NestMQTT, enabling users to compose and send MQTT messages with full MQTT V5 property support, inline file references, syntax-highlighted text editing, and publish history.

## User Stories

1. **As a developer**, I want to publish MQTT messages directly from the monitoring tool so I can test device responses without switching to another client.
2. **As a developer**, I want to reference files with `@path` syntax so I can send pre-prepared payloads from disk.
3. **As a developer**, I want to set MQTT V5 metadata (content-type, response-topic, correlation-data, etc.) so I can test request-response patterns.
4. **As a developer**, I want to see my own published messages in the message history so I can verify what was actually sent.
5. **As a developer**, I want to recall previously published messages so I don't have to re-type them.
6. **As a developer**, I want a floating publish window I can keep open while monitoring so I can work in both views simultaneously.

## Command Syntax

```
:publish [<topic>] [<payload> | @<filepath>]
```

### Variants

| Syntax | Behavior |
|--------|----------|
| `:publish` | Opens the publish dialog (uses selected topic if any) |
| `:publish my/topic` | Opens the publish dialog with topic pre-filled |
| `:publish my/topic "hello"` | Publishes text directly without opening dialog |
| `:publish my/topic @data.json` | Publishes file contents directly without opening dialog |
| `:publish my/topic @./relative/path.bin` | Publishes binary file contents |

### File Reference (`@`)

- `@path` loads a file's content as the payload
- In the **command line**: typing `@` triggers file path autocomplete suggestions
- In the **dialog text editor**: typing `@` triggers file path autocomplete to insert/load content
- Supported file types: any (text files loaded as UTF-8, binary files loaded as byte[])

## Publish Dialog (Floating Window)

### Layout

Non-modal floating window (`Ctrl+Shift+M` to toggle). Main sections:

1. **Topic bar** — text field, defaults to currently selected topic
2. **Payload editor** — AvaloniaEdit with line numbers and auto-detected syntax highlighting
3. **MQTT V5 Properties panel** — collapsible section with:
   - QoS (dropdown: 0, 1, 2; default: 1)
   - Retain (toggle; default: OFF)
   - Content-Type (text field; e.g., `application/json`)
   - Payload Format Indicator (dropdown: Unspecified, UTF-8 Character Data)
   - Response Topic (text field)
   - Correlation Data (text field; hex or UTF-8 input)
   - Message Expiry Interval (numeric; 0 = disabled, default: disabled)
   - User Properties (key-value table with add/remove rows)
4. **Publish History** — dropdown/list of recently published messages for re-use
5. **Action bar** — Publish button (`Alt+P`), Clear, Load from file

### Syntax Highlighting

Auto-detect based on Content-Type field:
- `application/json` → JSON highlighting
- `application/xml` / `text/xml` → XML highlighting
- `text/html` → HTML highlighting
- Fallback → plain text (no highlighting)

### Publish History

- Store last 50 published messages (topic + payload + all V5 properties)
- Persisted to `%LocalAppData%\CrowsNestMqtt\publish-history.json`
- Selecting a history entry populates all fields
- History entries show topic, timestamp, and payload preview

## Own Message Display

### Visual Indicator

- Messages published by this client get a **↑ (up arrow) icon** and/or colored accent (e.g., blue tint) in the message history list
- `IdentifiedMqttApplicationMessageReceivedEventArgs` extended with `IsOwnMessage` property

### Filter Support

- Add ability to filter message history to show only sent messages (via `:filter :sent` or UI toggle)

## Keyboard Shortcuts

| Shortcut | Context | Action |
|----------|---------|--------|
| `Ctrl+Shift+M` | Global | Toggle publish window visibility |
| `Alt+P` | Publish window | Send/publish the message |
| `Escape` | Publish window | Close the publish window |
| `Ctrl+Enter` | Publish window editor | Send/publish the message (alternative) |

## MQTT V5 Properties Reference (Publish-Relevant)

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| Topic | string | Selected topic | Required |
| QoS | 0/1/2 | 1 | Quality of Service |
| Retain | bool | false | Broker retains message |
| Content-Type | string | (empty) | MIME type of payload |
| Payload Format Indicator | enum | Unspecified | `Unspecified` or `CharacterData` |
| Response Topic | string | (empty) | For request-response patterns |
| Correlation Data | byte[] | (empty) | Links request to response |
| Message Expiry Interval | uint | 0 (disabled) | Seconds until message expires |
| User Properties | list<k,v> | (empty) | Custom key-value pairs |

## Acceptance Criteria

1. `:publish` command is recognized by CommandParserService
2. `:publish topic "text"` publishes directly without dialog
3. `:publish topic @file` reads file and publishes contents
4. `:publish` (no args) opens the floating publish window
5. Publish window is non-modal — main application remains interactive
6. All MQTT V5 publish properties can be set in the dialog
7. `@` triggers file autocomplete in both command line and dialog editor
8. Text editor shows line numbers and auto-detected syntax highlighting
9. Own published messages appear in message history with visual indicator
10. Publish history (last 50) is persisted and restorable
11. `Ctrl+Shift+M` toggles publish window, `Alt+P` sends message
12. Application updates status bar on publish success/failure
13. Publish is disabled when not connected to broker

## Edge Cases

- Publishing to empty topic → validation error
- Publishing with no payload → empty payload (valid in MQTT)
- File not found with `@` reference → error message in status bar
- Publishing while disconnected → error message, button disabled
- Very large file reference → warn if > 256KB, reject if > 1MB
- Binary file with text content-type → user's responsibility, no auto-correction
- Concurrent publishes → queue or allow parallel (MQTTnet handles this)
- Publish window open during reconnection → re-enable on reconnect
