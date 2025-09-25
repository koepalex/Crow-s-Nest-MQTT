# Contract: IDeleteTopicService

**Purpose**: Core service interface for deleting retained messages from MQTT topics

## Service Interface

```csharp
public interface IDeleteTopicService
{
    /// <summary>
    /// Asynchronously deletes retained messages from the specified topic and all subtopics
    /// </summary>
    /// <param name="command">Delete command parameters</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Result containing success/failure details</returns>
    Task<DeleteTopicResult> DeleteTopicAsync(DeleteTopicCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers all subtopics under the given pattern that have retained messages
    /// </summary>
    /// <param name="topicPattern">Root topic to search under</param>
    /// <returns>List of topic names with retained messages</returns>
    IEnumerable<string> FindTopicsWithRetainedMessages(string topicPattern);

    /// <summary>
    /// Validates if a delete operation can proceed given current constraints
    /// </summary>
    /// <param name="topicPattern">Topic pattern to validate</param>
    /// <param name="maxLimit">Maximum topic limit</param>
    /// <returns>Validation result with any constraint violations</returns>
    ValidationResult ValidateDeleteOperation(string topicPattern, int maxLimit);
}
```

## Method Contracts

### DeleteTopicAsync
**Preconditions**:
- Command must not be null
- Either command.TopicPattern is valid or active topic selection exists
- MQTT connection must be active and authenticated
- User must have publish permissions to target topics

**Postconditions**:
- Returns result with accurate counts and timing information
- All accessible topics under pattern have retained messages cleared
- Failed topics are documented with specific error reasons
- UI topic tree is updated to reflect changes
- Operation is logged with structured data

**Error Handling**:
- Throws `ArgumentNullException` for null command
- Throws `InvalidOperationException` if MQTT disconnected during operation
- Throws `OperationCanceledException` if cancelled via token
- Individual topic failures are collected, not thrown

### FindTopicsWithRetainedMessages
**Preconditions**:
- TopicPattern must be valid MQTT topic name
- Topic tree data must be available and current

**Postconditions**:
- Returns enumerable of exact topic names (not patterns)
- Includes all direct and nested subtopics
- Results are ordered for consistent processing
- Empty enumerable if no matching topics found

**Performance Requirements**:
- Must complete within 100ms for typical topic trees
- Memory usage proportional to result count only
- No blocking I/O operations (uses cached topic tree)

### ValidateDeleteOperation
**Preconditions**:
- TopicPattern may be null (indicating selected topic usage)
- MaxLimit must be positive integer

**Postconditions**:
- Returns validation result with specific constraint violations
- Includes estimated topic count if validation passes
- Provides actionable error messages for failures
- Does not modify application state

## Error Types

### Service-Level Exceptions
- `MqttConnectionException` - Broker connection lost
- `InsufficientPermissionsException` - Cannot publish to required topics
- `TopicLimitExceededException` - Operation would exceed configured limits

### Validation Errors
- `InvalidTopicPattern` - Topic name violates MQTT specification
- `ExceedsMaximumLimit` - Requested operation too large
- `NoActiveSelection` - No topic pattern and no active selection
- `EmptyTopicTree` - No topics available for deletion

## Dependencies

### Required Services
- `IMqttClientService` - For publishing empty retained messages
- `ITopicTreeService` - For discovering topics with retained messages
- `IConfigurationService` - For retrieving operation limits and timeouts
- `ILoggingService` - For structured operation logging

### UI Integration
- Must raise `TopicDeletedEvent` for each successful deletion
- Must update `StatusBarText` with operation progress
- Must integrate with existing command cancellation system