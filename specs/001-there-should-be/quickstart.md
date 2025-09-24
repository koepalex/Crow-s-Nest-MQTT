# Quickstart: Delete Topic Command

**Purpose**: Step-by-step validation guide for testing the `:deletetopic` command functionality

## Prerequisites

1. **MQTT Broker Setup**:
   - Running MQTT broker (local or remote)
   - Test user with publish permissions to test topics
   - Broker should retain messages (most do by default)

2. **Test Data Preparation**:
   - Publish several retained messages to create test topics
   - Use nested topic structure (e.g., `test/sensors/temp`, `test/sensors/humidity`)
   - Ensure some topics have retained messages, others don't

3. **Application State**:
   - Crow's NestMQTT connected to broker
   - Topic tree populated with test topics
   - Command palette accessible (Ctrl+Shift+P)

## Basic Functionality Tests

### Test 1: Delete Selected Topic
```
Setup: Select topic 'test/sensors/temp' in topic tree
Command: :deletetopic
Expected: Status bar shows "Delete operation started", topic tree updates to show 0 retained messages
Validation: Retained message for test/sensors/temp is cleared on broker
```

### Test 2: Delete with Topic Pattern
```
Setup: Navigate away from any topic selection
Command: :deletetopic test/sensors
Expected: All subtopics under test/sensors have retained messages cleared
Validation: test/sensors/temp, test/sensors/humidity both cleared
```

### Test 3: Delete Non-existent Topic
```
Setup: Ensure topic 'test/nonexistent' has no retained messages
Command: :deletetopic test/nonexistent
Expected: Command completes successfully with "No retained messages found"
Validation: No errors, operation completes quickly
```

## Edge Case Tests

### Test 4: Large Topic Count (Limit Validation)
```
Setup: Create topic structure with 100+ retained messages
Command: :deletetopic test/large
Expected: Confirmation prompt appears for large operation
Action: Confirm deletion
Validation: All topics processed in parallel, completion summary shown
```

### Test 5: Permission Denied Scenario
```
Setup: Connect with user lacking publish permissions to some topics
Command: :deletetopic test/restricted
Expected: Operation continues, skips unauthorized topics
Validation: Summary warning shows "X topics could not be deleted due to permissions"
```

### Test 6: Broker Disconnection During Operation
```
Setup: Start large delete operation (100+ topics)
Action: Disconnect broker during operation
Expected: Operation aborts immediately, user notified to restart manually
Validation: No partial state, clear error message
```

## Performance Validation

### Test 7: Parallel Processing Verification
```
Setup: Create 50 topics with retained messages
Command: :deletetopic test/performance
Measurement: Operation should complete in <5 seconds
Validation: Topics processed concurrently (check logs for parallel execution)
```

### Test 8: UI Responsiveness
```
Setup: Large delete operation (500 topics)
Action: Try to use other UI features during deletion
Expected: UI remains responsive, status bar shows operation progress
Validation: No UI freezing, other commands still functional
```

## Configuration Tests

### Test 9: Custom Topic Limit
```
Setup: Configure MaxTopicLimit to 10 in settings
Command: :deletetopic test/many (targeting 15+ topics)
Expected: Warning about exceeding limit, requires confirmation
Validation: Respects configured limit setting
```

### Test 10: Status Bar Integration
```
Setup: Any valid delete operation
Monitoring: Watch status bar during operation
Expected: Shows "Delete operation started", then "Completed: X topics deleted"
Validation: Minimal notifications as specified, no progress bar
```

## Integration Tests

### Test 11: Topic Tree Real-time Updates
```
Setup: Visible topic tree with retained message counts
Command: :deletetopic test/visible
Observation: Topic tree updates immediately as messages are deleted
Validation: Counts decrease in real-time, tree reflects final state
```

### Test 12: Command Palette Integration
```
Setup: Open command palette (Ctrl+Shift+P)
Action: Type "deletetopic"
Expected: Command appears in suggestions, accepts tab completion
Validation: Consistent with other commands, proper help available
```

## Error Handling Tests

### Test 13: Invalid Topic Pattern
```
Command: :deletetopic invalid#topic
Expected: Error message "Invalid topic pattern: invalid#topic"
Validation: Operation does not start, clear error feedback
```

### Test 14: No Active Connection
```
Setup: Disconnect from MQTT broker
Command: :deletetopic test/any
Expected: Error message "MQTT connection not available"
Validation: Graceful error handling, suggests reconnection
```

## Acceptance Criteria Validation

- [ ] ✅ Command accessible via command palette
- [ ] ✅ Accepts optional topic pattern argument
- [ ] ✅ Uses selected topic when no argument provided
- [ ] ✅ Processes topics in parallel for performance
- [ ] ✅ Handles permission errors gracefully
- [ ] ✅ Shows minimal status bar notifications
- [ ] ✅ Updates topic tree in real-time
- [ ] ✅ Respects configurable topic limits
- [ ] ✅ Aborts on broker disconnection
- [ ] ✅ Provides summary with error details

## Post-Test Cleanup

1. Clear all test topics from broker
2. Reset application configuration to defaults
3. Verify no side effects on other application features
4. Check logs for any unexpected errors or warnings