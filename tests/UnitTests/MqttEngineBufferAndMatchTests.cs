using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;

using Xunit;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Tests for MqttEngine internal buffer management methods, MatchTopic isolation,
/// GetMessagesForTopic with injected data, and GetMaxBufferSizeForTopic edge cases.
/// </summary>
public class MqttEngineBufferAndMatchTests
{
    private static MqttEngine CreateEngine(MqttConnectionSettings? settings = null)
    {
        return new MqttEngine(settings ?? new MqttConnectionSettings
        {
            Hostname = "localhost",
            Port = 1883,
            ClientId = "test-client"
        });
    }

    private static MqttEngine CreateEngineWithLimits(List<TopicBufferLimit> limits)
    {
        return new MqttEngine(new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = limits
        });
    }

    #region GetCurrentBufferedSize Tests

    [Fact]
    public void GetCurrentBufferedSize_NonExistentTopic_ReturnsZero()
    {
        using var engine = CreateEngine();
        Assert.Equal(0, engine.GetCurrentBufferedSize("no/such/topic"));
    }

    [Fact]
    public void GetCurrentBufferedSize_AfterInjection_ReturnsNonZero()
    {
        using var engine = CreateEngine();
        var payload = new byte[256];
        engine.InjectTestMessage("sensor/temp", payload);

        var size = engine.GetCurrentBufferedSize("sensor/temp");
        Assert.True(size > 0, $"Expected positive buffered size, got {size}");
    }

    [Fact]
    public void GetCurrentBufferedSize_GrowsWithMultipleMessages()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("data/stream", new byte[100]);
        var sizeAfterOne = engine.GetCurrentBufferedSize("data/stream");

        engine.InjectTestMessage("data/stream", new byte[200]);
        var sizeAfterTwo = engine.GetCurrentBufferedSize("data/stream");

        Assert.True(sizeAfterTwo > sizeAfterOne,
            $"Size should grow: after 1 msg = {sizeAfterOne}, after 2 msgs = {sizeAfterTwo}");
    }

    #endregion

    #region GetBufferedMessageCount Tests

    [Fact]
    public void GetBufferedMessageCount_NonExistentTopic_ReturnsZero()
    {
        using var engine = CreateEngine();
        Assert.Equal(0, engine.GetBufferedMessageCount("missing/topic"));
    }

    [Fact]
    public void GetBufferedMessageCount_CountsInjectedMessages()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("count/topic", new byte[50]);
        engine.InjectTestMessage("count/topic", new byte[50]);
        engine.InjectTestMessage("count/topic", new byte[50]);

        Assert.Equal(3, engine.GetBufferedMessageCount("count/topic"));
    }

    [Fact]
    public void GetBufferedMessageCount_IsolatedPerTopic()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("topic/a", new byte[10]);
        engine.InjectTestMessage("topic/a", new byte[10]);
        engine.InjectTestMessage("topic/b", new byte[10]);

        Assert.Equal(2, engine.GetBufferedMessageCount("topic/a"));
        Assert.Equal(1, engine.GetBufferedMessageCount("topic/b"));
    }

    #endregion

    #region GetActualBufferMaxSize Tests

    [Fact]
    public void GetActualBufferMaxSize_NonExistentTopic_ReturnsNegativeOne()
    {
        using var engine = CreateEngine();
        Assert.Equal(-1, engine.GetActualBufferMaxSize("no/buffer"));
    }

    [Fact]
    public void GetActualBufferMaxSize_ReturnsDefaultAfterInjection()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("default/topic", new byte[10]);

        var maxSize = engine.GetActualBufferMaxSize("default/topic");
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, maxSize);
    }

    [Fact]
    public void GetActualBufferMaxSize_RespectsCustomLimit()
    {
        var limits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("custom/topic", 5000),
            new TopicBufferLimit("#", 1000)
        };
        using var engine = CreateEngineWithLimits(limits);
        engine.InjectTestMessage("custom/topic", new byte[10]);

        Assert.Equal(5000, engine.GetActualBufferMaxSize("custom/topic"));
    }

    [Fact]
    public void GetActualBufferMaxSize_FallbackLimitForUnmatchedTopic()
    {
        var limits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("specific/only", 9999),
            new TopicBufferLimit("#", 3000)
        };
        using var engine = CreateEngineWithLimits(limits);
        engine.InjectTestMessage("other/topic", new byte[10]);

        Assert.Equal(3000, engine.GetActualBufferMaxSize("other/topic"));
    }

    #endregion

    #region InjectTestMessage Validation

    [Fact]
    public void InjectTestMessage_NullPayload_ReturnsFalse()
    {
        using var engine = CreateEngine();
        Assert.False(engine.InjectTestMessage("valid/topic", null!));
    }

    [Fact]
    public void InjectTestMessage_EmptyTopic_ReturnsFalse()
    {
        using var engine = CreateEngine();
        Assert.False(engine.InjectTestMessage("", new byte[10]));
    }

    [Fact]
    public void InjectTestMessage_WhitespaceTopic_ReturnsFalse()
    {
        using var engine = CreateEngine();
        Assert.False(engine.InjectTestMessage("   ", new byte[10]));
    }

    [Fact]
    public void InjectTestMessage_ValidInput_ReturnsTrue()
    {
        using var engine = CreateEngine();
        Assert.True(engine.InjectTestMessage("ok/topic", new byte[10]));
    }

    #endregion

    #region MatchTopic Isolation Tests

    [Theory]
    [InlineData("sensor/temp", "sensor/temp", 1000)]        // Exact match
    [InlineData("sensor/room1/data", "sensor/+/data", 25)]  // Single-level wildcard
    [InlineData("sensor/room1/data", "sensor/#", 11)]        // Multi-level wildcard
    [InlineData("a", "b", -1)]                                // No match single segment
    [InlineData("a/b/c", "x/y/z", -1)]                      // No match multi segment
    [InlineData("", "sensor/#", -1)]                          // Empty topic
    [InlineData("sensor/temp", "", -1)]                       // Empty filter
    [InlineData("", "", -1)]                                  // Both empty
    [InlineData("root", "root", 1000)]                        // Single segment exact
    [InlineData("root", "+", 5)]                              // Single segment with +
    [InlineData("root", "#", 1)]                              // Single segment with #
    public void MatchTopic_ReturnsExpectedScore(string topic, string filter, int expected)
    {
        Assert.Equal(expected, MqttEngine.MatchTopic(topic, filter));
    }

    [Fact]
    public void MatchTopic_MultiLevelWildcard_MatchesMultipleSegments()
    {
        // sensor/# should match sensor/a/b/c/d
        var score = MqttEngine.MatchTopic("sensor/a/b/c/d", "sensor/#");
        Assert.True(score > 0, $"sensor/# should match sensor/a/b/c/d, got {score}");
    }

    [Fact]
    public void MatchTopic_MultiLevelWildcard_MatchesZeroLevels()
    {
        // sensor/# should match sensor (zero additional levels)
        var score = MqttEngine.MatchTopic("sensor", "sensor/#");
        Assert.True(score > 0, $"sensor/# should match sensor (zero levels), got {score}");
    }

    [Fact]
    public void MatchTopic_SingleLevelWildcard_DoesNotMatchMultipleLevels()
    {
        // sensor/+ should NOT match sensor/a/b
        Assert.Equal(-1, MqttEngine.MatchTopic("sensor/a/b", "sensor/+"));
    }

    [Fact]
    public void MatchTopic_HashNotLast_ReturnsNoMatch()
    {
        // # in middle of filter is invalid
        Assert.Equal(-1, MqttEngine.MatchTopic("a/b/c", "a/#/c"));
    }

    [Fact]
    public void MatchTopic_ExactMatchHasHighestScore()
    {
        var exactScore = MqttEngine.MatchTopic("a/b/c", "a/b/c");
        var plusScore = MqttEngine.MatchTopic("a/b/c", "a/+/c");
        var hashScore = MqttEngine.MatchTopic("a/b/c", "a/#");

        Assert.True(exactScore > plusScore, "Exact match should score higher than + wildcard");
        Assert.True(plusScore > hashScore, "Plus wildcard should score higher than # wildcard");
    }

    [Fact]
    public void MatchTopic_TopicLongerThanFilter_NoMatch()
    {
        Assert.Equal(-1, MqttEngine.MatchTopic("a/b/c", "a/b"));
    }

    [Fact]
    public void MatchTopic_FilterLongerThanTopic_NoMatch()
    {
        Assert.Equal(-1, MqttEngine.MatchTopic("a/b", "a/b/c"));
    }

    #endregion

    #region GetMessagesForTopic Tests

    [Fact]
    public void GetMessagesForTopic_NonExistent_ReturnsNull()
    {
        using var engine = CreateEngine();
        Assert.Null(engine.GetMessagesForTopic("no/topic"));
    }

    [Fact]
    public void GetMessagesForTopic_WithInjectedMessages_ReturnsList()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("test/messages", new byte[] { 1, 2, 3 });
        engine.InjectTestMessage("test/messages", new byte[] { 4, 5, 6 });

        var messages = engine.GetMessagesForTopic("test/messages");
        Assert.NotNull(messages);
        var list = messages.ToList();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void GetMessagesForTopic_MessagesHaveCorrectTopic()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("check/topic", new byte[] { 10, 20 });

        var messages = engine.GetMessagesForTopic("check/topic");
        Assert.NotNull(messages);
        foreach (var msg in messages)
        {
            Assert.Equal("check/topic", msg.Message.Topic);
        }
    }

    [Fact]
    public void GetMessagesForTopic_MessagesHaveUniqueIds()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("id/topic", new byte[5]);
        engine.InjectTestMessage("id/topic", new byte[5]);
        engine.InjectTestMessage("id/topic", new byte[5]);

        var messages = engine.GetMessagesForTopic("id/topic")!.ToList();
        var ids = messages.Select(m => m.MessageId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void GetMessagesForTopic_PayloadPreserved()
    {
        using var engine = CreateEngine();
        var payload = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        engine.InjectTestMessage("payload/topic", payload);

        var messages = engine.GetMessagesForTopic("payload/topic")!.ToList();
        Assert.Single(messages);
        // Verify payload length matches
        Assert.False(messages[0].Message.Payload.IsEmpty);
        Assert.Equal(payload.Length, (int)messages[0].Message.Payload.Length);
    }

    #endregion

    #region GetMaxBufferSizeForTopic Tests

    [Fact]
    public void GetMaxBufferSizeForTopic_DefaultWhenNoSpecificRules()
    {
        using var engine = CreateEngine();
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("any/topic"));
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_SpecificOverrideWins()
    {
        var limits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("device/sensor", 42000),
            new TopicBufferLimit("#", 1000)
        };
        using var engine = CreateEngineWithLimits(limits);

        Assert.Equal(42000, engine.GetMaxBufferSizeForTopic("device/sensor"));
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_WildcardPlusFallback()
    {
        var limits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("home/+/status", 7777),
            new TopicBufferLimit("#", 500)
        };
        using var engine = CreateEngineWithLimits(limits);

        Assert.Equal(7777, engine.GetMaxBufferSizeForTopic("home/kitchen/status"));
        Assert.Equal(500, engine.GetMaxBufferSizeForTopic("office/room"));
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_HashWildcardFallback()
    {
        var limits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("sensors/#", 25000),
            new TopicBufferLimit("#", 2000)
        };
        using var engine = CreateEngineWithLimits(limits);

        Assert.Equal(25000, engine.GetMaxBufferSizeForTopic("sensors/temp/room1"));
        Assert.Equal(2000, engine.GetMaxBufferSizeForTopic("actuator/valve"));
    }

    #endregion

    #region Buffer + Query Integration Tests

    [Fact]
    public void BufferMethods_ConsistentAfterMultipleInjections()
    {
        using var engine = CreateEngine();
        var payload = new byte[100];

        for (int i = 0; i < 5; i++)
            engine.InjectTestMessage("consistent/topic", payload);

        Assert.Equal(5, engine.GetBufferedMessageCount("consistent/topic"));
        Assert.True(engine.GetCurrentBufferedSize("consistent/topic") > 0);
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetActualBufferMaxSize("consistent/topic"));
    }

    [Fact]
    public void BufferMethods_MultipleTopicsIndependent()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("topic/alpha", new byte[50]);
        engine.InjectTestMessage("topic/alpha", new byte[50]);
        engine.InjectTestMessage("topic/beta", new byte[200]);

        Assert.Equal(2, engine.GetBufferedMessageCount("topic/alpha"));
        Assert.Equal(1, engine.GetBufferedMessageCount("topic/beta"));
        Assert.Equal(0, engine.GetBufferedMessageCount("topic/gamma"));

        Assert.True(engine.GetCurrentBufferedSize("topic/beta") > engine.GetCurrentBufferedSize("topic/alpha") / 2,
            "Beta with larger payload should have proportionally larger size");
    }

    [Fact]
    public void ClearAllBuffers_ResetsAllBufferQueryMethods()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("clear/test", new byte[100]);
        engine.InjectTestMessage("clear/test2", new byte[100]);

        Assert.Equal(1, engine.GetBufferedMessageCount("clear/test"));
        Assert.Equal(1, engine.GetBufferedMessageCount("clear/test2"));

        engine.ClearAllBuffers();

        Assert.Equal(0, engine.GetBufferedMessageCount("clear/test"));
        Assert.Equal(0, engine.GetBufferedMessageCount("clear/test2"));
        Assert.Equal(0, engine.GetCurrentBufferedSize("clear/test"));
        Assert.Equal(-1, engine.GetActualBufferMaxSize("clear/test"));
        Assert.Null(engine.GetMessagesForTopic("clear/test"));
        Assert.Empty(engine.GetBufferedTopics());
    }

    [Fact]
    public void GetBufferedTopics_ListsAllInjectedTopics()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("list/a", new byte[10]);
        engine.InjectTestMessage("list/b", new byte[10]);
        engine.InjectTestMessage("list/c", new byte[10]);

        var topics = engine.GetBufferedTopics().ToList();
        Assert.Equal(3, topics.Count);
        Assert.Contains("list/a", topics);
        Assert.Contains("list/b", topics);
        Assert.Contains("list/c", topics);
    }

    [Fact]
    public void TryGetMessage_WithInjectedMessage_CanRetrieve()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("retrieve/topic", new byte[] { 42 });

        var messages = engine.GetMessagesForTopic("retrieve/topic")!.ToList();
        Assert.Single(messages);

        var found = engine.TryGetMessage("retrieve/topic", messages[0].MessageId, out var msg);
        Assert.True(found);
        Assert.NotNull(msg);
        Assert.Equal("retrieve/topic", msg.Topic);
    }

    [Fact]
    public void GetBufferedMessagesForTopic_ReturnsNullForMissingTopic()
    {
        using var engine = CreateEngine();
        Assert.Null(engine.GetBufferedMessagesForTopic("nonexistent"));
    }

    [Fact]
    public void GetBufferedMessagesForTopic_ReturnsList()
    {
        using var engine = CreateEngine();
        engine.InjectTestMessage("buffered/topic", new byte[10]);
        engine.InjectTestMessage("buffered/topic", new byte[20]);

        var msgs = engine.GetBufferedMessagesForTopic("buffered/topic");
        Assert.NotNull(msgs);
        Assert.Equal(2, msgs.Count());
    }

    #endregion

    #region Buffer Eviction Tests

    [Fact]
    public void Buffer_EvictsOldMessages_WhenLimitExceeded()
    {
        var limits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("evict/topic", 500),
            new TopicBufferLimit("#", 500)
        };
        using var engine = CreateEngineWithLimits(limits);

        // Inject messages that exceed the 500 byte limit
        for (int i = 0; i < 20; i++)
            engine.InjectTestMessage("evict/topic", new byte[100]);

        var size = engine.GetCurrentBufferedSize("evict/topic");
        Assert.True(size <= 500, $"Buffer size {size} should not exceed limit of 500");
        Assert.True(engine.GetBufferedMessageCount("evict/topic") < 20,
            "Some messages should have been evicted");
    }

    #endregion
}
