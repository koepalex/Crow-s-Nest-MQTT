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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(defaultPerTopicLimitBytes);

        _defaultPerTopicLimitBytes = defaultPerTopicLimitBytes;
        _specificTopicLimits = specificTopicLimits ?? new Dictionary<string, long>();
    }

    public (IReadOnlyList<AddedMessage> added, IReadOnlyList<EvictedMessage> evicted) AddBatch(
        IEnumerable<(Guid id, string topic, MqttApplicationMessage message)> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

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

        // Pattern matching using the same logic as MqttEngine
        long bestMatchSize = _defaultPerTopicLimitBytes;
        int bestMatchScore = -1;

        foreach (var kvp in _specificTopicLimits)
        {
            var filter = kvp.Key;
            var size = kvp.Value;
            
            int score = MatchTopic(topic, filter);
            if (score > bestMatchScore)
            {
                bestMatchScore = score;
                bestMatchSize = size;
            }
        }

        return bestMatchSize;
    }

    /// <summary>
    /// Matches a topic against a filter pattern using the same logic as MqttEngine.
    /// </summary>
    private static int MatchTopic(string topic, string filter)
    {
        if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(filter))
        {
            return -1;
        }

        if (filter == topic)
        {
            return 1000; // Exact match score
        }

        var topicSegments = topic.Split('/');
        var filterSegments = filter.Split('/');

        int score = 0;
        int i = 0; // topic segment index
        int j = 0; // filter segment index

        while (i < topicSegments.Length && j < filterSegments.Length)
        {
            if (filterSegments[j] == "#")
            {
                if (j == filterSegments.Length - 1) // '#' must be the last segment in the filter.
                {
                    // The '#' matches the rest of the topic segments.
                    // Score for '#' (1) will be added post-loop if this condition leads to a match.
                    i = topicSegments.Length; // Mark all remaining topic segments as "matched" by '#'.
                    break; // Exit loop; post-loop logic will determine final score.
                }
                else
                {
                    return -1; // '#' is not the last segment, invalid filter for this context.
                }
            }

            if (filterSegments[j] == topicSegments[i])
            {
                score += 10;
            }
            else if (filterSegments[j] == "+")
            {
                score += 5;
            }
            else
            {
                return -1; // Segments do not match.
            }
            i++;
            j++;
        }

        // After loop, check conditions for a valid match.

        // Case 1: All segments in both topic and filter have been processed and matched.
        if (i == topicSegments.Length && j == filterSegments.Length)
        {
            return score;
        }

        // Case 2: Filter ended with '#' (so j is at the '#' segment) and all topic segments were covered.
        // This covers both "topic/sub" vs "topic/#" (where '#' matches "sub")
        // and "topic" vs "topic/#" (where '#' matches zero levels).
        if (j == filterSegments.Length - 1 && filterSegments[j] == "#" && i == topicSegments.Length)
        {
            return score + 1; // Add score for the '#' wildcard itself.
        }
        
        // Case 3: Topic has more segments, but filter ended before '#'. (e.g. "a/b/c" vs "a/b")
        // This is implicitly handled as not a match by falling through if not covered above.

        // Case 4: Filter has more segments, but topic ended. (e.g. "a/b" vs "a/b/c")
        // This is also implicitly handled as not a match.

        return -1; // No match based on the rules.
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
