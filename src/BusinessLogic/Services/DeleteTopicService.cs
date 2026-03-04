using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.Utils.Models;
using MQTTnet;
using MQTTnet.Protocol;
using Microsoft.Extensions.Logging;

namespace CrowsNestMqtt.BusinessLogic.Services;

/// <summary>
/// Service implementation for deleting retained MQTT messages.
/// Publishes empty retained messages to clear topics with parallel processing support.
/// </summary>
public class DeleteTopicService : IDeleteTopicService
{
    private readonly IMqttService _mqttService;
    private readonly ILogger<DeleteTopicService> _logger;
    private readonly int _defaultParallelismDegree;
    private readonly TimeSpan _defaultTimeout;

    public DeleteTopicService(
        IMqttService mqttService,
        ILogger<DeleteTopicService> logger,
        int defaultParallelismDegree = 4,
        TimeSpan? defaultTimeout = null)
    {
        _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultParallelismDegree = defaultParallelismDegree;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc />
    public async Task<DeleteTopicResult> DeleteTopicAsync(DeleteTopicCommand command, CancellationToken cancellationToken = default)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var stopwatch = Stopwatch.StartNew();
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting delete operation for pattern: {Pattern}", command.TopicPattern);

            // Validate the operation first
            var validation = ValidateDeleteOperation(command.TopicPattern, command.MaxTopicLimit);
            if (!validation.IsValid)
            {
                return new DeleteTopicResult
                {
                    Status = DeleteOperationStatus.Failed,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    OperationDuration = stopwatch.Elapsed,
                    OriginalCommand = command,
                    SummaryMessage = string.Join("; ", validation.ErrorMessages)
                };
            }

            // For exact topic names (no wildcards), bypass discovery and directly attempt to clear
            List<string> topicsToDelete;
            if (!command.TopicPattern.Contains('+') && !command.TopicPattern.Contains('#'))
            {
                // Direct topic deletion - no need to discover, just attempt to clear the retained message
                topicsToDelete = new List<string> { command.TopicPattern };
                _logger.LogInformation("Direct topic deletion for: {Topic}", command.TopicPattern);
            }
            else
            {
                // Find topics to delete using pattern matching
                topicsToDelete = (await FindTopicsWithRetainedMessages(command.TopicPattern, cancellationToken).ConfigureAwait(false)).ToList();

                if (topicsToDelete.Count == 0)
                {
                    _logger.LogInformation("No topics found matching pattern: {Pattern}", command.TopicPattern);
                    return new DeleteTopicResult
                    {
                        Status = DeleteOperationStatus.CompletedSuccessfully,
                        TotalTopicsFound = 0,
                        SuccessfulDeletions = 0,
                        StartTime = startTime,
                        EndTime = DateTime.UtcNow,
                        OperationDuration = stopwatch.Elapsed,
                        OriginalCommand = command,
                        SummaryMessage = "No topics found matching the specified pattern"
                    };
                }
            }

            // Check if confirmation is required
            if (topicsToDelete.Count > command.MaxTopicLimit && !command.RequireConfirmation)
            {
                _logger.LogWarning("Topic count {Count} exceeds limit {Limit} without confirmation",
                    topicsToDelete.Count, command.MaxTopicLimit);
                return new DeleteTopicResult
                {
                    Status = DeleteOperationStatus.AwaitingConfirmation,
                    TotalTopicsFound = topicsToDelete.Count,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    OperationDuration = stopwatch.Elapsed,
                    OriginalCommand = command,
                    SummaryMessage = $"Found {topicsToDelete.Count} topics. Confirmation required to proceed (exceeds limit of {command.MaxTopicLimit})"
                };
            }

            // Execute parallel deletion
            var parallelismDegree = command.ParallelismDegree ?? _defaultParallelismDegree;
            var timeout = command.Timeout ?? _defaultTimeout;

            var result = await ExecuteParallelDeletion(topicsToDelete, parallelismDegree, timeout, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            var endTime = DateTime.UtcNow;

            // Calculate performance metrics
            var metrics = new OperationMetrics
            {
                TopicsPerSecond = result.SuccessfulDeletions / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001),
                PeakParallelTasks = parallelismDegree,
                AverageResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds / Math.Max(topicsToDelete.Count, 1),
                UIRemainedResponsive = stopwatch.Elapsed.TotalSeconds < 10 // Heuristic for UI responsiveness
            };

            var finalResult = result with
            {
                StartTime = startTime,
                EndTime = endTime,
                OperationDuration = stopwatch.Elapsed,
                OriginalCommand = command,
                Metrics = metrics,
                SummaryMessage = GenerateSummaryMessage(result.SuccessfulDeletions, result.FailedDeletions.Count, validation.WarningMessages)
            };

            _logger.LogInformation("Delete operation completed. Success: {Success}, Failed: {Failed}, Duration: {Duration}ms",
                finalResult.SuccessfulDeletions, finalResult.FailedDeletions.Count, stopwatch.ElapsedMilliseconds);

            return finalResult;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Delete operation was cancelled");
            return new DeleteTopicResult
            {
                Status = DeleteOperationStatus.Aborted,
                WasCancelled = true,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                OperationDuration = stopwatch.Elapsed,
                OriginalCommand = command,
                SummaryMessage = "Operation was cancelled by user"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete operation failed with unexpected error");
            return new DeleteTopicResult
            {
                Status = DeleteOperationStatus.Failed,
                StartTime = startTime,
                EndTime = DateTime.UtcNow,
                OperationDuration = stopwatch.Elapsed,
                OriginalCommand = command,
                SummaryMessage = $"Operation failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<IList<string>> FindTopicsWithRetainedMessages(string topicPattern, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding topics with retained messages for pattern: {Pattern}", topicPattern);

        // Use the MQTT service's existing buffered topics to find matching topics
        var bufferedTopics = _mqttService.GetBufferedTopics();
        var matchingTopics = new List<string>();

        foreach (var topic in bufferedTopics)
        {
            if (IsTopicMatchingPattern(topic, topicPattern))
            {
                // Check if this topic has retained messages by examining the buffer
                var messages = _mqttService.GetBufferedMessagesForTopic(topic);
                if (messages?.Any(m => m.Message.Retain) == true)
                {
                    matchingTopics.Add(topic);
                }
            }
        }

        _logger.LogDebug("Found {Count} topics matching pattern {Pattern}", matchingTopics.Count, topicPattern);
        await Task.CompletedTask.ConfigureAwait(false); // Maintain async interface

        return matchingTopics;
    }

    /// <inheritdoc />
    public ValidationResult ValidateDeleteOperation(string topicPattern, int maxTopicLimit)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate topic pattern
        if (string.IsNullOrWhiteSpace(topicPattern))
        {
            errors.Add("Topic pattern cannot be empty");
        }
        else
        {
            // Check for invalid MQTT topic characters
            if (topicPattern.Contains('#') && !topicPattern.EndsWith("#") && !topicPattern.EndsWith("/#"))
            {
                errors.Add("Multi-level wildcard '#' must be at the end of topic pattern");
            }

            if (topicPattern.Contains('+') && !IsValidSingleLevelWildcard(topicPattern))
            {
                errors.Add("Single-level wildcard '+' must be separated by '/' characters");
            }

            // Check for potentially dangerous patterns
            if (topicPattern == "#" || topicPattern == "+" ||
                topicPattern.EndsWith("#") || topicPattern.EndsWith("+") ||
                topicPattern.Contains("+"))
            {
                warnings.Add("This pattern may match a very large number of topics");
            }
        }

        // Validate limits
        if (maxTopicLimit <= 0)
        {
            errors.Add("Maximum topic limit must be greater than 0");
        }
        else if (maxTopicLimit > 10000)
        {
            errors.Add("Maximum topic limit exceeds system maximum (10,000)");
        }

        if (errors.Any())
        {
            return ValidationResult.Failure(errors.ToArray());
        }

        if (warnings.Any())
        {
            return ValidationResult.SuccessWithWarnings(warnings.ToArray());
        }

        return ValidationResult.Success();
    }

