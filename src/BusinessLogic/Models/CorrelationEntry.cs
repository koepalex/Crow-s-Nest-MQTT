using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.BusinessLogic.Models
{
    /// <summary>
    /// Represents a correlation entry for efficient in-memory storage and lookup.
    /// Combines correlation metadata with response tracking for fast operations.
    /// </summary>
    public class CorrelationEntry
    {
        /// <summary>
        /// The correlation key for dictionary lookups.
        /// </summary>
        public CorrelationKey Key { get; init; }

        /// <summary>
        /// The full correlation details and lifecycle management.
        /// </summary>
        public MessageCorrelation Correlation { get; set; }

        /// <summary>
        /// Current visual status for UI display.
        /// Cached to avoid repeated calculations.
        /// </summary>
        public ResponseStatus Status { get; set; }

        /// <summary>
        /// List of response message IDs that have been linked to this correlation.
        /// Optimized for fast lookup and iteration.
        /// </summary>
        public HashSet<string> ResponseMessageIds { get; init; } = new();

        /// <summary>
        /// When this entry was last accessed or updated.
        /// Used for cleanup and memory management decisions.
        /// </summary>
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Creates a new correlation entry from a correlation key and message correlation.
        /// </summary>
        /// <param name="key">The correlation key for lookups.</param>
        /// <param name="correlation">The correlation details.</param>
        /// <exception cref="ArgumentNullException">Thrown when key or correlation is null.</exception>
        public CorrelationEntry(CorrelationKey key, MessageCorrelation correlation)
        {
            Key = key;
            Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
            Status = DetermineInitialStatus(correlation);

            // Initialize response IDs from correlation
            foreach (var responseId in correlation.ResponseMessageIds)
            {
                ResponseMessageIds.Add(responseId);
            }
        }

        /// <summary>
        /// Updates the entry with a new response message.
        /// </summary>
        /// <param name="responseMessageId">The response message ID to add.</param>
        /// <returns>True if the response was newly added, false if already present.</returns>
        public bool AddResponse(string responseMessageId)
        {
            if (string.IsNullOrEmpty(responseMessageId))
                return false;

            LastAccessedAt = DateTime.UtcNow;

            if (ResponseMessageIds.Add(responseMessageId))
            {
                // Update the correlation as well
                Correlation.LinkResponse(responseMessageId);

                // Update status if this is the first response
                if (Status == ResponseStatus.Pending)
                {
                    Status = ResponseStatus.Received;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the entry status based on current correlation state.
        /// </summary>
        public void RefreshStatus()
        {
            LastAccessedAt = DateTime.UtcNow;
            Status = DetermineCurrentStatus();
        }

        /// <summary>
        /// Checks if this entry should be cleaned up based on expiration.
        /// </summary>
        public bool ShouldCleanup => Correlation.IsExpired;

        /// <summary>
        /// Checks if this entry has any response messages.
        /// </summary>
        public bool HasResponses => ResponseMessageIds.Count > 0;

        /// <summary>
        /// Gets the estimated memory usage of this entry.
        /// </summary>
        public long EstimatedMemoryUsage
        {
            get
            {
                long usage = Correlation.EstimatedMemoryUsage;

                // Add HashSet overhead and string storage
                usage += ResponseMessageIds.Count * 50; // Estimated overhead per hash entry
                foreach (var responseId in ResponseMessageIds)
                {
                    usage += System.Text.Encoding.UTF8.GetByteCount(responseId);
                }

                // Add entry object overhead
                usage += 64; // Estimated object overhead

                return usage;
            }
        }

        /// <summary>
        /// Determines the initial status based on correlation state.
        /// </summary>
        private static ResponseStatus DetermineInitialStatus(MessageCorrelation correlation)
        {
            if (correlation.HasResponses)
            {
                return ResponseStatus.Received;
            }
            else if (correlation.IsExpired)
            {
                return ResponseStatus.NavigationDisabled;
            }
            else
            {
                return ResponseStatus.Pending;
            }
        }

        /// <summary>
        /// Determines the current status based on correlation and entry state.
        /// </summary>
        private ResponseStatus DetermineCurrentStatus()
        {
            if (Correlation.IsExpired)
            {
                return ResponseStatus.NavigationDisabled;
            }
            else if (HasResponses)
            {
                return ResponseStatus.Received;
            }
            else
            {
                return ResponseStatus.Pending;
            }
        }

        /// <summary>
        /// Creates a copy of this entry with updated properties.
        /// </summary>
        public CorrelationEntry WithUpdates(
            MessageCorrelation? correlation = null,
            ResponseStatus? status = null)
        {
            var newEntry = new CorrelationEntry(Key, correlation ?? Correlation)
            {
                Status = status ?? Status,
                LastAccessedAt = DateTime.UtcNow
            };

            // Copy response IDs
            foreach (var responseId in ResponseMessageIds)
            {
                newEntry.ResponseMessageIds.Add(responseId);
            }

            return newEntry;
        }

        public override string ToString()
        {
            var responseCount = ResponseMessageIds.Count;
            return $"Entry[{Key}] {Status} ({responseCount} responses, {(Correlation.IsExpired ? "expired" : "active")})";
        }
    }
}