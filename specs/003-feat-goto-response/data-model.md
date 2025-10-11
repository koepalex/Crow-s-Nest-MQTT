# Data Model: Goto Response for MQTT V5 Request-Response

**Feature**: 003-feat-goto-response
**Phase**: 1 - Design & Contracts
**Created**: 2025-09-29

## Core Entities

### Request Message
Represents an MQTT message that initiates a request-response pattern.

**Properties**:
- `TopicName` (string): The topic where the request was published
- `Payload` (byte[]): Message content
- `ResponseTopic` (string, nullable): Target topic for response (from MQTT V5 response-topic property)
- `CorrelationData` (byte[], nullable): Unique identifier linking request to response (from MQTT V5 correlation-data property)
- `Timestamp` (DateTime): When the request message was received
- `MessageId` (string): Unique identifier for this message instance
- `UserProperties` (Dictionary<string, string>): Additional MQTT V5 user properties

**Validation Rules**:
- `TopicName` must not be null or empty
- `ResponseTopic` is required for request-response patterns
- `CorrelationData` must be present when `ResponseTopic` is specified
- `CorrelationData` must be unique within the current session scope
- `Timestamp` must not be in the future

**Business Rules**:
- Only messages with `ResponseTopic` metadata participate in request-response patterns
- Messages without `CorrelationData` cannot be correlated with responses
- Request messages remain active until corresponding response is received or TTL expires

### Response Message
Represents an MQTT message that responds to a previous request.

**Properties**:
- `TopicName` (string): The response topic (matches request's ResponseTopic)
- `Payload` (byte[]): Response content
- `CorrelationData` (byte[], nullable): Links back to original request
- `Timestamp` (DateTime): When the response message was received
- `MessageId` (string): Unique identifier for this message instance
- `UserProperties` (Dictionary<string, string>): Additional MQTT V5 user properties

**Validation Rules**:
- `TopicName` must match a subscribed response topic
- `CorrelationData` must match an existing pending request
- `Timestamp` must be after the corresponding request timestamp

**Business Rules**:
- Response messages are only meaningful when correlated with a request
- Multiple response messages can share the same correlation-data (broadcast responses)
- Response messages without correlation-data are treated as regular messages

### Message Correlation
Manages the relationship between request and response messages.

**Properties**:
- `CorrelationData` (byte[]): The unique correlation identifier
- `RequestMessageId` (string): Reference to the original request message
- `ResponseTopic` (string): Expected topic for response messages
- `RequestTimestamp` (DateTime): When the request was initiated
- `ResponseMessageIds` (List<string>): References to received response messages
- `Status` (CorrelationStatus): Current state of the correlation
- `ExpiresAt` (DateTime): When this correlation should be cleaned up

**Validation Rules**:
- `CorrelationData` must be unique across all active correlations
- `ResponseTopic` must be a valid MQTT topic name
- `ExpiresAt` must be after `RequestTimestamp`
- `Status` transitions must follow defined state machine

**State Transitions**:
```
Pending → Responded (when first response received)
Pending → Expired (when TTL reached without response)
Responded → Expired (after extended TTL for cleanup)
```

### Response Status
Enumeration defining the visual state for request messages in the UI.

**Values**:
- `Hidden`: No response-topic metadata present, no icon shown
- `Pending`: Request sent, awaiting response, show clock icon
- `Received`: Response received, show clickable arrow icon
- `NavigationDisabled`: Response topic not subscribed, show disabled clock icon

**Business Rules**:
- Status automatically updates when correlation state changes
- UI icons are bound to this status value
- Navigation is only enabled in `Received` state

## Entity Relationships

```
Request Message (1) ←→ (0..1) Message Correlation
Message Correlation (1) ←→ (0..n) Response Message
Request Message (1) ←→ (1) Response Status
```

**Relationship Rules**:
- Each Request Message can have at most one active Message Correlation
- Each Message Correlation can link to multiple Response Messages (one-to-many responses)
- Response Status is derived from the associated Message Correlation state
- Orphaned correlations (no matching request) are cleaned up automatically

## Data Flow

### Request Processing Flow
1. MQTT message received → Parse for response-topic and correlation-data
2. If both present → Create Message Correlation entry
3. Update Request Message with correlation reference
4. Set Response Status to Pending
5. Start TTL timer for correlation cleanup

### Response Processing Flow
1. MQTT message received on response topic → Extract correlation-data
2. Lookup existing Message Correlation by correlation-data
3. If found → Link Response Message to correlation
4. Update correlation status to Responded
5. Update Request Message Response Status to Received
6. Trigger UI update for icon change

### Navigation Flow
1. User clicks arrow icon → Retrieve correlation data from Request Message
2. Lookup Response Messages by correlation-data
3. Navigate to response topic in UI
4. Select and highlight specific response message
5. Log navigation action for user workflow tracking

## Persistence Strategy

### In-Memory Storage
- **Message Correlations**: ConcurrentDictionary with TTL-based cleanup
- **Status Cache**: Observable collection for UI binding
- **Cleanup Timer**: Periodic removal of expired correlations

### Cleanup Policies
- **Default TTL**: 30 minutes for pending correlations
- **Extended TTL**: 2 hours for responded correlations (allow late responses)
- **Cleanup Frequency**: Every 5 minutes
- **Memory Limit**: 10,000 active correlations (configurable)

### Thread Safety
- All correlation operations use concurrent collections
- Status updates use immutable value types
- UI updates dispatched to main thread via ReactiveUI

## Performance Characteristics

### Time Complexity
- **Correlation Lookup**: O(1) - hash-based dictionary access
- **Status Update**: O(1) - direct property assignment
- **Cleanup Operation**: O(n) - periodic scan of all correlations

### Space Complexity
- **Per Correlation**: ~200 bytes (correlation-data + metadata)
- **Maximum Memory**: 2MB for 10,000 correlations
- **Cleanup Impact**: Automatic reduction when TTL expires

### Scalability Limits
- **Correlation Volume**: Up to 10,000 active correlations
- **Response Volume**: Unlimited responses per correlation
- **Update Frequency**: Real-time updates for up to 1,000 status changes/second