# Data Model: Delete Topic Command

**Phase 1 Output** - Core entities and their relationships for the delete topic functionality

## Core Entities

### DeleteTopicCommand
**Purpose**: Represents a delete topic operation request
**Attributes**:
- `TopicPattern: string` - Target topic pattern (optional, uses selected topic if null)
- `MaxTopicLimit: int` - Maximum topics to process (default 500)
- `RequireConfirmation: bool` - Whether to prompt user for large operations
- `Timestamp: DateTime` - When command was initiated

**Validation Rules**:
- TopicPattern must be valid MQTT topic (no wildcards in delete context)
- MaxTopicLimit must be positive integer ≤ 10,000
- If TopicPattern is null, must have active topic selection

### DeleteTopicResult
**Purpose**: Represents the outcome of a delete topic operation
**Attributes**:
- `TotalTopicsFound: int` - Number of topics matching the pattern
- `SuccessfulDeletions: int` - Number of topics successfully cleared
- `FailedDeletions: List<TopicDeletionFailure>` - Topics that could not be deleted
- `OperationDuration: TimeSpan` - Total time taken
- `Status: DeleteOperationStatus` - Overall result status

**Relationships**:
- Has many `TopicDeletionFailure` entries
- Associated with one `DeleteTopicCommand`

### TopicDeletionFailure
**Purpose**: Details about a specific topic that failed to delete
**Attributes**:
- `TopicName: string` - The topic that failed
- `ErrorType: DeletionErrorType` - Type of failure (Permission, Network, Timeout)
- `ErrorMessage: string` - Human-readable error description
- `Timestamp: DateTime` - When the failure occurred

**Relationships**:
- Belongs to one `DeleteTopicResult`

### TopicTreeNode
**Purpose**: Existing entity representing topics in the UI tree (extended for delete operation)
**Extended Attributes**:
- `HasRetainedMessages: bool` - Whether this topic has retained messages
- `RetainedMessageCount: int` - Number of retained messages
- `LastRetainedMessageTime: DateTime?` - When last retained message was received

**Relationships**:
- Tree structure with parent/child relationships
- Traversed by delete operation to find matching topics

## Entity State Transitions

### DeleteOperationStatus Enum
- `NotStarted` → Initial state
- `InProgress` → Operation is running
- `CompletedWithWarnings` → Finished but some topics failed
- `CompletedSuccessfully` → All topics processed successfully
- `Aborted` → Operation was cancelled (e.g., due to disconnection)
- `Failed` → Operation could not start or encountered critical error

### DeletionErrorType Enum
- `PermissionDenied` → Insufficient publish permissions
- `NetworkError` → Connection or communication failure
- `Timeout` → Topic deletion took too long
- `BrokerError` → Broker rejected the operation
- `UnknownError` → Unexpected failure

## Data Volume Assumptions

**Scale Expectations**:
- Default maximum: 500 topics per operation
- Configurable ceiling: 10,000 topics per operation
- Expected typical usage: 10-50 topics per operation
- Concurrent operations: 1 (operations are exclusive)

**Memory Considerations**:
- Each `TopicDeletionFailure` ~200 bytes
- Maximum memory for failures: 2MB (10,000 topics worst case)
- Temporary collections cleared after operation completion

## Validation and Business Rules

### Topic Pattern Validation
- Must be valid MQTT topic name (UTF-8, no null characters)
- Cannot contain MQTT wildcards (`+`, `#`) in delete context
- Maximum length: 65,535 characters (MQTT specification limit)
- Cannot be empty string (use null for selected topic)

### Operation Limits
- Single delete operation cannot exceed `MaxTopicLimit`
- User must confirm operations exceeding 100 topics
- Operations are atomic per topic (partial success allowed)
- No concurrent delete operations permitted

### Error Recovery
- Individual topic failures do not stop operation
- Network disconnection aborts entire operation
- Timeout per topic: 5 seconds default
- Failed topics are reported in summary but operation continues