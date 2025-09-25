using System;

namespace CrowsNestMqtt.BusinessLogic.Models;

/// <summary>
/// Represents a failure to delete a specific topic during a delete operation.
/// Contains details about the topic and reason for failure.
/// </summary>
public record TopicDeletionFailure
{
    /// <summary>
    /// The MQTT topic name that failed to be deleted.
    /// </summary>
    public required string TopicName { get; init; }

    /// <summary>
    /// The type of error that caused the deletion to fail.
    /// </summary>
    public required DeletionErrorType ErrorType { get; init; }

    /// <summary>
    /// Human-readable error message describing the failure.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// The underlying exception that caused the failure, if available.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Timestamp when the failure occurred.
    /// </summary>
    public DateTime FailureTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this failure should be retried automatically.
    /// Some transient errors like network timeouts can be retried.
    /// </summary>
    public bool IsRetryable { get; init; } = false;
}