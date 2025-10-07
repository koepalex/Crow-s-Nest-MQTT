using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CrowsNestMQTT.BusinessLogic.Contracts
{
    /// <summary>
    /// Service contract for managing MQTT V5 request-response message correlations.
    /// Handles correlation-data tracking, response matching, and correlation lifecycle.
    /// </summary>
    public interface IMessageCorrelationService
    {
        /// <summary>
        /// Registers a request message that expects a response, creating a correlation entry.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <param name="correlationData">MQTT V5 correlation-data bytes for linking request and response</param>
        /// <param name="responseTopic">Expected topic where response will be published</param>
        /// <param name="ttlMinutes">Time-to-live in minutes for this correlation (default: 30)</param>
        /// <returns>True if correlation was created successfully, false if correlation already exists</returns>
        Task<bool> RegisterRequestAsync(string requestMessageId, byte[] correlationData, string responseTopic, int ttlMinutes = 30);

        /// <summary>
        /// Links a response message to an existing correlation using correlation-data.
        /// </summary>
        /// <param name="responseMessageId">Unique identifier for the response message</param>
        /// <param name="correlationData">MQTT V5 correlation-data bytes matching the original request</param>
        /// <param name="responseTopic">Topic where the response was received</param>
        /// <returns>True if response was linked successfully, false if no matching correlation found</returns>
        Task<bool> LinkResponseAsync(string responseMessageId, byte[] correlationData, string responseTopic);

        /// <summary>
        /// Gets the current status of a request message's expected response.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>Current response status (Hidden, Pending, Received, NavigationDisabled)</returns>
        Task<ResponseStatus> GetResponseStatusAsync(string requestMessageId);

        /// <summary>
        /// Retrieves all response message IDs linked to a specific request.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>Collection of response message IDs, empty if no responses received</returns>
        Task<IReadOnlyList<string>> GetResponseMessageIdsAsync(string requestMessageId);

        /// <summary>
        /// Gets the response topic associated with a request message.
        /// </summary>
        /// <param name="requestMessageId">Unique identifier for the request message</param>
        /// <returns>Response topic name, null if request has no correlation</returns>
        Task<string?> GetResponseTopicAsync(string requestMessageId);

        /// <summary>
        /// Removes expired correlations and cleans up resources.
        /// Called automatically by internal timer and can be invoked manually for testing.
        /// </summary>
        /// <returns>Number of correlations cleaned up</returns>
        Task<int> CleanupExpiredCorrelationsAsync();

        /// <summary>
        /// Gets statistics about current correlation state for monitoring and diagnostics.
        /// </summary>
        /// <returns>Correlation statistics including active count, pending count, and memory usage</returns>
        Task<CorrelationStatistics> GetStatisticsAsync();

        /// <summary>
        /// Event raised when a correlation status changes (pending → received, etc.).
        /// UI components subscribe to this for real-time status updates.
        /// </summary>
        event EventHandler<CorrelationStatusChangedEventArgs> CorrelationStatusChanged;
    }

    /// <summary>
    /// Represents the visual status of a request message in the UI.
    /// </summary>
    public enum ResponseStatus
    {
        /// <summary>No response-topic metadata present, no icon shown</summary>
        Hidden,

        /// <summary>Request sent, awaiting response, show clock icon</summary>
        Pending,

        /// <summary>Response received, show clickable arrow icon</summary>
        Received,

        /// <summary>Response topic not subscribed, show disabled clock icon</summary>
        NavigationDisabled
    }

    /// <summary>
    /// Event arguments for correlation status change notifications.
    /// </summary>
    public class CorrelationStatusChangedEventArgs : EventArgs
    {
        public string RequestMessageId { get; init; } = string.Empty;
        public ResponseStatus OldStatus { get; init; }
        public ResponseStatus NewStatus { get; init; }
        public string ResponseTopic { get; init; } = string.Empty;
        public DateTime ChangedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics about current correlation state for monitoring and diagnostics.
    /// </summary>
    public record CorrelationStatistics
    {
        public int ActiveCorrelations { get; init; }
        public int PendingCorrelations { get; init; }
        public int RespondedCorrelations { get; init; }
        public int ExpiredCorrelations { get; init; }
        public long EstimatedMemoryUsageBytes { get; init; }
        public DateTime LastCleanupAt { get; init; }
        public TimeSpan AverageResponseTime { get; init; }
    }
}