using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.BusinessLogic.Models
{
    /// <summary>
    /// Represents an MQTT message that initiates a request-response pattern.
    /// Contains response-topic metadata and correlation-data for linking with response messages.
    /// </summary>
    public class RequestMessage
    {
        /// <summary>
        /// The topic where the request was published.
        /// </summary>
        public string TopicName { get; init; } = string.Empty;

        /// <summary>
        /// Message content as byte array.
        /// </summary>
        public byte[] Payload { get; init; } = Array.Empty<byte>();

        /// <summary>
        /// Target topic for response (from MQTT V5 response-topic property).
        /// Null if this message is not part of a request-response pattern.
        /// </summary>
        public string? ResponseTopic { get; init; }

        /// <summary>
        /// Unique identifier linking request to response (from MQTT V5 correlation-data property).
        /// Required when ResponseTopic is specified.
        /// </summary>
        public byte[]? CorrelationData { get; init; }

        /// <summary>
        /// When the request message was received.
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
        /// Validates the request message according to business rules.
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

            // If ResponseTopic is specified, CorrelationData must be present
            if (!string.IsNullOrEmpty(ResponseTopic) && (CorrelationData == null || CorrelationData.Length == 0))
                return false;

            return true;
        }

        /// <summary>
        /// Determines if this message participates in a request-response pattern.
        /// </summary>
        public bool IsRequestResponseMessage => !string.IsNullOrEmpty(ResponseTopic) && CorrelationData != null && CorrelationData.Length > 0;

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
        /// Creates a copy of this request message with updated properties.
        /// </summary>
        public RequestMessage WithUpdates(
            string? topicName = null,
            byte[]? payload = null,
            string? responseTopic = null,
            byte[]? correlationData = null,
            DateTime? timestamp = null,
            string? messageId = null,
            Dictionary<string, string>? userProperties = null)
        {
            return new RequestMessage
            {
                TopicName = topicName ?? TopicName,
                Payload = payload ?? Payload,
                ResponseTopic = responseTopic ?? ResponseTopic,
                CorrelationData = correlationData ?? CorrelationData,
                Timestamp = timestamp ?? Timestamp,
                MessageId = messageId ?? MessageId,
                UserProperties = userProperties ?? UserProperties
            };
        }

        /// <summary>
        /// Checks if two correlation data arrays are equal (for correlation matching).
        /// </summary>
        public static bool CorrelationDataEquals(byte[]? left, byte[]? right)
        {
            if (left == null && right == null)
                return true;

            if (left == null || right == null)
                return false;

            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a hash code for correlation data (for dictionary keys).
        /// </summary>
        public static int GetCorrelationDataHashCode(byte[]? correlationData)
        {
            if (correlationData == null || correlationData.Length == 0)
                return 0;

            unchecked
            {
                int hash = 17;
                foreach (byte b in correlationData)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }

        public override string ToString()
        {
            var response = IsRequestResponseMessage ? $" → {ResponseTopic}" : "";
            return $"Request[{MessageId}] {TopicName}{response} at {Timestamp:HH:mm:ss.fff}";
        }
    }
}