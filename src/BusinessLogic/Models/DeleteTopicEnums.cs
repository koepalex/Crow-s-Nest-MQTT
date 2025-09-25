namespace CrowsNestMqtt.BusinessLogic.Models;

/// <summary>
/// Status of a delete topic operation.
/// Indicates the final outcome of the operation.
/// </summary>
public enum DeleteOperationStatus
{
    /// <summary>
    /// Operation completed successfully with all topics deleted.
    /// </summary>
    CompletedSuccessfully,

    /// <summary>
    /// Operation completed but with some warnings or non-critical failures.
    /// Some topics may have failed to delete due to permissions or other issues.
    /// </summary>
    CompletedWithWarnings,

    /// <summary>
    /// Operation failed completely due to critical error.
    /// No topics were successfully deleted.
    /// </summary>
    Failed,

    /// <summary>
    /// Operation was aborted due to user request, timeout, or critical system error.
    /// Some topics may have been deleted before abortion.
    /// </summary>
    Aborted,

    /// <summary>
    /// Operation is waiting for user confirmation before proceeding.
    /// Occurs when topic count exceeds limits or patterns are sensitive.
    /// </summary>
    AwaitingConfirmation
}

/// <summary>
/// Type of error that occurred during topic deletion.
/// Used for categorizing and handling different failure scenarios.
/// </summary>
public enum DeletionErrorType
{
    /// <summary>
    /// Permission denied - client lacks permission to publish to the topic.
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// Network error - connection issues, timeout, or broker unavailable.
    /// </summary>
    NetworkError,

    /// <summary>
    /// Broker error - MQTT broker rejected the publish operation.
    /// </summary>
    BrokerError,

    /// <summary>
    /// Timeout - operation took too long to complete.
    /// </summary>
    Timeout,

    /// <summary>
    /// Invalid topic - topic name or pattern is malformed.
    /// </summary>
    InvalidTopic,

    /// <summary>
    /// Unknown error - unexpected failure during operation.
    /// </summary>
    Unknown
}