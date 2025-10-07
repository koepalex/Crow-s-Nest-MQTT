# Quickstart: Goto Response for MQTT V5 Request-Response

**Feature**: 003-feat-goto-response
**Purpose**: Manual testing scenarios and user acceptance validation
**Prerequisites**: Crow's Nest MQTT client with MQTT V5 broker connection

## Test Environment Setup

### MQTT V5 Broker Setup
```bash
# Using Mosquitto with MQTT V5 support
mosquitto -c mosquitto.conf -v
# Ensure config includes: protocol_version mqttv5
```

### Test Topic Structure
```
request/sensor/data      # Request topic
response/sensor/data     # Response topic (should be subscribed)
response/sensor/other    # Alternative response topic (unsubscribed)
```

## Acceptance Test Scenarios

### Scenario 1: Clock Icon Display (Pending Response)

**Setup**:
1. Connect to MQTT V5 broker
2. Subscribe to topic `request/sensor/data`
3. Subscribe to topic `response/sensor/data`

**Test Steps**:
1. Publish request message to `request/sensor/data` with:
   ```json
   {
     "response-topic": "response/sensor/data",
     "correlation-data": "test-correlation-001",
     "payload": "sensor reading request"
   }
   ```
2. Locate the message in the message list
3. Check the metadata area for the request message

**Expected Results**:
- ✅ Clock icon appears next to response-topic metadata
- ✅ Icon tooltip shows "Awaiting response"
- ✅ Icon is not clickable (no cursor change on hover)
- ✅ Message displays correlation-data in metadata table

**Validation Commands**:
```
:filter correlation-data:test-correlation-001
```

### Scenario 2: Arrow Icon Display (Response Received)

**Setup**: Continue from Scenario 1

**Test Steps**:
1. Publish response message to `response/sensor/data` with:
   ```json
   {
     "correlation-data": "test-correlation-001",
     "payload": "sensor reading: 23.5°C"
   }
   ```
2. Observe the original request message icon

**Expected Results**:
- ✅ Clock icon changes to arrow icon automatically
- ✅ Icon tooltip shows "Click to view response"
- ✅ Arrow icon is clickable (cursor changes on hover)
- ✅ Icon color changes to indicate active state

**Validation Commands**:
```
:goto response test-correlation-001
```

### Scenario 3: Response Navigation

**Setup**: Continue from Scenario 2

**Test Steps**:
1. Click the arrow icon on the request message
2. Observe navigation behavior
3. Check selected message in response topic

**Expected Results**:
- ✅ UI navigates to `response/sensor/data` topic
- ✅ Response message with matching correlation-data is selected and highlighted
- ✅ Status bar shows navigation confirmation
- ✅ Navigation command is logged in command history

**Alternative Test** (Keyboard Navigation):
1. Select request message
2. Use command palette (Ctrl+Shift+P)
3. Type `:goto response`
4. Execute command

### Scenario 4: Unsubscribed Response Topic

**Setup**:
1. Clear current messages and correlations
2. Ensure `response/sensor/other` is NOT subscribed

**Test Steps**:
1. Publish request message to `request/sensor/data` with:
   ```json
   {
     "response-topic": "response/sensor/other",
     "correlation-data": "test-correlation-002",
     "payload": "unsubscribed response test"
   }
   ```
2. Check icon display

**Expected Results**:
- ✅ Disabled clock icon appears (grayed out)
- ✅ Icon tooltip shows "Response topic not subscribed"
- ✅ Icon is not clickable
- ✅ Status remains "NavigationDisabled" even if response is published

### Scenario 5: Multiple Responses with Same Correlation

**Setup**:
1. Subscribe to `response/sensor/data`
2. Publish request with correlation-data: "multi-response-test"

**Test Steps**:
1. Publish first response with correlation-data: "multi-response-test"
2. Publish second response with same correlation-data
3. Click arrow icon on request message

