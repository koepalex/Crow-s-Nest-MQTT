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
           // Simplified size calculation for testing purposes - primarily payload length.
           // A more accurate calculation might be needed later.
           Size = message.Payload.Length; // Use payload length directly for now
       }
   }
    // -------------------------------------------------

    private readonly LinkedList<BufferedMqttMessage> _messages;
    private readonly Dictionary<Guid, LinkedListNode<BufferedMqttMessage>> _messageIndex; // Added for fast lookup
    private readonly long _maxSizeInBytes;
    private long _currentSizeInBytes;
    private readonly object _lock = new object(); // Added for thread safety

    public long CurrentSizeInBytes
    {
        get
        {
            lock (_lock)
            {
                return _currentSizeInBytes;
            }
        }
    }
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _messages.Count;
            }
        }
    }
    public long MaxSizeInBytes => _maxSizeInBytes; // Max size is immutable, no lock needed

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
        lock (_lock)
        {
            // Prevent adding duplicates if somehow the same ID is generated (highly unlikely)
            if (_messageIndex.ContainsKey(messageId))
            {
                 Log.Warning("Attempted to add message with duplicate ID {MessageId} to topic buffer '{Topic}'. Ignoring.", messageId, message.Topic);
                 return;
            }

           var bufferedMessage = new BufferedMqttMessage(message, messageId);

           // Remove the explicit check for oversized messages here.
           // The while loop below will handle making space.

           // Remove oldest messages until there's space for the new one
            while (_currentSizeInBytes + bufferedMessage.Size > _maxSizeInBytes && _messages.Count > 0)
            {
                // RemoveOldestMessage is called within the lock
                RemoveOldestMessage();
           }

           // Only add the message if it fits after potentially removing older messages
           if (_currentSizeInBytes + bufferedMessage.Size <= _maxSizeInBytes)
           {
               // Add the new message to the list and the index
               var node = _messages.AddLast(bufferedMessage);
               _messageIndex.Add(messageId, node); // Add to index
               _currentSizeInBytes += bufferedMessage.Size;
           }
           else
           {
                // Log if a message couldn't be added even after clearing space (because it's intrinsically too large)
                Log.Warning("Message for topic '{Topic}' (ID: {MessageId}, Size: {Size} bytes) could not be added as it exceeds the buffer limit ({Limit} bytes) even after clearing space.",
                    message.Topic, messageId, bufferedMessage.Size, _maxSizeInBytes);
                // Ensure buffer is clear if the only message was too large and couldn't be added
                if (_messages.Count == 0)
                {
                    // Clear is called within the lock
                    Clear();
                }
           }
        }
    }

    /// <summary>
    /// Attempts to retrieve a message by its unique identifier.
    /// </summary>
    /// <param name="messageId">The unique identifier of the message to retrieve.</param>
    /// <param name="message">The retrieved message, or null if not found.</param>
    /// <returns>True if the message was found, false otherwise.</returns>
    public bool TryGetMessage(Guid messageId, out MqttApplicationMessage? message)
    {
        lock (_lock)
        {
            if (_messageIndex.TryGetValue(messageId, out var node))
            {
                message = node.Value.Message;
                return true;
            }

            message = null;
            return false;
        }
    }


    /// <summary>
    /// Retrieves all messages currently stored in the buffer, ordered from oldest to newest.
    /// </summary>
    /// <returns>An enumerable collection of the stored MQTT messages.</returns>
    public IEnumerable<MqttApplicationMessage> GetMessages()
    {
        lock (_lock)
        {
            // Return a copy or snapshot to avoid issues with modification while enumerating
            // ToList() creates the copy inside the lock
            return _messages.Select(bm => bm.Message).ToList();
        }
    }

    /// <summary>
    /// Clears all messages from the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _messages.Clear();
            _messageIndex.Clear(); // Clear the index too
            _currentSizeInBytes = 0;
        }
    }

    // This method should only be called from within a lock
    private void RemoveOldestMessage()
    {
        // No lock here, assumes caller holds the lock
        if (_messages.First != null)
        {
            var oldestBufferedMessage = _messages.First.Value;
            _currentSizeInBytes -= oldestBufferedMessage.Size;
            _messageIndex.Remove(oldestBufferedMessage.MessageId); // Remove from index
            _messages.RemoveFirst();
        }
    }
}