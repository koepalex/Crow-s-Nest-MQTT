using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.BusinessLogic.Models
{
    /// <summary>
    /// Represents an MQTT message that responds to a previous request.
    /// Contains correlation-data to link back to the original request message.
    /// </summary>
    public class ResponseMessage
    {
        /// <summary>
        /// The response topic (matches request's ResponseTopic).
        /// </summary>
        public string TopicName { get; init; } = string.Empty;

        /// <summary>
        /// Response content as byte array.
        /// </summary>
        public byte[] Payload { get; init; } = Array.Empty<byte>();

        /// <summary>
        /// Links back to original request (from MQTT V5 correlation-data property).
        /// Must match the correlation-data from the corresponding request message.
        /// </summary>
        public byte[]? CorrelationData { get; init; }

        /// <summary>
        /// When the response message was received.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// Unique identifier for this message instance.
        /// </summary>
        public string MessageId { get; init; } = string.Empty;

        /// <summary>
        /// Additional MQTT V5 user properties.
        /// </summary>
        public Dictionary<string, string> UserProperties { get; init; } = new();

        /// <summary>
        /// Validates the response message according to business rules.
        /// </summary>
        /// <returns>True if the message is valid, false otherwise.</returns>
        public bool IsValid()
        {
            // TopicName must not be null or empty
            if (string.IsNullOrEmpty(TopicName))
                return false;

            // MessageId must not be null or empty
            if (string.IsNullOrEmpty(MessageId))
                return false;

            // Timestamp must not be in the future (allow small clock skew)
            if (Timestamp > DateTime.UtcNow.AddMinutes(5))
                return false;

            return true;
        }

        /// <summary>
        /// Determines if this message has correlation data for request-response linking.
        /// </summary>
        public bool HasCorrelationData => CorrelationData != null && CorrelationData.Length > 0;

        /// <summary>
        /// Gets a string representation of the correlation data for logging and debugging.
        /// </summary>
        public string CorrelationDataString => CorrelationData != null ? Convert.ToBase64String(CorrelationData) : string.Empty;

        /// <summary>
        /// Gets the payload as a UTF-8 string for display purposes.
        /// Returns base64 encoding if the payload contains non-UTF-8 data.
        /// </summary>
        public string PayloadAsString
        {
            get
            {
                try
                {
                    return System.Text.Encoding.UTF8.GetString(Payload);
                }
                catch
                {
                    return Convert.ToBase64String(Payload);
                }
            }
        }

        /// <summary>
        /// Checks if this response message correlates with the given request message.
        /// </summary>
        /// <param name="requestMessage">The request message to check correlation against.</param>
        /// <returns>True if the messages are correlated, false otherwise.</returns>
        public bool CorrelatesWith(RequestMessage requestMessage)
        {
            if (requestMessage == null)
                return false;

            if (!HasCorrelationData || !requestMessage.IsRequestResponseMessage)
                return false;

            return RequestMessage.CorrelationDataEquals(CorrelationData, requestMessage.CorrelationData);
        }

        /// <summary>
        /// Checks if this response message correlates with the given correlation data.
        /// </summary>
        /// <param name="correlationData">The correlation data to check against.</param>
        /// <returns>True if the correlation data matches, false otherwise.</returns>
        public bool CorrelatesWith(byte[]? correlationData)
        {
            if (!HasCorrelationData)
                return false;

            return RequestMessage.CorrelationDataEquals(CorrelationData, correlationData);
        }

        /// <summary>
        /// Validates that this response can be linked to a request with the given properties.
        /// </summary>
        /// <param name="requestTimestamp">The timestamp of the original request.</param>
        /// <param name="expectedResponseTopic">The expected response topic from the request.</param>
        /// <returns>True if the response can be linked, false otherwise.</returns>
        public bool CanLinkToRequest(DateTime requestTimestamp, string expectedResponseTopic)
        {
            // Response must be on the expected topic
            if (!string.Equals(TopicName, expectedResponseTopic, StringComparison.Ordinal))
                return false;

            // Response timestamp must be after the request timestamp
            if (Timestamp <= requestTimestamp)
                return false;

            // Must have correlation data to link
            if (!HasCorrelationData)
                return false;

            return true;
        }

        /// <summary>
        /// Creates a copy of this response message with updated properties.
        /// </summary>
        public ResponseMessage WithUpdates(
            string? topicName = null,
            byte[]? payload = null,
            byte[]? correlationData = null,
            DateTime? timestamp = null,
            string? messageId = null,
            Dictionary<string, string>? userProperties = null)
        {
            return new ResponseMessage
            {
                TopicName = topicName ?? TopicName,
                Payload = payload ?? Payload,
                CorrelationData = correlationData ?? CorrelationData,
                Timestamp = timestamp ?? Timestamp,
                MessageId = messageId ?? MessageId,
                UserProperties = userProperties ?? UserProperties
            };
        }

        /// <summary>
        /// Gets the estimated memory footprint of this response message.
        /// Used for memory management and cleanup decisions.
        /// </summary>
        public long EstimatedMemoryUsage
        {
            get
            {
                long usage = 0;
                usage += System.Text.Encoding.UTF8.GetByteCount(TopicName);
                usage += Payload?.Length ?? 0;
                usage += CorrelationData?.Length ?? 0;
                usage += System.Text.Encoding.UTF8.GetByteCount(MessageId);
                usage += 8; // DateTime is 8 bytes

                // Estimate user properties
                foreach (var kvp in UserProperties)
                {
                    usage += System.Text.Encoding.UTF8.GetByteCount(kvp.Key);
                    usage += System.Text.Encoding.UTF8.GetByteCount(kvp.Value);
                }

                // Add object overhead
                usage += 64; // Estimated object overhead

                return usage;
            }
        }

        /// <summary>
        /// Compares two response messages for equality based on MessageId.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is ResponseMessage other)
            {
                return string.Equals(MessageId, other.MessageId, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>
        /// Gets hash code based on MessageId.
        /// </summary>
        public override int GetHashCode()
        {
            return MessageId?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            var correlation = HasCorrelationData ? $" [{CorrelationDataString[..Math.Min(8, CorrelationDataString.Length)]}...]" : "";
            return $"Response[{MessageId}] {TopicName}{correlation} at {Timestamp:HH:mm:ss.fff}";
        }
    }
}