**Expected Results**:
- ✅ Arrow icon appears after first response
- ✅ Navigation selects the first matching response message
- ✅ Both response messages are correlated to the same request
- ✅ User can manually navigate between responses

### Scenario 6: Command Palette Integration

**Setup**: Have request-response correlation established

**Test Steps**:
1. Open command palette (Ctrl+Shift+P)
2. Type `:goto`
3. Look for response navigation commands
4. Execute `:gotoresponse [message-id]`

**Expected Results**:
- ✅ Response navigation commands appear in palette
- ✅ Commands show target response topic names
- ✅ Command execution navigates correctly
- ✅ Invalid commands show appropriate error messages

## Performance Validation

### High-Volume Message Test

**Setup**:
```bash
# Script to generate 1000 request-response pairs
for i in {1..1000}; do
  mosquitto_pub -h localhost -t request/load/test -p 1883 \
    -P mqttv5 --property correlation-data "load-test-$i" \
    --property response-topic "response/load/test" \
    -m "Load test message $i"
done
```

**Validation Criteria**:
- ✅ UI remains responsive during message influx
- ✅ Icons update in real-time without lag
- ✅ Memory usage stays within acceptable limits
- ✅ Navigation performance maintains <100ms response time

### Cleanup and TTL Validation

**Test Steps**:
1. Create correlations with short TTL (1 minute)
2. Wait for TTL expiration
3. Check memory usage and correlation count

**Expected Results**:
- ✅ Expired correlations are cleaned up automatically
- ✅ Icons transition from Pending to hidden after TTL
- ✅ Memory usage decreases after cleanup
- ✅ Performance remains stable with cleanup operations

## Error Handling Validation

### Malformed Correlation Data
```json
{
  "response-topic": "response/sensor/data",
  "correlation-data": null,
  "payload": "invalid correlation test"
}
```

**Expected**: No icon displayed, message treated as regular MQTT message

### Invalid Response Topic
```json
{
  "response-topic": "invalid/topic/with spaces",
  "correlation-data": "invalid-topic-test",
  "payload": "invalid topic test"
}
```

**Expected**: Disabled clock icon, error logged but no UI disruption

### Network Disconnection During Correlation
**Test Steps**:
1. Establish request-response correlation
2. Disconnect from broker
3. Reconnect after 30 seconds
4. Check correlation state

**Expected**: Correlations cleared on disconnect, icons hidden, clean state on reconnect

## User Experience Validation

### Accessibility
- ✅ Icons have appropriate ARIA labels
- ✅ Keyboard navigation works for clickable icons
- ✅ High contrast mode displays icons clearly
- ✅ Screen reader announces icon state changes

### Visual Consistency
- ✅ Icons match application design language
- ✅ Hover states provide clear feedback
- ✅ Icons scale properly at different zoom levels
- ✅ Theme changes (light/dark) update icon colors

### User Workflow
- ✅ Common request-response workflows feel intuitive
- ✅ Error states provide helpful feedback
- ✅ Navigation is discoverable without training
- ✅ Feature doesn't interfere with existing MQTT client functionality

## Completion Criteria

All scenarios must pass with ✅ results before feature is considered complete:

- [ ] Visual feedback (clock/arrow icons) works correctly
- [ ] Navigation to response messages functions as expected
- [ ] Command palette integration is discoverable and functional
- [ ] Performance remains acceptable under high message volume
- [ ] Error handling is graceful and informative
- [ ] Cleanup and resource management prevents memory leaks
- [ ] Cross-platform compatibility (Windows, Linux, macOS)
- [ ] Accessibility requirements are met

## Troubleshooting Common Issues

### Icons Not Appearing
- Check MQTT V5 protocol version in connection
- Verify response-topic metadata is present in message
- Confirm correlation-data is valid byte array

### Navigation Not Working
- Ensure response topic is subscribed and visible
- Check correlation-data matching between request/response
- Verify response message is received and processed

### Performance Issues
- Monitor correlation cleanup frequency settings
- Check message buffer limits and memory usage
- Verify UI update throttling is working correctly