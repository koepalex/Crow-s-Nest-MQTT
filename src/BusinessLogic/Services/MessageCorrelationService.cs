using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;

namespace CrowsNestMqtt.BusinessLogic.Services
{
    /// <summary>
    /// Implementation of IMessageCorrelationService for managing MQTT message correlations.
    /// Thread-safe implementation with concurrent collections for high-performance scenarios.
    /// </summary>
    public class MessageCorrelationService : IMessageCorrelationService
    {
        private readonly ConcurrentDictionary<CorrelationKey, CorrelationEntry> _correlations = new();
        private readonly ConcurrentDictionary<string, CorrelationKey> _requestMessageIndex = new();

        /// <inheritdoc />
        public event EventHandler<CorrelationStatusChangedEventArgs>? CorrelationStatusChanged;

        /// <inheritdoc />
        public Task<bool> RegisterRequestAsync(string requestMessageId, byte[] correlationData, string responseTopic, int ttlMinutes = 30)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                throw new ArgumentException("Request message ID cannot be null or empty", nameof(requestMessageId));

            if (correlationData == null || correlationData.Length == 0)
                throw new ArgumentException("Correlation data cannot be null or empty", nameof(correlationData));

            if (string.IsNullOrEmpty(responseTopic))
                throw new ArgumentException("Response topic cannot be null or empty", nameof(responseTopic));

            var correlationKey = new CorrelationKey(correlationData);

            // Check if correlation already exists
            if (_correlations.ContainsKey(correlationKey))
                return Task.FromResult(false);

            var correlation = new MessageCorrelation(correlationData, requestMessageId, responseTopic, ttlMinutes);
            var entry = new CorrelationEntry(correlationKey, correlation);

            // Add to both dictionaries atomically
            if (_correlations.TryAdd(correlationKey, entry) &&
                _requestMessageIndex.TryAdd(requestMessageId, correlationKey))
            {
                // Raise status changed event
                RaiseCorrelationStatusChanged(requestMessageId, ResponseStatus.Pending, ResponseStatus.Hidden);
                return Task.FromResult(true);
            }

            // If we failed to add to request index, remove from correlations
            _correlations.TryRemove(correlationKey, out _);
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public Task<bool> LinkResponseAsync(string responseMessageId, byte[] correlationData, string responseTopic)
        {
            if (string.IsNullOrEmpty(responseMessageId))
                throw new ArgumentException("Response message ID cannot be null or empty", nameof(responseMessageId));

            if (correlationData == null || correlationData.Length == 0)
                throw new ArgumentException("Correlation data cannot be null or empty", nameof(correlationData));

            if (string.IsNullOrEmpty(responseTopic))
                throw new ArgumentException("Response topic cannot be null or empty", nameof(responseTopic));

            var correlationKey = new CorrelationKey(correlationData);

            if (!_correlations.TryGetValue(correlationKey, out var entry))
                return Task.FromResult(false);

            // Verify response topic matches
            if (!string.Equals(entry.Correlation.ResponseTopic, responseTopic, StringComparison.Ordinal))
                return Task.FromResult(false);

            var previousStatus = entry.Status;
            var wasAdded = entry.AddResponse(responseMessageId);

            if (wasAdded)
            {
                // Update the entry in the dictionary
                _correlations.TryUpdate(correlationKey, entry, entry);

                // Raise status changed event if status changed
                if (entry.Status != previousStatus)
                {
                    RaiseCorrelationStatusChanged(entry.Correlation.RequestMessageId, entry.Status, previousStatus);
                }
            }

            return Task.FromResult(wasAdded);
        }

        /// <inheritdoc />
        public Task<ResponseStatus> GetResponseStatusAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult(ResponseStatus.Hidden);

            if (!_requestMessageIndex.TryGetValue(requestMessageId, out var correlationKey))
            {
                Serilog.Log.Warning("GetResponseStatus: Request {RequestMessageId} not found in index", requestMessageId);
                return Task.FromResult(ResponseStatus.Hidden);
            }

            if (!_correlations.TryGetValue(correlationKey, out var entry))
            {
                Serilog.Log.Warning("GetResponseStatus: Correlation key not found for request {RequestMessageId}", requestMessageId);
                return Task.FromResult(ResponseStatus.Hidden);
            }

