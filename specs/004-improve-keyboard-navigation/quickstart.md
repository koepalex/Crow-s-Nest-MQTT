# Quickstart: Keyboard Navigation Enhancements

**Feature**: 004-improve-keyboard-navigation
**Purpose**: Validate keyboard navigation functionality through acceptance scenarios
**Estimated Time**: 10-15 minutes

## Prerequisites

- Crow's Nest MQTT application running
- Connected to MQTT broker with multiple topics
- Test topics with varying names (e.g., `sensor/temperature`, `sensor/humidity`, `device/status`, `logs/error`)
- Each topic should have multiple messages in history

## Test Scenario 1: Topic Search via Command Palette

**Validates**: FR-001, FR-002, FR-003, FR-004, FR-005, FR-022

**Steps**:
1. Open command palette (Ctrl+Shift+P or configured shortcut)
2. Type `/sensor` and press Enter
3. **Expected**:
   - First topic containing "sensor" is selected in topic tree (e.g., "sensor/temperature")
   - Message history view updates to show messages from selected topic
   - Status bar displays: `"Search: 'sensor' (X matches)"` where X is total count

**Acceptance Criteria**:
- [ ] Search is case-insensitive (e.g., `/SENSOR` or `/SeNsOr` works identically)
- [ ] Substring matching works (e.g., `/temp` matches "sensor/temperature")
- [ ] First match auto-selected
- [ ] Message history displays correct topic messages
- [ ] Status bar shows search term and match count

---

## Test Scenario 2: Navigate Forward Through Search Results

**Validates**: FR-008, FR-010, FR-012, FR-023

**Steps**:
1. Perform search from Scenario 1 (must have multiple matches)
2. Press `n` key
3. **Expected**:
   - Topic selection moves to second matching topic
   - Message history updates to show second topic's messages
   - Status bar updates: `"Search: 'sensor' (match 2 of X)"`
4. Continue pressing `n` until reaching last match
5. Press `n` one more time
6. **Expected**:
   - Selection wraps to first matching topic
   - Status bar shows: `"Search: 'sensor' (match 1 of X)"`

**Acceptance Criteria**:
- [ ] Each `n` press advances to next match
- [ ] Message history updates for each new topic
- [ ] Status bar shows current position
- [ ] Wrap-around occurs at last match → first match
- [ ] No error or visual glitch during wrap

---

## Test Scenario 3: Navigate Backward Through Search Results

**Validates**: FR-009, FR-010, FR-012, FR-023

**Steps**:
1. Perform search from Scenario 1 (first match selected)
2. Press `N` key (Shift+n)
3. **Expected**:
   - Selection wraps to last matching topic
   - Message history updates to last topic's messages
   - Status bar shows: `"Search: 'sensor' (match X of X)"`
4. Press `N` again
5. **Expected**:
   - Selection moves to second-to-last match
   - Status bar shows decremented position

**Acceptance Criteria**:
- [ ] `N` (Shift+n) moves to previous match
- [ ] Wrap-around occurs at first match → last match
- [ ] Message history updates correctly
- [ ] Status bar position decrements

---

## Test Scenario 4: Navigate Down Messages with `j`

**Validates**: FR-013, FR-015, FR-016

**Steps**:
1. Select any topic with multiple messages (via search or manual selection)
2. Note the first message is selected (or select it manually)
3. Press `j` key
4. **Expected**:
   - Selection moves down to second message
   - Selected message is visually highlighted
5. Continue pressing `j` until reaching last message
6. Press `j` one more time
7. **Expected**:
   - Selection wraps to first message
   - Highlight moves to top of message list

**Acceptance Criteria**:
- [ ] Each `j` press moves selection down one message
- [ ] Visual highlight moves with selection
- [ ] Wrap-around occurs at last message → first message
- [ ] No-op if topic has no messages (no error)

---

## Test Scenario 5: Navigate Up Messages with `k`

**Validates**: FR-014, FR-015, FR-016

**Steps**:
1. Select topic with multiple messages (first message selected)
2. Press `k` key
3. **Expected**:
   - Selection wraps to last message
   - Highlight moves to bottom of message list
4. Press `k` again
5. **Expected**:
   - Selection moves to second-to-last message

**Acceptance Criteria**:
- [ ] Each `k` press moves selection up one message
- [ ] Wrap-around occurs at first message → last message
- [ ] Visual highlight follows selection
- [ ] No-op if topic has no messages

---

## Test Scenario 6: Keyboard Shortcuts Suppressed in Command Palette

**Validates**: FR-020, FR-021

**Steps**:
1. Open command palette (Ctrl+Shift+P)
2. Type `test n and k` (including the letters 'n' and 'k')
3. **Expected**:
   - Letters 'n' and 'k' appear as text in command palette input
   - No topic navigation occurs
   - No message navigation occurs
