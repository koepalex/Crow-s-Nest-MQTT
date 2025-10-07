using System;

namespace CrowsNestMqtt.BusinessLogic.Models
{
    /// <summary>
    /// Immutable key for correlation dictionary lookups.
    /// Provides efficient hashing and equality comparison for correlation-data.
    /// </summary>
    public readonly struct CorrelationKey : IEquatable<CorrelationKey>
    {
        private readonly byte[] _correlationData;
        private readonly int _hashCode;

        /// <summary>
        /// Creates a new correlation key from correlation data.
        /// </summary>
        /// <param name="correlationData">The correlation data bytes.</param>
        /// <exception cref="ArgumentNullException">Thrown when correlationData is null.</exception>
        /// <exception cref="ArgumentException">Thrown when correlationData is empty.</exception>
        public CorrelationKey(byte[] correlationData)
        {
            if (correlationData == null)
                throw new ArgumentNullException(nameof(correlationData));

            if (correlationData.Length == 0)
                throw new ArgumentException("Correlation data cannot be empty", nameof(correlationData));

            _correlationData = new byte[correlationData.Length];
            Array.Copy(correlationData, _correlationData, correlationData.Length);
            _hashCode = RequestMessage.GetCorrelationDataHashCode(_correlationData);
        }

        /// <summary>
        /// Gets a copy of the correlation data.
        /// </summary>
        public byte[] CorrelationData
        {
            get
            {
                var copy = new byte[_correlationData.Length];
                Array.Copy(_correlationData, copy, _correlationData.Length);
                return copy;
            }
        }

        /// <summary>
        /// Gets the pre-computed hash code for efficient dictionary operations.
        /// </summary>
        public override int GetHashCode() => _hashCode;

        /// <summary>
        /// Compares this correlation key with another for equality.
        /// </summary>
        /// <param name="other">The other correlation key to compare.</param>
        /// <returns>True if the correlation data is identical, false otherwise.</returns>
        public bool Equals(CorrelationKey other)
        {
            return RequestMessage.CorrelationDataEquals(_correlationData, other._correlationData);
        }

        /// <summary>
        /// Compares this correlation key with an object for equality.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if obj is a CorrelationKey with identical correlation data.</returns>
        public override bool Equals(object? obj)
        {
            return obj is CorrelationKey other && Equals(other);
        }

        /// <summary>
        /// Gets a string representation of the correlation key for debugging.
        /// </summary>
        public override string ToString()
        {
            var base64 = Convert.ToBase64String(_correlationData);
            var snippet = base64.Length > 12 ? base64[..12] + "..." : base64;
            return $"CorrelationKey[{snippet}]";
        }

        /// <summary>
        /// Equality operator for correlation keys.
        /// </summary>
        public static bool operator ==(CorrelationKey left, CorrelationKey right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator for correlation keys.
        /// </summary>
        public static bool operator !=(CorrelationKey left, CorrelationKey right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates a correlation key from byte array, with validation.
        /// </summary>
        /// <param name="correlationData">The correlation data to create key from.</param>
        /// <returns>A new correlation key, or null if data is invalid.</returns>
        public static CorrelationKey? TryCreate(byte[]? correlationData)
        {
            if (correlationData == null || correlationData.Length == 0)
                return null;

            try
            {
                return new CorrelationKey(correlationData);
            }
            catch
            {
                return null;
            }
        }
    }
}