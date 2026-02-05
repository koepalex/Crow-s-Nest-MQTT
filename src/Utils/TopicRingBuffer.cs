using System.Text;
using System.Linq;
using MQTTnet;
using MQTTnet.Packets;

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
    private sealed class InternalBufferedMqttMessage
    {
        public Guid MessageId { get; }
        public MqttApplicationMessage Message { get; }
        public long Size { get; }

        public InternalBufferedMqttMessage(MqttApplicationMessage message, Guid messageId)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            MessageId = messageId;
            Size = message.Payload.Length;
        }
    }
    
    /// <summary>
    /// Extended API used by TopicMessageStore to add a message and obtain eviction info.
    /// Returns list of evicted message IDs (oldest first). If the original message
    /// cannot be added due to size, a proxy may be inserted (proxyId set) when possible.
    /// </summary>
    /// <param name="message">Message to add.</param>
    /// <param name="messageId">Unique ID for the message.</param>
    /// <param name="added">True if the original message was added.</param>
    /// <param name="proxyId">If a proxy message was inserted instead of the original, its ID.</param>
    public IReadOnlyList<Guid> AddMessageWithEvictionInfo(MqttApplicationMessage message, Guid messageId, out bool added, out Guid? proxyId)
    {
        ArgumentNullException.ThrowIfNull(message);
        
        var evicted = new List<Guid>();
        added = false;
        proxyId = null;

        lock (_lock)
        {
            if (_messageIndex.ContainsKey(messageId))
            {
                AppLogger.Warning("Attempted to add message with duplicate ID {MessageId} to topic buffer '{Topic}'. Ignoring.", messageId, message.Topic);
                return evicted;
            }

            var bufferedMessage = new InternalBufferedMqttMessage(message, messageId);

            // Evict until space (or empty)
            while (_currentSizeInBytes + bufferedMessage.Size > _maxSizeInBytes && _messages.Count > 0)
            {
                var oldest = _messages.First!.Value;
                evicted.Add(oldest.MessageId);
                RemoveOldestMessage();
            }

            if (_currentSizeInBytes + bufferedMessage.Size <= _maxSizeInBytes)
            {
                var node = _messages.AddLast(bufferedMessage);
                _messageIndex.Add(messageId, node);
                _currentSizeInBytes += bufferedMessage.Size;
                added = true;
            }
            else
            {
                // Could not fit original (oversized)
                AppLogger.Warning("Message for topic '{Topic}' (ID: {MessageId}, Size: {Size} bytes) could not be added; exceeds buffer limit ({Limit} bytes).",
                    message.Topic, messageId, bufferedMessage.Size, _maxSizeInBytes);

                if (_messages.Count == 0)
                {
                    // Nothing else to evict and still too large; give up.
                    return evicted;
                }

                // Build proxy
                var builder = new MqttApplicationMessageBuilder()
                    .WithTopic(message.Topic)
                    .WithPayload("Payload too large for buffer")
                    .WithUserProperty("CrowProxy", Encoding.UTF8.GetBytes("PayloadTooLarge"))
                    .WithUserProperty("OriginalPayloadSize", Encoding.UTF8.GetBytes(bufferedMessage.Size.ToString()))
                    .WithUserProperty("ReceivedTime", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")))
                    .WithUserProperty("Preview", Encoding.UTF8.GetBytes(GetPreview(message)));

                if (message.UserProperties != null)
                {
                    foreach (var prop in message.UserProperties)
                    {
                        builder.WithUserProperty(prop.Name, prop.ValueBuffer);
                    }
                }

                var proxy = builder.Build();
                var proxyIdLocal = Guid.NewGuid();
                var proxyBuffered = new InternalBufferedMqttMessage(proxy, proxyIdLocal);

                while (_currentSizeInBytes + proxyBuffered.Size > _maxSizeInBytes && _messages.Count > 0)
                {
                    var oldest = _messages.First!.Value;
                    evicted.Add(oldest.MessageId);
                    RemoveOldestMessage();
                }

                if (_currentSizeInBytes + proxyBuffered.Size <= _maxSizeInBytes)
                {
                    var node = _messages.AddLast(proxyBuffered);
                    _messageIndex.Add(proxyIdLocal, node);
                    _currentSizeInBytes += proxyBuffered.Size;
                    proxyId = proxyIdLocal;
                }
                else
                {
                    AppLogger.Warning("Proxy message for topic '{Topic}' could not be added; still exceeds limit ({Limit} bytes).",
                        message.Topic, _maxSizeInBytes);
                }
            }
        }

        return evicted;

        static string GetPreview(MqttApplicationMessage msg)
        {
#pragma warning disable CA1031 // Do not catch general exception types - preview generation must handle any encoding/payload errors gracefully
            try
            {
                if (msg.Payload.IsEmpty)
                    return "[No Payload]";
                var bytes = msg.Payload.FirstSpan.ToArray();
                var preview = Encoding.UTF8.GetString(bytes);
                if (preview.Length > 100)
                    preview = string.Concat(preview.AsSpan(0, 100), "...");
                return preview.Replace("\r", " ").Replace("\n", " ");
            }
            catch
            {
                return "[Binary or non-UTF8 Payload]";
            }
#pragma warning restore CA1031
        }
    }

    // -------------------------------------------------

    private readonly LinkedList<InternalBufferedMqttMessage> _messages;
    private readonly Dictionary<Guid, LinkedListNode<InternalBufferedMqttMessage>> _messageIndex; // Added for fast lookup
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
        _messages = new LinkedList<InternalBufferedMqttMessage>();
        _messageIndex = new Dictionary<Guid, LinkedListNode<InternalBufferedMqttMessage>>(); // Initialize index
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
        _ = AddMessageWithEvictionInfo(message, messageId, out _, out _);
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
            return _messages.Select(bm => bm.Message).ToList();
        }
    }

    /// <summary>
    /// Retrieves all buffered messages with their IDs, ordered from oldest to newest.
    /// </summary>
    public IEnumerable<BufferedMqttMessage> GetBufferedMessages()
    {
        lock (_lock)
        {
            return _messages
                .Select(bm => new BufferedMqttMessage(bm.MessageId, bm.Message))
                .ToList();
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