    /// <inheritdoc />
    public async Task<OperationMetrics> EstimatePerformanceImpact(int topicCount)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Simulate async operation

        // Rough performance estimates based on typical MQTT performance
        var estimatedDurationSeconds = Math.Max(topicCount / 200.0, 0.5); // ~200 topics/second baseline
        var parallelTasks = Math.Min(topicCount, _defaultParallelismDegree);

        return new OperationMetrics
        {
            TopicsPerSecond = topicCount / estimatedDurationSeconds,
            PeakParallelTasks = parallelTasks,
            AverageResponseTimeMs = (estimatedDurationSeconds * 1000) / topicCount,
            UIRemainedResponsive = estimatedDurationSeconds < 5
        };
    }

    private async Task<DeleteTopicResult> ExecuteParallelDeletion(
        IList<string> topics,
        int parallelismDegree,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var failures = new List<TopicDeletionFailure>();
        var successCount = 0;

        // Use semaphore to control parallelism
        using var semaphore = new SemaphoreSlim(parallelismDegree, parallelismDegree);
        var tasks = topics.Select(async topic =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await DeleteSingleTopic(topic, timeout, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref successCount);
            }
            catch (Exception ex)
            {
                var failure = new TopicDeletionFailure
                {
                    TopicName = topic,
                    ErrorType = ClassifyError(ex),
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    IsRetryable = IsRetryableError(ex)
                };

                lock (failures)
                {
                    failures.Add(failure);
                }

                _logger.LogWarning(ex, "Failed to delete topic: {Topic}", topic);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var status = failures.Count == 0 ? DeleteOperationStatus.CompletedSuccessfully :
                    successCount > 0 ? DeleteOperationStatus.CompletedWithWarnings :
                    DeleteOperationStatus.Failed;

        return new DeleteTopicResult
        {
            Status = status,
            TotalTopicsFound = topics.Count,
            SuccessfulDeletions = successCount,
            FailedDeletions = failures
        };
    }

    private async Task DeleteSingleTopic(string topic, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // Publish empty retained message to clear the topic using IMqttService
        await _mqttService.ClearRetainedMessageAsync(
            topic,
            qos: MqttQualityOfServiceLevel.AtLeastOnce, // Use QoS 1 for reliability
            combinedCts.Token).ConfigureAwait(false);

        _logger.LogDebug("Cleared retained message for topic: {Topic}", topic);
    }

    private static DeletionErrorType ClassifyError(Exception exception)
    {
        return exception switch
        {
            OperationCanceledException => DeletionErrorType.Timeout,
            UnauthorizedAccessException => DeletionErrorType.PermissionDenied,
            MQTTnet.Exceptions.MqttCommunicationException => DeletionErrorType.NetworkError,
            MQTTnet.Exceptions.MqttProtocolViolationException => DeletionErrorType.BrokerError,
            ArgumentException => DeletionErrorType.InvalidTopic,
            InvalidOperationException when exception.Message.Contains("not connected") => DeletionErrorType.NetworkError,
            _ => DeletionErrorType.Unknown
        };
    }

    private static bool IsRetryableError(Exception exception)
    {
        return exception switch
        {
            MQTTnet.Exceptions.MqttCommunicationException => true,
            OperationCanceledException when exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            InvalidOperationException when exception.Message.Contains("not connected") => true,
            _ => false
        };
    }

    private static bool IsValidSingleLevelWildcard(string topicPattern)
    {
        // Single-level wildcard must be a complete topic level
        var parts = topicPattern.Split('/');

        foreach (var part in parts)
        {
            if (part.Contains('+'))
            {
                // If the part contains '+', it must be exactly '+'
                if (part != "+")
                    return false;
            }
        }

        return true;
    }

    private static bool IsTopicMatchingPattern(string topic, string pattern)
    {
        // Convert MQTT wildcard pattern to regex pattern
        // + matches a single level: "sensor/+/temperature" matches "sensor/room1/temperature"
        // # matches multiple levels: "sensor/#" matches "sensor/room1/temperature"

        if (pattern == topic)
            return true;

        // Handle exact match first
        if (!pattern.Contains('+') && !pattern.Contains('#'))
        {
            return pattern == topic;
        }

        // Convert MQTT pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\+", @"[^/]+")     // + matches single level (not including '/')
            .Replace(@"\#", @".*")        // # matches everything
            + "$";

        return Regex.IsMatch(topic, regexPattern);
    }

    private static string GenerateSummaryMessage(int successful, int failed, IList<string> warnings)
    {
        var parts = new List<string>();

        if (successful > 0)
        {
            parts.Add($"{successful} topics cleared successfully");
        }

        if (failed > 0)
        {
            parts.Add($"{failed} topics failed");
        }

        if (warnings.Any())
        {
            parts.Add($"{warnings.Count} warnings");
        }

        return string.Join(", ", parts);
    }
}