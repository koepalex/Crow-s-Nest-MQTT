# Feature Specification: Export All Messages from Topic History

**Feature Branch**: `006-there-is-already`
**Created**: 2026-01-21
**Status**: Draft
**Input**: User description: "There is already the functionality to export the selected message. We need now the functionality to export all messages in the history view of the selected topic. This should be triggered by :export all (:export still causes old behavior). There should be also a small icon next to the topic delete icon to export all. That said there is currently a button to copy the message into clipboard for each row in history view but the button to export the single message is missing"

## Execution Flow (main)
```
1. Parse user description from Input
   → Identified needs: bulk export of topic history, additional UI buttons
2. Extract key concepts from description
   → Actors: users viewing topic history
   → Actions: export all messages, export single message, copy single message
   → Data: message history for selected topic
   → Constraints: preserve existing :export behavior, add new :export all syntax
3. For each unclear aspect:
   → All aspects are clear from description
4. Fill User Scenarios & Testing section
   → Clear user flows: export all messages from topic, export single message
5. Generate Functional Requirements
   → Each requirement is testable
6. Identify Key Entities (if data involved)
   → Message history entity identified
7. Run Review Checklist
   → No clarifications needed, ready for implementation
8. Return: SUCCESS (spec ready for planning)
```

---

## ⚡ Quick Guidelines
- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

---

## Clarifications

### Session 2026-01-21
- Q: When exporting all messages from a topic, how should the output file be named? → A: Auto-generated with pattern: `topic-name_timestamp.ext`
- Q: For JSON format export, when exporting all messages from a topic, how should the messages be structured in the output file? → A: JSON format uses single JSON array `[{msg1}, {msg2}, ...]`; TXT format uses delimiter and new lines between messages
- Q: When exporting all messages from a topic, should there be a maximum limit on the number of messages that can be exported? → A: Hard limit of 100 messages maximum per export
- Q: When an export all operation generates a filename that already exists in the target directory, what should happen? → A: Overwrite the existing file without warning
- Q: When a user clicks the export button for a single message in the history view (not the "export all"), how should the output file be named? → A: Same as existing `:export` behavior (uses configured export path as-is)

---

## User Scenarios & Testing *(mandatory)*

### Primary User Story
A user monitoring MQTT messages for a specific topic accumulates multiple messages in the history view. They want to export all messages at once for analysis, archival, or sharing purposes. Additionally, when reviewing individual messages in the history, users want the ability to export a single message without selecting it first.

### Acceptance Scenarios
1. **Given** a topic with multiple messages in history, **When** user executes `:export all` command, **Then** all messages for the selected topic should be exported to an auto-generated file named with pattern `topic-name_timestamp.ext` in the configured format
2. **Given** a topic with multiple messages in history, **When** user clicks the export all icon next to the delete topic button, **Then** all messages for the selected topic should be exported to an auto-generated file
3. **Given** a message displayed in the history view, **When** user clicks the export button for that specific message row, **Then** only that single message should be exported using the same behavior as the existing `:export` command (configured export path)
4. **Given** user executes `:export` command without the "all" parameter, **When** command is processed, **Then** system should maintain current behavior (export selected message only)
5. **Given** a topic with no messages in history, **When** user attempts to export all, **Then** system should provide appropriate feedback indicating no messages to export
6. **Given** multiple messages exported together, **When** export completes, **Then** exported file should preserve message order, timestamps, and all metadata
7. **Given** JSON export format is configured, **When** user exports all messages, **Then** the output file should contain a valid JSON array with all message objects: `[{message1}, {message2}, ...]`
8. **Given** TXT export format is configured, **When** user exports all messages, **Then** the output file should contain messages separated by delimiters and new lines
9. **Given** a topic has more than 100 messages in history, **When** user executes `:export all` or clicks export all button, **Then** system should export only the most recent 100 messages and notify the user about the limit
10. **Given** an export file with auto-generated name already exists in the target directory, **When** user executes `:export all`, **Then** the system should overwrite the existing file without prompting for confirmation

### Edge Cases
- What happens when attempting to export all messages from an empty topic history?
- What occurs when export fails mid-operation (disk full, permission denied)?
- What happens when user executes `:export all` but no topic is currently selected?
- When a topic has more than 100 messages, only the most recent 100 are exported with user notification about the limit

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001**: System MUST support `:export all` command to export all messages from the selected topic's history view
- **FR-002**: System MUST preserve existing `:export` command behavior (export selected message only) when "all" parameter is not specified
- **FR-003**: System MUST provide a UI button/icon positioned next to the topic delete icon to trigger export all functionality
- **FR-004**: System MUST provide an export button for each message row in the history view to export individual messages
- **FR-005**: Per-message export buttons in the history view MUST use the same file naming and export behavior as the existing `:export` command (uses configured export path as-is)
- **FR-006**: System MUST export all messages in the order they appear in the history view
- **FR-007**: System MUST include all message metadata (timestamp, topic, QoS, retain flag, user properties, correlation data) in exported files
- **FR-008**: System MUST respect the configured export format setting (JSON, text, etc.) for both single and bulk exports
- **FR-009**: When exporting all messages in JSON format, system MUST structure the output as a single JSON array containing all message objects: `[{message1}, {message2}, ...]`
- **FR-010**: When exporting all messages in TXT format, system MUST separate messages using delimiters and new lines between each message entry
- **FR-011**: System MUST respect the configured export path setting for all export operations
- **FR-012**: System MUST auto-generate filenames for export all operations using the pattern `topic-name_timestamp.ext` where topic-name is the selected topic and timestamp ensures uniqueness
- **FR-013**: System MUST enforce a hard limit of 100 messages maximum per export all operation
- **FR-014**: System MUST provide clear feedback to users when the export all limit of 100 messages would be exceeded, indicating only the most recent 100 messages will be exported
- **FR-015**: System MUST overwrite existing files without warning when the auto-generated export filename already exists in the target directory
- **FR-016**: System MUST provide user feedback when export all operation completes successfully
- **FR-017**: System MUST provide appropriate error messages when export operations fail
- **FR-018**: System MUST disable or provide appropriate feedback for export all button when no topic is selected
- **FR-019**: System MUST disable or provide appropriate feedback for export all button when selected topic has no messages in history
- **FR-020**: Users MUST be able to distinguish between "export selected message", "export this message", and "export all messages" operations through clear UI elements and command syntax

### Key Entities *(include if feature involves data)*
- **Message History**: Collection of messages received for a specific topic, displayed in the history view with timestamp, payload, and metadata
- **Export Configuration**: User settings defining export format (JSON, text) and target file path for all export operations

---

## Review & Acceptance Checklist
*GATE: Automated checks run during main() execution*

### Content Quality
- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness
- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

---

## Execution Status
*Updated by main() during processing*

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [x] Review checklist passed

---
