# Quickstart: JSON Viewer Default Expansion

**Feature**: 003-json-viewer-should
**Purpose**: Validate JSON auto-expansion behavior across all viewer contexts
**Duration**: ~10 minutes

## Prerequisites

- Crow's NestMQTT application running
- Access to MQTT broker with test messages
- Ability to publish JSON messages (e.g., via `mosquitto_pub` or MQTT Explorer)

## Test Setup

### Sample JSON Messages

Create these test messages for validation:

**Message 1: 3-Level Nested (all should expand)**
```json
{
  "level1": {
    "level2": {
      "level3": {
        "value": "deepest"
      }
    }
  }
}
```

**Message 2: 5-Level Nested (all should expand)**
```json
{
  "a": {
    "b": {
      "c": {
        "d": {
          "e": {
            "value": "level 5"
          }
        }
      }
    }
  }
}
```

**Message 3: 7-Level Nested (only first 5 should expand)**
```json
{
  "l1": {
    "l2": {
      "l3": {
        "l4": {
          "l5": {
            "l6": {
              "l7": {
                "value": "too deep"
              }
            }
          }
        }
      }
    }
  }
}
```

**Message 4: Large Flat Object (performance test)**
```json
{
  "prop1": "value1",
  "prop2": "value2",
  ... (1000 properties)
}
```

### Publish Messages

```bash
# Publish to test topic
mosquitto_pub -h localhost -t "test/json/expansion" -m '{"level1":{"level2":{"level3":{"value":"deepest"}}}}'
mosquitto_pub -h localhost -t "test/json/expansion" -m '{"a":{"b":{"c":{"d":{"e":{"value":"level 5"}}}}}}'
mosquitto_pub -h localhost -t "test/json/expansion" -m '{"l1":{"l2":{"l3":{"l4":{"l5":{"l6":{"l7":{"value":"too deep"}}}}}}}}'
```

## Validation Steps

### Test 1: `:view json` Command Expansion

**Objective**: Verify all JSON displays expanded in main viewer (Acceptance Scenario 1)

**Steps**:
1. Connect to MQTT broker: `:connect localhost:1883`
2. Subscribe to topic: (use existing subscription mechanism)
3. Select message with 3-level JSON
4. Execute `:view json` command
5. Observe tree view

**Expected Result**:
- ✅ All 3 levels visible without manual expansion
- ✅ Tree nodes showing keys: "level1" → "level2" → "level3" → "value"
- ✅ No collapsed nodes (all expand icons showing "-" not "+")

**Actual Result**: _[Fill during testing]_

---

### Test 2: 5-Level Depth Limit

**Objective**: Verify 5-level messages fully expand (Acceptance Scenario 6)

**Steps**:
1. Select message with 5-level JSON
2. Execute `:view json`
3. Count visible levels in tree

**Expected Result**:
- ✅ All 5 levels (a → b → c → d → e) visible
- ✅ Value "level 5" visible without manual expansion

**Actual Result**: _[Fill during testing]_

---

### Test 3: 6+ Level Partial Expansion

**Objective**: Verify depth >5 stays collapsed (Acceptance Scenario 7)

**Steps**:
1. Select message with 7-level JSON
2. Execute `:view json`
3. Inspect tree expansion state

**Expected Result**:
- ✅ Levels 1-5 (l1 → l2 → l3 → l4 → l5) visible and expanded
- ✅ Level 6 (l6) visible but COLLAPSED (expand icon showing "+")
- ✅ Level 7 (l7) NOT initially visible
- ✅ Manual expansion of l6 reveals l7

**Actual Result**: _[Fill during testing]_

---

### Test 4: Manual Collapse Functionality

**Objective**: Verify users can collapse nodes (Acceptance Scenario 4)

**Steps**:
1. Use message from Test 1 (3-level JSON)
2. Execute `:view json`
3. Click collapse icon ("-") on "level1" node
4. Observe tree state

**Expected Result**:
- ✅ "level1" node collapses
- ✅ "level2" and "level3" hidden
- ✅ Expand icon changes from "-" to "+"
- ✅ Re-expanding shows children again