4. Press Escape to close command palette (or clear input)
5. Press `n` (outside command palette)
6. **Expected**:
   - If search is active: navigate to next topic match
   - If no search: no action

**Acceptance Criteria**:
- [ ] `n`, `N`, `j`, `k` typed as text when command palette focused
- [ ] No navigation shortcuts trigger during text input
- [ ] Shortcuts work normally when command palette closed
- [ ] No interference with text entry

---

## Test Scenario 7: Search with No Matches

**Validates**: FR-007

**Steps**:
1. Open command palette
2. Type `/nonexistenttopicxyz123` and press Enter
3. **Expected**:
   - Status bar displays: `"No topics matching 'nonexistenttopicxyz123'"`
   - Current topic selection remains unchanged
   - No navigation occurs

**Acceptance Criteria**:
- [ ] Feedback message shown in status bar
- [ ] Current selection not cleared
- [ ] No error or crash
- [ ] Status bar message persists until cleared

---

## Test Scenario 8: Rapid Sequential Navigation

**Validates**: FR-017, FR-018, Performance Goals

**Steps**:
1. Perform search with 5+ matches
2. Rapidly press `n` key 10 times in quick succession
3. **Expected**:
   - All key presses processed
   - Topic selection cycles through matches (with wrap-around)
   - UI remains responsive
   - No visual lag or stuttering
4. Rapidly press `j` key 20 times in message history
5. **Expected**:
   - Message selection cycles through history
   - UI stays responsive
   - Highlight updates smoothly

**Acceptance Criteria**:
- [ ] All rapid inputs processed
- [ ] Response time <100ms per key press (no noticeable lag)
- [ ] No dropped inputs
- [ ] UI remains responsive during navigation
- [ ] No visual artifacts or rendering issues

---

## Test Scenario 9: Cross-Component State Synchronization

**Validates**: FR-005, FR-010, FR-018

**Steps**:
1. Perform search with `/sensor`
2. Select second match using `n`
3. Manually click a different topic in topic tree (outside search results)
4. Observe message history updates
5. Press `n` again
6. **Expected**:
   - Navigation returns to search results (third match)
   - Message history updates to third match's messages
   - Search context maintained despite manual topic click

**Acceptance Criteria**:
- [ ] Search navigation works after manual topic selection
- [ ] Message history updates correctly for both search and manual selection
- [ ] `j`/`k` navigation works on manually selected topic
- [ ] Search state persists across manual interactions

---

## Test Scenario 10: Visual Feedback Indicator Lifecycle

**Validates**: FR-022, FR-023, FR-024, FR-025

**Steps**:
1. Verify status bar is visible (or identify search indicator location)
2. Perform search `/sensor`
3. **Expected**: Search indicator shows: `"Search: 'sensor' (X matches)"`
4. Press `n` to navigate
5. **Expected**: Indicator updates: `"Search: 'sensor' (match 2 of X)"`
6. Open command palette and type `/device`
7. Press Enter
8. **Expected**:
   - Old search cleared
   - New indicator shows: `"Search: 'device' (Y matches)"`
9. (If clear search command implemented): Execute clear search
10. **Expected**: Indicator hidden or cleared

**Acceptance Criteria**:
- [ ] Indicator appears in consistent location (status bar)
- [ ] Text updates on search and navigation
- [ ] Format matches specification
- [ ] Old search cleared when new search performed
- [ ] Indicator clears/hides when search cancelled

---

## Edge Cases to Verify

### Single Match Search
**Steps**: Search for topic with only one match, press `n` and `N`
**Expected**: Selection stays on same topic (wrap-around to itself), no error

### Empty Topic History
**Steps**: Select topic with no messages, press `j` and `k`
**Expected**: No action, no error, no visual feedback needed

### Search While on Last Match
**Steps**: Navigate to last search match, press `n`
**Expected**: Wrap to first match seamlessly

---

## Performance Validation

**Metric**: Command response time <100ms
**Test**: Use browser dev tools or performance profiler to measure time from key press to UI update
**Acceptance**: 95th percentile <100ms for all navigation operations

**Metric**: High-volume message handling unaffected
**Test**: Monitor message ingestion rate (10,000+ msg/sec) while performing keyboard navigation
**Acceptance**: No degradation in message processing throughput

---

## Cleanup

- No persistent state changes (navigation is ephemeral)
- No data created or modified
- Can repeat tests without cleanup

---

## Success Criteria Summary

All test scenarios PASS with:
- ✅ All acceptance criteria met
- ✅ No crashes or errors
- ✅ Performance targets achieved
- ✅ Cross-platform behavior identical (Windows, Linux, macOS)

**Estimated Pass Rate**: 100% (all scenarios must pass for feature acceptance)
