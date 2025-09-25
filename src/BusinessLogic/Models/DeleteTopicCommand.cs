using System;

namespace CrowsNestMqtt.BusinessLogic.Models;

/// <summary>
/// Command for deleting retained messages from MQTT topics.
/// Represents a request to publish empty retained messages to clear topics.
/// </summary>
public record DeleteTopicCommand
{
    /// <summary>
    /// The MQTT topic pattern to match for deletion.
    /// Can be a specific topic or pattern with wildcards.
    /// </summary>
    public required string TopicPattern { get; init; }

    /// <summary>
    /// Maximum number of topics that can be deleted in a single operation.
    /// If exceeded, requires confirmation or operation will be rejected.
    /// </summary>
    public int MaxTopicLimit { get; init; } = 500;

    /// <summary>
    /// Whether confirmation has been explicitly provided for operations
    /// exceeding the MaxTopicLimit or containing sensitive patterns.
    /// </summary>
    public bool RequireConfirmation { get; init; } = false;

    /// <summary>
    /// Timestamp when the command was created.
    /// Used for operation tracking and timeout calculation.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional timeout period for the entire operation.
    /// If not specified, uses default from configuration.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Degree of parallelism for processing multiple topics.
    /// If not specified, uses default from configuration.
    /// </summary>
    public int? ParallelismDegree { get; init; }
}