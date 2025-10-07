using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Models;

namespace CrowsNestMqtt.BusinessLogic.Contracts
{
    /// <summary>
    /// Service for managing MQTT message correlations between requests and responses.
    /// Handles correlation registration, response linking, and lifecycle management.
    /// </summary>
    public interface IMessageCorrelationService
    {
        /// <summary>
        /// Registers a new request message for correlation tracking.
        /// </summary>
        /// <param name="requestMessageId">The ID of the request message.</param>
        /// <param name="correlationData">The correlation data from MQTT V5.</param>
        /// <param name="responseTopic">The expected response topic.</param>
        /// <param name="ttlMinutes">Time-to-live in minutes (default: 30).</param>
        /// <returns>True if registration succeeded, false if correlation already exists.</returns>
        Task<bool> RegisterRequestAsync(string requestMessageId, byte[] correlationData, string responseTopic, int ttlMinutes = 30);

        /// <summary>
        /// Links a response message to an existing correlation.
        /// </summary>
        /// <param name="responseMessageId">The ID of the response message.</param>
        /// <param name="correlationData">The correlation data to match.</param>
        /// <param name="responseTopic">The topic the response was received on.</param>
        /// <returns>True if linking succeeded, false if no matching correlation found.</returns>
        Task<bool> LinkResponseAsync(string responseMessageId, byte[] correlationData, string responseTopic);

        /// <summary>
        /// Gets the current response status for a request message.
        /// </summary>
        /// <param name="requestMessageId">The request message ID.</param>
        /// <returns>The current response status.</returns>
        Task<ResponseStatus> GetResponseStatusAsync(string requestMessageId);

        /// <summary>
        /// Gets all response message IDs linked to a request.
        /// </summary>
        /// <param name="requestMessageId">The request message ID.</param>
        /// <returns>List of response message IDs.</returns>
        Task<IReadOnlyList<string>> GetResponseMessageIdsAsync(string requestMessageId);

        /// <summary>
        /// Gets the response topic for a request message.
        /// </summary>
        /// <param name="requestMessageId">The request message ID.</param>
        /// <returns>The response topic, or null if not found.</returns>
        Task<string?> GetResponseTopicAsync(string requestMessageId);

        /// <summary>
        /// Cleans up expired correlations and returns the count removed.
        /// </summary>
        /// <returns>Number of correlations cleaned up.</returns>
        Task<int> CleanupExpiredCorrelationsAsync();

        /// <summary>
        /// Gets correlation statistics for monitoring.
        /// </summary>
        /// <returns>Statistics about active correlations.</returns>
        Task<CorrelationStatistics> GetStatisticsAsync();

        /// <summary>
        /// Event raised when correlation status changes.
        /// </summary>
        event EventHandler<CorrelationStatusChangedEventArgs> CorrelationStatusChanged;
    }

    /// <summary>
    /// Statistics about correlation service state.
    /// </summary>
    public class CorrelationStatistics
    {
        public int ActiveCorrelations { get; init; }
        public int TotalCorrelations { get; init; }
        public int PendingCorrelations { get; init; }
        public int RespondedCorrelations { get; init; }
        public int ExpiredCorrelations { get; init; }
        public long EstimatedMemoryUsageBytes { get; init; }
        public long EstimatedMemoryUsage { get; init; }
        public DateTime LastCleanupAt { get; init; }
        public TimeSpan AverageResponseTime { get; init; }
    }

    /// <summary>
    /// Event arguments for correlation status changes.
    /// </summary>
    public class CorrelationStatusChangedEventArgs : EventArgs
    {
        public string RequestMessageId { get; init; } = string.Empty;
        public ResponseStatus NewStatus { get; init; }
        public ResponseStatus PreviousStatus { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}