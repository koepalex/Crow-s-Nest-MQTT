using MQTTnet;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrowsNestMqtt.Utils;

/// <summary>
/// A ring buffer implementation to store MQTT messages for a specific topic,
/// constrained by a maximum total size in bytes.
/// Oldest messages are discarded when the buffer exceeds its size limit.
/// This implementation is not inherently thread-safe. External locking is required
/// if accessed concurrently.
/// </summary>
public class TopicRingBuffer
{
    // --- Private Nested Class for Buffered Message ---
    private class BufferedMqttMessage
    {
        public Guid MessageId { get; }
        public MqttApplicationMessage Message { get; }
        public long Size { get; } // Cache the size

        public BufferedMqttMessage(MqttApplicationMessage message, Guid messageId)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            MessageId = messageId;
            // Estimate size: Topic length + Payload length + some overhead
            // This is a rough estimate; adjust if more accuracy is needed.
            Size = (message.Topic?.Length ?? 0) * sizeof(char) + message.Payload.Length + 100; // Payload is ReadOnlySequence<byte> (struct), cannot be null
        }
    }
    // -------------------------------------------------

    private readonly LinkedList<BufferedMqttMessage> _messages;
    private readonly Dictionary<Guid, LinkedListNode<BufferedMqttMessage>> _messageIndex; // Added for fast lookup
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
        _messageIndex = new Dictionary<Guid, LinkedListNode<BufferedMqttMessage>>(); // Initialize index
        _currentSizeInBytes = 0;
    }

    /// <summary>
    /// Adds a new MQTT message with its unique ID to the buffer. If adding the message exceeds the
    /// maximum size, the oldest messages are removed until there is enough space.
    /// </summary>
    /// <param name="message">The MQTT message to add.</param>
    /// <param name="messageId">The unique identifier for this message.</param>
    public void AddMessage(MqttApplicationMessage message, Guid messageId) // Added messageId parameter
    {
        // Prevent adding duplicates if somehow the same ID is generated (highly unlikely)
        if (_messageIndex.ContainsKey(messageId))
        {
             Log.Warning("Attempted to add message with duplicate ID {MessageId} to topic buffer '{Topic}'. Ignoring.", messageId, message.Topic);
             return;
        }

        var bufferedMessage = new BufferedMqttMessage(message, messageId);

        if (bufferedMessage.Size > _maxSizeInBytes)
        {
            Log.Warning("Message for topic '{Topic}' (ID: {MessageId}, Size: {Size} bytes) exceeds buffer limit ({Limit} bytes). Clearing buffer and adding.",
                message.Topic, messageId, bufferedMessage.Size, _maxSizeInBytes);
            Clear(); // Clear buffer if single message is too large
            // Fall through to add the large message after clearing
        }

        // Remove oldest messages until there's space for the new one
        while (_currentSizeInBytes + bufferedMessage.Size > _maxSizeInBytes && _messages.Count > 0)
        {
            RemoveOldestMessage();
        }

        // Add the new message to the list and the index
        var node = _messages.AddLast(bufferedMessage);
        _messageIndex.Add(messageId, node); // Add to index
        _currentSizeInBytes += bufferedMessage.Size;
    }

    /// <summary>
    /// Attempts to retrieve a message by its unique identifier.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to retrieve.</param>
    /// <param name="message">The retrieved message, or null if not found.</param>
    /// <returns>True if the message was found, false otherwise.</returns>
    public bool TryGetMessage(Guid messageId, out MqttApplicationMessage? message)
    {
        if (_messageIndex.TryGetValue(messageId, out var node))
        {
            message = node.Value.Message;
            return true;
        }

        message = null;
        return false;
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
        _messageIndex.Clear(); // Clear the index too
        _currentSizeInBytes = 0;
    }

    private void RemoveOldestMessage()
    {
        if (_messages.First != null)
        {
            var oldestBufferedMessage = _messages.First.Value;
            _currentSizeInBytes -= oldestBufferedMessage.Size;
            _messageIndex.Remove(oldestBufferedMessage.MessageId); // Remove from index
            _messages.RemoveFirst();
        }
    }
}