**Actual Result**: _[Fill during testing]_

---

### Test 5: State Reset on Message Switch

**Objective**: Verify state doesn't persist across messages (Acceptance Scenario 5)

**Steps**:
1. View Message 1 with `:view json`
2. Manually collapse "level1" node
3. Switch to Message 2 (5-level JSON)
4. Execute `:view json` on Message 2
5. Switch back to Message 1
6. Observe Message 1 expansion state

**Expected Result**:
- ✅ Message 2 shows fully expanded (5 levels)
- ✅ Returning to Message 1 shows fully expanded again (not collapsed)
- ✅ Manual collapse state forgotten

**Actual Result**: _[Fill during testing]_

---

### Test 6: Cross-Context Consistency (Message Preview)

**Objective**: Verify expansion works in message previews (Acceptance Scenario 2)

**Steps**:
1. Ensure message preview pane is visible
2. Select message with 3-level JSON (don't execute `:view json`)
3. Observe preview pane JSON rendering

**Expected Result**:
- ✅ Preview shows expanded JSON tree (same as `:view json`)
- ✅ All 3 levels visible in preview

**Actual Result**: _[Fill during testing]_

---

### Test 7: Performance with Large JSON

**Objective**: Verify UI remains responsive (Edge Case from spec)

**Steps**:
1. Select message with 1000+ properties (flat structure)
2. Execute `:view json`
3. Measure time until tree fully rendered
4. Scroll through tree view

**Expected Result**:
- ✅ Tree renders in <1 second
- ✅ Scrolling remains smooth (60 FPS)
- ✅ UI does not freeze during rendering
- ✅ All properties visible (expanded)

**Actual Result**: _[Fill during testing]_

---

### Test 8: Malformed JSON Handling

**Objective**: Verify graceful error handling (Edge Case from spec)

**Steps**:
1. Publish malformed JSON: `{"broken": "json`
2. Select the malformed message
3. Execute `:view json`

**Expected Result**:
- ✅ Error message displayed (e.g., "Invalid JSON: unexpected end of input")
- ✅ Application does not crash
- ✅ Can still view other messages normally

**Actual Result**: _[Fill during testing]_

---

## Acceptance Criteria Checklist

Based on spec.md acceptance scenarios:

- [ ] **AC-1**: `:view json` displays all nested JSON properties expanded (Test 1)
- [ ] **AC-2**: No manual expand clicks needed to see nested data (Test 1-3)
- [ ] **AC-3**: Multiple nesting levels all expanded by default (Test 2)
- [ ] **AC-4**: Manual collapse functionality works (Test 4)
- [ ] **AC-5**: Collapse state resets when switching messages (Test 5)
- [ ] **AC-6**: Up to 5 levels auto-expand (Test 2)
- [ ] **AC-7**: Beyond 5 levels remain collapsed (Test 3)

## Edge Cases Checklist

- [ ] Large JSON (1000+ properties) renders without UI blocking (Test 7)
- [ ] Deep nesting (10+ levels) handled correctly (Test 3)
- [ ] Malformed JSON shows error message (Test 8)

## Cross-Platform Validation

**If possible, repeat Tests 1-3 on**:
- [ ] Windows
- [ ] Linux
- [ ] macOS

Behavior should be identical across all platforms per constitutional requirement V.

## Troubleshooting

**If expansion doesn't work**:
1. Check application logs for errors during tree construction
2. Verify JsonTreeBuilder.MaxAutoExpandDepth = 5
3. Inspect TreeViewItem.IsExpanded bindings in UI

**If performance is poor**:
1. Profile tree construction time
2. Check if virtualization is enabled on TreeView control
3. Verify lazy-loading is working for depth > 5

## Success Criteria

✅ **Feature is working correctly if**:
- All 7 acceptance criteria pass
- All 3 edge case tests pass
- No crashes or exceptions during testing
- Cross-platform consistency verified (if applicable)

## Regression Testing

**After future changes, re-run**:
- Test 1 (basic expansion)
- Test 3 (depth limit)
- Test 5 (state reset)

These cover core functionality and most common use cases.
