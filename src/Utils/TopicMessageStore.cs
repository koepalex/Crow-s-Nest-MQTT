using System.Collections.Concurrent;
using MQTTnet;

namespace CrowsNestMqtt.Utils;

/// <summary>
/// Provides per-topic retention using individual TopicRingBuffer instances.
/// Prevents cross-topic eviction: messages for a topic are only evicted by newer
/// messages of the same topic when that topic exceeds its own byte limit.
/// </summary>
public interface ITopicMessageStore
{
    /// <summary>
    /// Adds a batch of messages. Returns collections describing which messages were added
    /// and which (if any) were evicted (per-topic) during the process.
    /// </summary>
    /// <param name="batch">Sequence of (MessageId, Topic, Message)</param>
    /// <returns>Tuple of added messages (with full message) and evicted messages (ids only with topic).</returns>
    (IReadOnlyList<AddedMessage> added, IReadOnlyList<EvictedMessage> evicted) AddBatch(IEnumerable<(Guid id, string topic, MqttApplicationMessage message)> batch);

    /// <summary>
    /// Gets all buffered messages for an exact topic (oldest to newest). Returns empty if topic not present.
    /// </summary>
    IEnumerable<BufferedMqttMessage> GetBufferedMessages(string topic);

    /// <summary>
    /// Attempts to get a specific buffered message by its id (searches all topics).
    /// </summary>
    bool TryGetMessage(Guid messageId, out MqttApplicationMessage? message, out string? topic);
}

/// <summary>
/// Describes an added message result.
/// </summary>
public readonly record struct AddedMessage(Guid MessageId, string Topic, MqttApplicationMessage Message);

/// <summary>
/// Describes an evicted message result.
/// </summary>
public readonly record struct EvictedMessage(Guid MessageId, string Topic);

/// <summary>
/// Concrete implementation of ITopicMessageStore backed by TopicRingBuffer instances.
/// </summary>
public class TopicMessageStore : ITopicMessageStore
{
    private readonly long _defaultPerTopicLimitBytes;
    private readonly IReadOnlyDictionary<string, long> _specificTopicLimits;
    private readonly ConcurrentDictionary<string, TopicRingBuffer> _buffers = new();
    // Reverse index for fast Guid->Topic lookup (eviction/removal coordination)
    private readonly ConcurrentDictionary<Guid, string> _idToTopic = new();

    public TopicMessageStore(
        long defaultPerTopicLimitBytes,
        IReadOnlyDictionary<string, long>? specificTopicLimits = null)
    {
        if (defaultPerTopicLimitBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(defaultPerTopicLimitBytes));

        _defaultPerTopicLimitBytes = defaultPerTopicLimitBytes;
        _specificTopicLimits = specificTopicLimits ?? new Dictionary<string, long>();
    }

    public (IReadOnlyList<AddedMessage> added, IReadOnlyList<EvictedMessage> evicted) AddBatch(
        IEnumerable<(Guid id, string topic, MqttApplicationMessage message)> batch)
    {
        if (batch == null) throw new ArgumentNullException(nameof(batch));

        var added = new List<AddedMessage>();
        var evicted = new List<EvictedMessage>();

        // Group by topic for locality (helps with contention & cache)
        foreach (var group in batch.GroupBy(b => b.topic))
        {
            var topic = NormalizeTopic(group.Key);
            var buffer = _buffers.GetOrAdd(topic, t =>
            {
                var limit = ResolveLimitForTopic(t);
                return new TopicRingBuffer(limit);
            });

            foreach (var (id, _, msg) in group)
            {
                // Use new extended API if available (will be added alongside this store).
                // Fallback to existing AddMessage (no eviction info) if not yet modified.
                if (TryAddWithEvictionInfo(buffer, msg, id,
                        out bool addedFlag,
                        out IReadOnlyList<Guid> evictedIds,
                        out Guid? proxyId))
                {
                    if (addedFlag)
                    {
                        added.Add(new AddedMessage(id, topic, msg));
                        _idToTopic[id] = topic;
                    }

                    // If a proxy was inserted (oversized original replaced), treat proxy as added too (optional).
                    if (proxyId.HasValue)
                    {
                        _idToTopic[proxyId.Value] = topic;
                    }

                    if (evictedIds.Count > 0)
                    {
                        foreach (var evId in evictedIds)
                        {
                            if (_idToTopic.TryRemove(evId, out var evTopic))
                            {
                                evicted.Add(new EvictedMessage(evId, evTopic));
                            }
                            else
                            {
                                // Topic fallback (evicted from this topic anyway)
                                evicted.Add(new EvictedMessage(evId, topic));
                            }
                        }
                    }
                }
                else
                {
                    // Fallback path: original ring buffer without eviction info
                    var preCount = buffer.Count;
                    buffer.AddMessage(msg, id);
                    if (buffer.Count > preCount) // crude heuristic: was added
                    {
                        added.Add(new AddedMessage(id, topic, msg));
                        _idToTopic[id] = topic;
                    }
                    // Evictions not observable in fallback mode
                }
            }
        }

        return (added, evicted);
    }

    public IEnumerable<BufferedMqttMessage> GetBufferedMessages(string topic)
    {
        topic = NormalizeTopic(topic);
        if (_buffers.TryGetValue(topic, out var buffer))
        {
            return buffer.GetBufferedMessages();
        }
        return Enumerable.Empty<BufferedMqttMessage>();
    }

    public bool TryGetMessage(Guid messageId, out MqttApplicationMessage? message, out string? topic)
    {
        topic = null;
        message = null;
        if (_idToTopic.TryGetValue(messageId, out var t)
            && _buffers.TryGetValue(t, out var buffer)
            && buffer.TryGetMessage(messageId, out var msg))
        {
            topic = t;
            message = msg;
            return true;
        }
        return false;
    }

    // --- Helpers ---

    private long ResolveLimitForTopic(string topic)
    {
        // Exact match first
        if (_specificTopicLimits.TryGetValue(topic, out var exact))
            return exact;

        // Optional: pattern match (wildcards) future enhancement.
        return _defaultPerTopicLimitBytes;
    }

    private static string NormalizeTopic(string topic)
        => string.IsNullOrWhiteSpace(topic) ? string.Empty : topic.Trim().TrimEnd('/');

    /// <summary>
    /// Attempts to call the extended TopicRingBuffer API if present.
    /// This allows incremental integration without breaking existing code.
    /// </summary>
    private static bool TryAddWithEvictionInfo(
        TopicRingBuffer buffer,
        MqttApplicationMessage message,
        Guid id,
        out bool added,
        out IReadOnlyList<Guid> evictedIds,
        out Guid? proxyId)
    {
        added = false;
        evictedIds = Array.Empty<Guid>();
        proxyId = null;

        var method = typeof(TopicRingBuffer).GetMethod(
            "AddMessageWithEvictionInfo",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        if (method == null) return false;

        // Prepare argument array matching method signature
        object?[] args = { message, id, null, null };

        var resultObj = method.Invoke(buffer, args);
        if (resultObj is IReadOnlyList<Guid> evicted)
        {
            evictedIds = evicted;
        }

        // Out parameters populated back into args indices 2 & 3
        if (args[2] is bool addedFlag) added = addedFlag;
        if (args[3] is Guid proxyGuid) proxyId = proxyGuid;
        else if (args[3] == null) proxyId = null;

        return true;
    }
}
