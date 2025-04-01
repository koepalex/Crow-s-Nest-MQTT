using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrowsNestMqtt.Utils
{
    /// <summary>
    /// Represents a message stored in the ring buffer, tracking its size.
    /// </summary>
    internal class BufferedMqttMessage
    {
        public MqttApplicationMessage Message { get; }
        public int Size { get; } // Size in bytes (approximated by payload size)

        public BufferedMqttMessage(MqttApplicationMessage message)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            // Approximate size using payload length. A more accurate calculation
            // might include topic length and metadata, but payload is dominant.
            // Use Payload.Length directly as it's a ReadOnlySequence<byte>.
            Size = (int)message.Payload.Length; // Cast to int if needed, ReadOnlySequence.Length is long
        }
    }

    /// <summary>
    /// A ring buffer implementation to store MQTT messages for a specific topic,
    /// constrained by a maximum total size in bytes.
    /// Oldest messages are discarded when the buffer exceeds its size limit.
    /// This implementation is not inherently thread-safe. External locking is required
    /// if accessed concurrently.
    /// </summary>
    public class TopicRingBuffer
    {
        private readonly LinkedList<BufferedMqttMessage> _messages;
        private readonly long _maxSizeInBytes;
        private long _currentSizeInBytes;

        public long CurrentSizeInBytes => _currentSizeInBytes;
        public int Count => _messages.Count;
        public long MaxSizeInBytes => _maxSizeInBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicRingBuffer"/> class.
        /// </summary>
        /// <param name="maxSizeInBytes">The maximum total size of messages (in bytes) the buffer can hold.</param>
        public TopicRingBuffer(long maxSizeInBytes)
        {
            if (maxSizeInBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSizeInBytes), "Maximum size must be positive.");
            }
            _maxSizeInBytes = maxSizeInBytes;
            _messages = new LinkedList<BufferedMqttMessage>();
            _currentSizeInBytes = 0;
        }

        /// <summary>
        /// Adds a new MQTT message to the buffer. If adding the message exceeds the
        /// maximum size, the oldest messages are removed until there is enough space.
        /// </summary>
        /// <param name="message">The MQTT message to add.</param>
        public void AddMessage(MqttApplicationMessage message)
        {
            var bufferedMessage = new BufferedMqttMessage(message);

            // If even this single message is too large, we cannot add it.
            // (Or alternatively, clear the entire buffer first if desired)
            if (bufferedMessage.Size > _maxSizeInBytes)
            {
                // Log or handle this case? For now, we just won't add it.
                // Consider clearing the buffer if one message can exceed the total limit.
                _messages.Clear();
                _currentSizeInBytes = 0;
                // Optionally throw an exception or log a warning.
                Console.WriteLine($"Warning: Message for topic '{message.Topic}' ({bufferedMessage.Size} bytes) exceeds buffer limit ({_maxSizeInBytes} bytes) and cannot be added.");
                return; // Or potentially add anyway after clearing? Design decision.
            }

            // Remove oldest messages until there's space for the new one
            while (_currentSizeInBytes + bufferedMessage.Size > _maxSizeInBytes && _messages.Count > 0)
            {
                RemoveOldestMessage();
            }

            // Add the new message
            _messages.AddLast(bufferedMessage);
            _currentSizeInBytes += bufferedMessage.Size;
        }

        /// <summary>
        /// Retrieves all messages currently stored in the buffer, ordered from oldest to newest.
        /// </summary>
        /// <returns>An enumerable collection of the stored MQTT messages.</returns>
        public IEnumerable<MqttApplicationMessage> GetMessages()
        {
            // Return a copy or snapshot to avoid issues with modification while enumerating
            return _messages.Select(bm => bm.Message).ToList();
        }

        /// <summary>
        /// Clears all messages from the buffer.
        /// </summary>
        public void Clear()
        {
            _messages.Clear();
            _currentSizeInBytes = 0;
        }

        private void RemoveOldestMessage()
        {
            if (_messages.First != null)
            {
                var oldestMessage = _messages.First.Value;
                _currentSizeInBytes -= oldestMessage.Size;
                _messages.RemoveFirst();
            }
        }
    }
}