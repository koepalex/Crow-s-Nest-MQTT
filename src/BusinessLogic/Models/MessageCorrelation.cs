using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.BusinessLogic.Models
{
    /// <summary>
    /// Manages the relationship between request and response messages using MQTT V5 correlation-data.
    /// Tracks correlation lifecycle, status transitions, and cleanup timing.
    /// </summary>
    public class MessageCorrelation
    {
        /// <summary>
        /// The unique correlation identifier from MQTT V5 correlation-data property.
        /// </summary>
        public byte[] CorrelationData { get; init; } = Array.Empty<byte>();

        /// <summary>
        /// Reference to the original request message.
        /// </summary>
        public string RequestMessageId { get; init; } = string.Empty;

        /// <summary>
        /// Expected topic for response messages.
        /// </summary>
        public string ResponseTopic { get; init; } = string.Empty;

        /// <summary>
        /// When the request was initiated.
        /// </summary>
        public DateTime RequestTimestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// References to received response messages.
        /// Multiple responses can share the same correlation-data for broadcast scenarios.
        /// </summary>
        public List<string> ResponseMessageIds { get; init; } = new();

        /// <summary>
        /// Current state of the correlation.
        /// </summary>
        public CorrelationStatus Status { get; set; } = CorrelationStatus.Pending;

        /// <summary>
        /// When this correlation should be cleaned up.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// When the first response was received (if any).
        /// </summary>
        public DateTime? FirstResponseTimestamp { get; set; }

        /// <summary>
        /// When the most recent response was received (if any).
        /// </summary>
        public DateTime? LastResponseTimestamp { get; set; }

        /// <summary>
        /// Creates a new message correlation with default TTL.
        /// </summary>
        /// <param name="correlationData">The correlation data bytes.</param>
        /// <param name="requestMessageId">The request message ID.</param>
        /// <param name="responseTopic">The expected response topic.</param>
        /// <param name="ttlMinutes">Time-to-live in minutes (default: 30).</param>
        public MessageCorrelation(byte[] correlationData, string requestMessageId, string responseTopic, int ttlMinutes = 30)
        {
            CorrelationData = correlationData ?? throw new ArgumentNullException(nameof(correlationData));
            RequestMessageId = requestMessageId ?? throw new ArgumentNullException(nameof(requestMessageId));
            ResponseTopic = responseTopic ?? throw new ArgumentNullException(nameof(responseTopic));
            RequestTimestamp = DateTime.UtcNow;
            ExpiresAt = RequestTimestamp.AddMinutes(ttlMinutes);
        }

        /// <summary>
        /// Parameterless constructor for serialization/deserialization.
        /// </summary>
        public MessageCorrelation() { }

        /// <summary>
        /// Validates the correlation according to business rules.
        /// </summary>
        /// <returns>True if the correlation is valid, false otherwise.</returns>
        public bool IsValid()
        {
            // CorrelationData must not be null or empty
            if (CorrelationData == null || CorrelationData.Length == 0)
                return false;

            // RequestMessageId must not be null or empty
            if (string.IsNullOrEmpty(RequestMessageId))
                return false;

            // ResponseTopic must be a valid MQTT topic name
            if (string.IsNullOrEmpty(ResponseTopic) || ResponseTopic.Contains('#') || ResponseTopic.Contains('+'))
                return false;

            // ExpiresAt must be after RequestTimestamp
            if (ExpiresAt <= RequestTimestamp)
                return false;

            // Status transitions must be valid
            if (!IsValidStatusTransition(CorrelationStatus.Pending, Status))
                return false;

            return true;
        }

        /// <summary>
        /// Links a response message to this correlation.
        /// </summary>
        /// <param name="responseMessageId">The response message ID to link.</param>
        /// <returns>True if the response was linked successfully, false if already linked.</returns>
        public bool LinkResponse(string responseMessageId)
        {
            if (string.IsNullOrEmpty(responseMessageId))
                throw new ArgumentException("Response message ID cannot be null or empty", nameof(responseMessageId));

            // Don't add duplicates
            if (ResponseMessageIds.Contains(responseMessageId))
                return false;

            var now = DateTime.UtcNow;

            // Add the response message ID
            ResponseMessageIds.Add(responseMessageId);

            // Update timestamps
            if (FirstResponseTimestamp == null)
                FirstResponseTimestamp = now;

            LastResponseTimestamp = now;

            // Update status to Responded if this is the first response
            if (Status == CorrelationStatus.Pending)
            {
                Status = CorrelationStatus.Responded;

                // Extend TTL for responded correlations to allow late responses
                var extendedTtl = TimeSpan.FromHours(2);
                if (ExpiresAt < now.Add(extendedTtl))
                    ExpiresAt = now.Add(extendedTtl);
            }

            return true;
        }

        /// <summary>
        /// Checks if this correlation has expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        /// <summary>
        /// Checks if this correlation has received any responses.
        /// </summary>
        public bool HasResponses => ResponseMessageIds.Count > 0;

        /// <summary>
        /// Gets the average response time for this correlation.
        /// Returns TimeSpan.Zero if no responses have been received.
        /// </summary>
        public TimeSpan AverageResponseTime
        {
            get
            {
                if (FirstResponseTimestamp == null)
                    return TimeSpan.Zero;

                return FirstResponseTimestamp.Value - RequestTimestamp;
            }
        }

        /// <summary>
        /// Marks this correlation as expired and updates the status.
        /// </summary>
        public void MarkExpired()
        {
            Status = CorrelationStatus.Expired;
            ExpiresAt = DateTime.UtcNow; // Mark for immediate cleanup
        }

        /// <summary>
        /// Gets the estimated memory usage of this correlation.
        /// </summary>
        public long EstimatedMemoryUsage
        {
            get
            {
                long usage = 0;
                usage += CorrelationData?.Length ?? 0;
                usage += System.Text.Encoding.UTF8.GetByteCount(RequestMessageId);
                usage += System.Text.Encoding.UTF8.GetByteCount(ResponseTopic);
                usage += 8 * 4; // Four DateTime fields (8 bytes each)

                // Estimate response message IDs
                foreach (var responseId in ResponseMessageIds)
                {
                    usage += System.Text.Encoding.UTF8.GetByteCount(responseId);
                }

                // Add object overhead
                usage += 128; // Estimated object overhead

                return usage;
            }
        }

        /// <summary>
        /// Gets a string representation of the correlation data for logging.
        /// </summary>
        public string CorrelationDataString => Convert.ToBase64String(CorrelationData);

        /// <summary>
        /// Creates a copy of this correlation with updated properties.
        /// </summary>
        public MessageCorrelation WithUpdates(
            CorrelationStatus? status = null,
            DateTime? expiresAt = null,
            List<string>? responseMessageIds = null)
        {
            return new MessageCorrelation
            {
                CorrelationData = CorrelationData,
                RequestMessageId = RequestMessageId,
                ResponseTopic = ResponseTopic,
                RequestTimestamp = RequestTimestamp,
                ResponseMessageIds = responseMessageIds ?? new List<string>(ResponseMessageIds),
                Status = status ?? Status,
                ExpiresAt = expiresAt ?? ExpiresAt,
                FirstResponseTimestamp = FirstResponseTimestamp,
                LastResponseTimestamp = LastResponseTimestamp
            };
        }

        /// <summary>
        /// Validates if a status transition is allowed.
        /// </summary>
        private static bool IsValidStatusTransition(CorrelationStatus from, CorrelationStatus to)
        {
            return to switch
            {
                CorrelationStatus.Pending => from == CorrelationStatus.Pending,
                CorrelationStatus.Responded => from == CorrelationStatus.Pending || from == CorrelationStatus.Responded,
                CorrelationStatus.Expired => true, // Can expire from any state
                _ => false
            };
        }

        /// <summary>
        /// Compares two correlations for equality based on correlation data.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is MessageCorrelation other)
            {
                return RequestMessage.CorrelationDataEquals(CorrelationData, other.CorrelationData);
            }
            return false;
        }

        /// <summary>
        /// Gets hash code based on correlation data.
        /// </summary>
        public override int GetHashCode()
        {
            return RequestMessage.GetCorrelationDataHashCode(CorrelationData);
        }

        public override string ToString()
        {
            var correlationSnippet = CorrelationDataString.Length > 8
                ? CorrelationDataString[..8] + "..."
                : CorrelationDataString;

            return $"Correlation[{correlationSnippet}] {RequestMessageId} → {ResponseTopic} ({Status}, {ResponseMessageIds.Count} responses)";
        }
    }

    /// <summary>
    /// Enumeration defining the correlation lifecycle states.
    /// </summary>
    public enum CorrelationStatus
    {
        /// <summary>Request sent, awaiting response.</summary>
        Pending,

        /// <summary>Response received and linked.</summary>
        Responded,

        /// <summary>TTL reached, marked for cleanup.</summary>
        Expired
    }
}