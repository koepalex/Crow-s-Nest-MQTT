using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using Microsoft.Extensions.Logging;

namespace CrowsNestMqtt.UI.Commands;

/// <summary>
/// Extension methods for ICommandProcessor to handle delete topic commands.
/// Provides integration between UI command processing and delete topic business logic.
/// </summary>
public static class DeleteTopicCommandExtensions
{
    /// <summary>
    /// Executes the delete topic command with the specified arguments.
    /// </summary>
    /// <param name="processor">The command processor</param>
    /// <param name="arguments">Command arguments</param>
    /// <param name="deleteTopicService">The delete topic service for actual deletion operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command execution result</returns>
    public static Task<ICommandProcessor.CommandExecutionResult> ExecuteDeleteTopicCommand(
        this ICommandProcessor processor,
        string[] arguments,
        IDeleteTopicService deleteTopicService,
        CancellationToken cancellationToken = default)
    {
        return ExecuteDeleteTopicCommandAsync(processor, arguments, deleteTopicService, cancellationToken);
    }

    private static async Task<ICommandProcessor.CommandExecutionResult> ExecuteDeleteTopicCommandAsync(
        ICommandProcessor processor,
        string[] arguments,
        IDeleteTopicService deleteTopicService,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate arguments
            if (arguments.Length > 1)
            {
                return new ICommandProcessor.CommandExecutionResult(
                    false,
                    "Invalid arguments for :deletetopic. Expected: :deletetopic [topic-pattern]");
            }

            string topicPattern;

            if (arguments.Length == 0)
            {
                // No arguments provided - caller should handle selected topic logic
                return new ICommandProcessor.CommandExecutionResult(
                    false,
                    "Topic pattern required. Usage: :deletetopic [topic-pattern]");
            }
            else
            {
                topicPattern = arguments[0];
            }

            // Validate topic pattern
            if (string.IsNullOrWhiteSpace(topicPattern))
            {
                return new ICommandProcessor.CommandExecutionResult(
                    false,
                    "Topic pattern cannot be empty");
            }

            // For delete topic operations, preserve exact topic names to enable direct deletion
            // Converting to hierarchical patterns (adding /#) interferes with direct retained message clearing
            // topicPattern = ConvertToHierarchicalPattern(topicPattern);

            // Check for invalid MQTT topic characters
            if (topicPattern.Contains('#') && !IsValidWildcardUsage(topicPattern))
            {
                return new ICommandProcessor.CommandExecutionResult(
                    false,
                    $"Invalid topic pattern: {topicPattern}");
            }

            // Get configuration values
            var config = GetDeleteTopicConfiguration();

            // Create the delete topic command
            var deleteCommand = new DeleteTopicCommand
            {
                TopicPattern = topicPattern,
                MaxTopicLimit = config.MaxTopicLimit,
                RequireConfirmation = false, // Always confirmed through UI action
                ParallelismDegree = config.ParallelismDegree,
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            // Execute the actual deletion using the service
            var result = await deleteTopicService.DeleteTopicAsync(deleteCommand, cancellationToken).ConfigureAwait(false);
            return new ICommandProcessor.CommandExecutionResult(
                result.Status == DeleteOperationStatus.CompletedSuccessfully,
                result.SummaryMessage ?? $"Delete operation completed with status: {result.Status}");

        }
        catch (OperationCanceledException)
        {
            return new ICommandProcessor.CommandExecutionResult(
                false,
                "Delete topic command was cancelled");
        }
        catch (Exception ex)
        {
            return new ICommandProcessor.CommandExecutionResult(
                false,
                $"Delete topic command failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts exact topic patterns to hierarchical patterns for subtopic deletion.
    /// For example, "foo" becomes "foo/#" to match "foo", "foo/bar", "foo/baz/qux", etc.
    /// Preserves existing wildcard patterns like "foo/+" or "foo/#".
    /// </summary>
    /// <param name="topicPattern">The original topic pattern</param>
    /// <returns>The hierarchical topic pattern</returns>
    private static string ConvertToHierarchicalPattern(string topicPattern)
    {
        // If the pattern already contains wildcards, preserve it as-is
        if (topicPattern.Contains('+') || topicPattern.Contains('#'))
        {
            return topicPattern;
        }

        // Convert exact topics to hierarchical patterns
        // "foo" -> "foo/#" to match all subtopics
        return topicPattern.TrimEnd('/') + "/#";
    }

    /// <summary>
    /// Gets configuration values for delete topic operations.
    /// TODO: T026 - Add configuration support integration
    /// </summary>
    /// <returns>Configuration values for limits and timeouts</returns>
    private static (int MaxTopicLimit, int ParallelismDegree, int TimeoutSeconds) GetDeleteTopicConfiguration()
    {
        // TODO: T026 - Load from actual configuration
        // For now, return hardcoded defaults that match the SettingsData defaults
        return (MaxTopicLimit: 500, ParallelismDegree: 4, TimeoutSeconds: 5);
    }


    /// <summary>
    /// Validates wildcard usage in MQTT topic patterns.
    /// </summary>
    /// <param name="topicPattern">Topic pattern to validate</param>
    /// <returns>True if wildcard usage is valid</returns>
    private static bool IsValidWildcardUsage(string topicPattern)
    {
        // Multi-level wildcard must be at the end
        var hashIndex = topicPattern.IndexOf('#');
        if (hashIndex >= 0 && hashIndex != topicPattern.Length - 1)
        {
            // Check if it's at the end after a slash
            if (hashIndex != topicPattern.Length - 2 || topicPattern[hashIndex + 1] != '/')
            {
                return false;
            }
        }

        // Single-level wildcards must be properly separated
        var plusIndices = topicPattern.Select((c, i) => new { Char = c, Index = i })
                                     .Where(x => x.Char == '+')
                                     .Select(x => x.Index);

        foreach (var index in plusIndices)
        {
            // Check boundaries
            if (index > 0 && topicPattern[index - 1] != '/')
                return false;
            if (index < topicPattern.Length - 1 && topicPattern[index + 1] != '/')
                return false;
        }

        return true;
    }
}