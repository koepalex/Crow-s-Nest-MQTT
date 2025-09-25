using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.Utils.Models;

namespace CrowsNestMqtt.BusinessLogic.Services;

/// <summary>
/// Service for deleting retained MQTT messages by publishing empty retained messages.
/// Provides functionality to clear topics and their subtopics with performance monitoring.
/// </summary>
public interface IDeleteTopicService
{
    /// <summary>
    /// Deletes retained messages from topics matching the specified pattern.
    /// Publishes empty retained messages to clear the topics.
    /// </summary>
    /// <param name="command">Command containing topic pattern and operation parameters</param>
    /// <param name="cancellationToken">Token for cancelling the operation</param>
    /// <returns>Result containing operation status and metrics</returns>
    Task<DeleteTopicResult> DeleteTopicAsync(DeleteTopicCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all topics with retained messages that match the specified pattern.
    /// Used for preview before deletion and validation.
    /// </summary>
    /// <param name="topicPattern">MQTT topic pattern to match</param>
    /// <param name="cancellationToken">Token for cancelling the operation</param>
    /// <returns>List of matching topic names</returns>
    Task<IList<string>> FindTopicsWithRetainedMessages(string topicPattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a delete operation before execution.
    /// Checks topic limits, pattern validity, and permissions.
    /// </summary>
    /// <param name="topicPattern">MQTT topic pattern to validate</param>
    /// <param name="maxTopicLimit">Maximum allowed topics for operation</param>
    /// <returns>Validation result with any errors or warnings</returns>
    ValidationResult ValidateDeleteOperation(string topicPattern, int maxTopicLimit);

    /// <summary>
    /// Estimates the performance impact of deleting the specified topics.
    /// Used for UI responsiveness planning and user warnings.
    /// </summary>
    /// <param name="topicCount">Number of topics to be deleted</param>
    /// <returns>Estimated duration and performance metrics</returns>
    Task<OperationMetrics> EstimatePerformanceImpact(int topicCount);
}