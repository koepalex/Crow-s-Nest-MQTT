using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Models;
using MQTTnet.Protocol;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Tests for MqttEngine methods when client is not connected.
/// The engine is constructed with default settings, so _client.IsConnected == false.
/// </summary>
public class MqttEngineNotConnectedTests : IDisposable
{
    private readonly MqttEngine _engine;
    private readonly List<string> _logMessages = new();

    public MqttEngineNotConnectedTests()
    {
        var settings = new MqttConnectionSettings
        {
            Hostname = "localhost",
            Port = 1883
        };
        _engine = new MqttEngine(settings);
        _engine.LogMessage += (_, msg) => _logMessages.Add(msg);
    }

    public void Dispose()
    {
        _engine.Dispose();
    }

    #region PublishAsync(string topic, string payload, ...) - not connected

    [Fact]
    public async Task PublishAsync_StringPayload_WhenNotConnected_LogsCannotPublish()
    {
        await _engine.PublishAsync("test/topic", "hello");

        Assert.Contains(_logMessages, m => m.Contains("Cannot publish"));
    }

    [Fact]
    public async Task PublishAsync_StringPayload_WhenNotConnected_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _engine.PublishAsync("test/topic", "hello"));

        Assert.Null(exception);
    }

    #endregion

    #region PublishAsync(string topic, byte[] payload, ...) - not connected

    [Fact]
    public async Task PublishAsync_BytePayload_WhenNotConnected_LogsCannotPublish()
    {
        await _engine.PublishAsync("test/topic", new byte[] { 0x01, 0x02 });

        Assert.Contains(_logMessages, m => m.Contains("Cannot publish"));
    }

    [Fact]
    public async Task PublishAsync_BytePayload_WhenNotConnected_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _engine.PublishAsync("test/topic", new byte[] { 0x01, 0x02 }));

        Assert.Null(exception);
    }

    #endregion

    #region PublishAsync(MqttPublishRequest, ...) - not connected

    [Fact]
    public async Task PublishAsync_Request_WhenNotConnected_ReturnsFailedResult()
    {
        var request = new MqttPublishRequest
        {
            Topic = "test/topic",
            PayloadText = "hello"
        };

        var result = await _engine.PublishAsync(request);

        Assert.False(result.Success);
        Assert.Equal("test/topic", result.Topic);
        Assert.Contains("not connected", result.ErrorMessage!);
    }

    [Fact]
    public async Task PublishAsync_Request_WhenNotConnected_LogsCannotPublish()
    {
        var request = new MqttPublishRequest
        {
            Topic = "sensor/data",
            Payload = new byte[] { 0xFF }
        };

        await _engine.PublishAsync(request);

        Assert.Contains(_logMessages, m => m.Contains("Cannot publish"));
    }

    #endregion

    #region SubscribeAsync - not connected

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.SubscribeAsync("test/#"));

        Assert.Contains("not connected", ex.Message);
    }

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_LogsCannotSubscribe()
    {
        try
        {
            await _engine.SubscribeAsync("test/topic");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        Assert.Contains(_logMessages, m => m.Contains("Cannot subscribe"));
    }

    #endregion

    #region UnsubscribeAsync - not connected

    [Fact]
    public async Task UnsubscribeAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.UnsubscribeAsync("test/#"));

        Assert.Contains("not connected", ex.Message);
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenNotConnected_LogsCannotUnsubscribe()
    {
        try
        {
            await _engine.UnsubscribeAsync("test/topic");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        Assert.Contains(_logMessages, m => m.Contains("Cannot unsubscribe"));
    }

    #endregion

    #region ClearRetainedMessageAsync - not connected

    [Fact]
    public async Task ClearRetainedMessageAsync_WhenNotConnected_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() =>
            _engine.ClearRetainedMessageAsync("test/topic"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ClearRetainedMessageAsync_WhenNotConnected_LogsMessages()
    {
        await _engine.ClearRetainedMessageAsync("test/topic");

        // ClearRetainedMessageAsync calls PublishAsync (which logs "Cannot publish")
        // then logs "Cleared retained message" itself
        Assert.Contains(_logMessages, m => m.Contains("Cannot publish"));
        Assert.Contains(_logMessages, m => m.Contains("Cleared retained message"));
    }

    #endregion

    #region EnsureDefaultTopicLimit - static method

    [Fact]
    public void EnsureDefaultTopicLimit_NullInput_ReturnsListWithHashDefault()
    {
        var settings = new MqttConnectionSettings
        {
            Hostname = "localhost",
            TopicSpecificBufferLimits = null!
        };
        using var engine = new MqttEngine(settings);

        // The constructor calls EnsureDefaultTopicLimit; verify via GetMaxBufferSizeForTopic
        // which uses _topicSpecificBufferLimits internally.
        // A topic matching '#' should get the default buffer size.
        long size = engine.GetMaxBufferSizeForTopic("any/topic");
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, size);
    }

    [Fact]
    public void EnsureDefaultTopicLimit_EmptyList_AddsHashDefault()
    {
        var settings = new MqttConnectionSettings
        {
            Hostname = "localhost",
            TopicSpecificBufferLimits = new List<TopicBufferLimit>()
        };
        using var engine = new MqttEngine(settings);

        long size = engine.GetMaxBufferSizeForTopic("some/topic");
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, size);
    }

    [Fact]
    public void EnsureDefaultTopicLimit_ListWithHash_DoesNotDuplicate()
    {
        long customSize = 2 * 1024 * 1024;
        var settings = new MqttConnectionSettings
        {
            Hostname = "localhost",
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", customSize)
            }
        };
        using var engine = new MqttEngine(settings);

        long size = engine.GetMaxBufferSizeForTopic("any/topic");
        Assert.Equal(customSize, size);
    }

    [Fact]
    public void EnsureDefaultTopicLimit_ListWithoutHash_AddsHashDefault()
    {
        long specificSize = 512 * 1024;
        var settings = new MqttConnectionSettings
        {
            Hostname = "localhost",
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("sensor/+", specificSize)
            }
        };
        using var engine = new MqttEngine(settings);

        // sensor/temp matches "sensor/+" rule
        long sensorSize = engine.GetMaxBufferSizeForTopic("sensor/temp");
        Assert.Equal(specificSize, sensorSize);

        // other/topic matches only the auto-added '#' rule
        long otherSize = engine.GetMaxBufferSizeForTopic("other/topic");
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, otherSize);
    }

    [Fact]
    public void EnsureDefaultTopicLimit_CustomDefaultSize_UsesCustomValue()
    {
        long customDefault = 500_000;
        var settings = new MqttConnectionSettings
        {
            Hostname = "localhost",
            TopicSpecificBufferLimits = new List<TopicBufferLimit>(),
            DefaultTopicBufferSizeBytes = customDefault
        };
        using var engine = new MqttEngine(settings);

        long size = engine.GetMaxBufferSizeForTopic("any/topic");
        Assert.Equal(customDefault, size);
    }

    #endregion

    #region MatchTopic - internal static method

    [Theory]
    [InlineData("home/sensor/temp", "home/sensor/temp", 1000)]  // Exact match
    [InlineData("home/sensor/temp", "home/sensor/+", 25)]       // Single-level wildcard
    [InlineData("home/sensor/temp", "home/#", 11)]              // Multi-level wildcard
    [InlineData("home/sensor/temp", "#", 1)]                    // Root wildcard
    [InlineData("home/sensor/temp", "+/+/+", 15)]               // All single-level wildcards
    [InlineData("home/sensor/temp", "home/sensor/humidity", -1)] // No match
    [InlineData("home/sensor", "home/sensor/temp", -1)]         // Topic shorter than filter
    [InlineData("home/sensor/temp/value", "home/sensor/+", -1)] // Topic longer, no #
    [InlineData("", "home/#", -1)]                              // Empty topic
    [InlineData("home/sensor", "", -1)]                         // Empty filter
    public void MatchTopic_ReturnsExpectedScore(string topic, string filter, int expectedScore)
    {
        int score = MqttEngine.MatchTopic(topic, filter);
        Assert.Equal(expectedScore, score);
    }

    [Fact]
    public void MatchTopic_HashMustBeLastSegment()
    {
        // '#' in the middle of filter is invalid
        int score = MqttEngine.MatchTopic("a/b/c", "a/#/c");
        Assert.Equal(-1, score);
    }

    [Fact]
    public void MatchTopic_HashMatchesZeroLevels()
    {
        // "topic" should match "topic/#" (zero levels after topic)
        int score = MqttEngine.MatchTopic("topic", "topic/#");
        Assert.True(score > 0);
    }

    [Fact]
    public void MatchTopic_PlusMatchesSingleLevel()
    {
        int score = MqttEngine.MatchTopic("a/b/c", "a/+/c");
        Assert.True(score > 0);

        // Plus does NOT match multiple levels
        int noMatch = MqttEngine.MatchTopic("a/b/c/d", "a/+/d");
        Assert.Equal(-1, noMatch);
    }

    #endregion
}