            // Refresh status before returning
            entry.RefreshStatus();
            Serilog.Log.Information("GetResponseStatus: Request {RequestMessageId} has status {Status}, {ResponseCount} responses",
                requestMessageId, entry.Status, entry.ResponseMessageIds.Count);
            return Task.FromResult(entry.Status);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<string>> GetResponseMessageIdsAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            if (!_requestMessageIndex.TryGetValue(requestMessageId, out var correlationKey))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            if (!_correlations.TryGetValue(correlationKey, out var entry))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            return Task.FromResult<IReadOnlyList<string>>(entry.ResponseMessageIds.ToArray());
        }

        /// <inheritdoc />
        public Task<string?> GetResponseTopicAsync(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return Task.FromResult<string?>(null);

            if (!_requestMessageIndex.TryGetValue(requestMessageId, out var correlationKey))
                return Task.FromResult<string?>(null);

            if (!_correlations.TryGetValue(correlationKey, out var entry))
                return Task.FromResult<string?>(null);

            return Task.FromResult<string?>(entry.Correlation.ResponseTopic);
        }

        /// <inheritdoc />
        public Task<int> CleanupExpiredCorrelationsAsync()
        {
            var expiredKeys = new List<CorrelationKey>();

            // Find expired correlations
            foreach (var kvp in _correlations)
            {
                if (kvp.Value.ShouldCleanup)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            int cleanedUp = 0;

            // Remove expired correlations
            foreach (var key in expiredKeys)
            {
                if (_correlations.TryRemove(key, out var entry))
                {
                    // Also remove from request message index
                    _requestMessageIndex.TryRemove(entry.Correlation.RequestMessageId, out _);

                    // Raise status changed event
                    RaiseCorrelationStatusChanged(entry.Correlation.RequestMessageId, ResponseStatus.Hidden, entry.Status);

                    cleanedUp++;
                }
            }

            return Task.FromResult(cleanedUp);
        }

        /// <inheritdoc />
        public Task<CorrelationStatistics> GetStatisticsAsync()
        {
            var totalCorrelations = _correlations.Count;
            var pendingCorrelations = 0;
            var respondedCorrelations = 0;
            var expiredCorrelations = 0;
            long estimatedMemoryUsage = 0;

            foreach (var entry in _correlations.Values)
            {
                estimatedMemoryUsage += entry.EstimatedMemoryUsage;

                switch (entry.Status)
                {
                    case ResponseStatus.Pending:
                        pendingCorrelations++;
                        break;
                    case ResponseStatus.Received:
                        respondedCorrelations++;
                        break;
                    case ResponseStatus.NavigationDisabled:
                        if (entry.Correlation.IsExpired)
                            expiredCorrelations++;
                        break;
                }
            }

            return Task.FromResult(new CorrelationStatistics
            {
                ActiveCorrelations = totalCorrelations,
                TotalCorrelations = totalCorrelations,
                PendingCorrelations = pendingCorrelations,
                RespondedCorrelations = respondedCorrelations,
                ExpiredCorrelations = expiredCorrelations,
                EstimatedMemoryUsageBytes = estimatedMemoryUsage,
                EstimatedMemoryUsage = estimatedMemoryUsage,
                LastCleanupAt = DateTime.UtcNow,
                AverageResponseTime = TimeSpan.Zero
            });
        }

        /// <summary>
        /// Raises the CorrelationStatusChanged event.
        /// </summary>
        private void RaiseCorrelationStatusChanged(string requestMessageId, ResponseStatus newStatus, ResponseStatus previousStatus)
        {
            CorrelationStatusChanged?.Invoke(this, new CorrelationStatusChangedEventArgs
            {
                RequestMessageId = requestMessageId,
                NewStatus = newStatus,
                PreviousStatus = previousStatus
            });
        }

        /// <summary>
        /// Gets correlation entry by request message ID (for internal use).
        /// </summary>
        internal CorrelationEntry? GetCorrelationEntry(string requestMessageId)
        {
            if (string.IsNullOrEmpty(requestMessageId))
                return null;

            if (!_requestMessageIndex.TryGetValue(requestMessageId, out var correlationKey))
                return null;

            _correlations.TryGetValue(correlationKey, out var entry);
            return entry;
        }

        /// <summary>
        /// Gets all active correlations (for testing and debugging).
        /// </summary>
        internal IReadOnlyDictionary<CorrelationKey, CorrelationEntry> GetAllCorrelations()
        {
            return _correlations.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}