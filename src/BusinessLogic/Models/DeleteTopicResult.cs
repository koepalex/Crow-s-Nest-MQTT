using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.BusinessLogic.Models;

/// <summary>
/// Result of a delete topic operation.
/// Contains summary information and details about the operation outcome.
/// </summary>
public record DeleteTopicResult
{
    /// <summary>
    /// Overall status of the delete operation.
    /// </summary>
    public required DeleteOperationStatus Status { get; init; }

    /// <summary>
    /// Total number of topics found matching the pattern.
    /// </summary>
    public int TotalTopicsFound { get; init; }

    /// <summary>
    /// Number of topics successfully deleted (empty retained messages published).
    /// </summary>
    public int SuccessfulDeletions { get; init; }

    /// <summary>
    /// List of topics that failed to be deleted, with error details.
    /// </summary>
    public IList<TopicDeletionFailure> FailedDeletions { get; init; } = new List<TopicDeletionFailure>();

    /// <summary>
    /// Total duration of the operation from start to completion.
    /// </summary>
    public TimeSpan OperationDuration { get; init; }

    /// <summary>
    /// Timestamp when the operation started.
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the operation completed.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// The original command that triggered this operation.
    /// </summary>
    public DeleteTopicCommand? OriginalCommand { get; init; }

    /// <summary>
    /// Whether the operation was cancelled by the user.
    /// </summary>
    public bool WasCancelled { get; init; }

    /// <summary>
    /// Human-readable summary message about the operation outcome.
    /// </summary>
    public string? SummaryMessage { get; init; }

    /// <summary>
    /// Performance metrics for the operation.
    /// </summary>
    public OperationMetrics? Metrics { get; init; }
}

/// <summary>
/// Performance metrics for delete operations.
/// </summary>
public record OperationMetrics
{
    /// <summary>
    /// Average processing rate in topics per second.
    /// </summary>
    public double TopicsPerSecond { get; init; }

    /// <summary>
    /// Peak parallel tasks executing simultaneously.
    /// </summary>
    public int PeakParallelTasks { get; init; }

    /// <summary>
    /// Average response time per topic deletion in milliseconds.
    /// </summary>
    public double AverageResponseTimeMs { get; init; }

    /// <summary>
    /// Whether UI remained responsive during operation.
    /// </summary>
    public bool UIRemainedResponsive { get; init; } = true;
}