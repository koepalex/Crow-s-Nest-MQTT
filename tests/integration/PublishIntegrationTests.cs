using System.Text;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Models;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Integration tests for MqttEngine.PublishAsync using an embedded MQTTnet.Server broker.
/// Verifies publish + receive round-trip, MQTT V5 properties, QoS levels, and retain behaviour.
/// </summary>
public class PublishIntegrationTests : IClassFixture<MqttBrokerFixture>, IDisposable
{
    private readonly MqttBrokerFixture _broker;
    private readonly ITestOutputHelper _output;
    private readonly MqttEngine _engine;
    private static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(10);

    public PublishIntegrationTests(MqttBrokerFixture broker, ITestOutputHelper output)
    {
        _broker = broker;
        _output = output;

        var settings = new MqttConnectionSettings
        {
            Hostname = _broker.Hostname,
            Port = _broker.Port,
            ClientId = $"publish-test-{Guid.NewGuid():N}",
            CleanSession = true
        };

        _engine = new MqttEngine(settings);
        _engine.LogMessage += (_, msg) => _output.WriteLine($"[Engine] {msg}");
    }

    public void Dispose()
    {
        try
        {
            _engine.DisconnectAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // best-effort cleanup
        }
        _engine.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Connects the engine, waits for the auto-subscription to '#' to complete,
    /// then returns. The short delay avoids races between subscribe and publish.
    /// </summary>
    private async Task ConnectAndWaitForSubscriptionAsync()
    {
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(MessageTimeout);
        await _engine.ConnectAsync(cts.Token);

        // The engine subscribes to '#' asynchronously on connect.
        // Give the subscription time to be acknowledged by the broker.
        await Task.Delay(500);
    }

    /// <summary>
    /// Waits for the first message matching <paramref name="topicFilter"/> to arrive
    /// via <see cref="MqttEngine.MessagesBatchReceived"/> and returns it.
    /// </summary>
    private async Task<IdentifiedMqttApplicationMessageReceivedEventArgs> WaitForMessageAsync(
        string topicFilter, TimeSpan? timeout = null)
    {
        timeout ??= MessageTimeout;
        var tcs = new TaskCompletionSource<IdentifiedMqttApplicationMessageReceivedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Handler(object? sender, IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs> batch)
        {
            foreach (var msg in batch)
            {
                if (msg.ApplicationMessage.Topic == topicFilter)
                {
                    tcs.TrySetResult(msg);
                    return;
                }
            }
        }

        _engine.MessagesBatchReceived += Handler;
        try
        {
            using var cts = new CancellationTokenSource(timeout.Value);
            cts.Token.Register(() => tcs.TrySetCanceled());
            return await tcs.Task;
        }
        finally
        {
            _engine.MessagesBatchReceived -= Handler;
        }
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task PublishAndReceive_OwnMessage_IsOwnMessageIsTrue()
    {
        // Arrange
        await ConnectAndWaitForSubscriptionAsync();

        var topic = $"publish-test/own/{Guid.NewGuid():N}";
        var payload = "hello from engine";

        var request = new MqttPublishRequest
        {
            Topic = topic,
            PayloadText = payload
        };

        var receiveTask = WaitForMessageAsync(topic);

        // Act
        var publishResult = await _engine.PublishAsync(request);

        // Assert – publish succeeded
        Assert.True(publishResult.Success, $"Publish failed: {publishResult.ErrorMessage}");
        Assert.Equal(topic, publishResult.Topic);

        // Assert – received back with correct data
        var received = await receiveTask;
        Assert.Equal(topic, received.ApplicationMessage.Topic);
        Assert.Equal(payload, Encoding.UTF8.GetString(received.ApplicationMessage.Payload));
        Assert.True(received.IsOwnMessage, "Message should be flagged as own message.");
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task Publish_WithV5Properties_PropertiesAreReceivedCorrectly()
    {
        // Arrange
        await ConnectAndWaitForSubscriptionAsync();

        var topic = $"publish-test/v5/{Guid.NewGuid():N}";
        var responseTopic = $"publish-test/v5/response/{Guid.NewGuid():N}";
        var correlationData = Encoding.UTF8.GetBytes("corr-test-001");
        var contentType = "application/json";
        var payloadText = "{\"sensor\":\"temp\",\"value\":22.5}";

        var request = new MqttPublishRequest
        {
            Topic = topic,
            PayloadText = payloadText,
            ContentType = contentType,
            ResponseTopic = responseTopic,
            CorrelationData = correlationData,
            PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
            MessageExpiryInterval = 60,
            UserProperties =
            [
                new MqttUserProperty("source", Encoding.UTF8.GetBytes("integration-test")),
                new MqttUserProperty("version", Encoding.UTF8.GetBytes("2"))
            ]
        };

        var receiveTask = WaitForMessageAsync(topic);

        // Act
        var publishResult = await _engine.PublishAsync(request);

        // Assert – publish succeeded
        Assert.True(publishResult.Success, $"Publish failed: {publishResult.ErrorMessage}");

        // Assert – V5 properties round-trip
        var received = await receiveTask;
        var msg = received.ApplicationMessage;

        Assert.Equal(payloadText, Encoding.UTF8.GetString(msg.Payload));
        Assert.Equal(contentType, msg.ContentType);
        Assert.Equal(responseTopic, msg.ResponseTopic);
        Assert.True(correlationData.SequenceEqual(msg.CorrelationData.ToArray()),
            "CorrelationData should match the published value.");

        // UserProperties
        Assert.NotNull(msg.UserProperties);
        Assert.Contains(msg.UserProperties,
            p => p.Name == "source" && Encoding.UTF8.GetString(p.ValueBuffer.ToArray()) == "integration-test");
        Assert.Contains(msg.UserProperties,
            p => p.Name == "version" && Encoding.UTF8.GetString(p.ValueBuffer.ToArray()) == "2");
    }

    [Theory]
    [Trait("Category", "RequiresMqttBroker")]
    [InlineData(MqttQualityOfServiceLevel.AtMostOnce)]
    [InlineData(MqttQualityOfServiceLevel.AtLeastOnce)]
    [InlineData(MqttQualityOfServiceLevel.ExactlyOnce)]
    public async Task Publish_WithQoSLevel_MessageIsDelivered(MqttQualityOfServiceLevel qos)
    {
        // Arrange
        await ConnectAndWaitForSubscriptionAsync();

        var topic = $"publish-test/qos{(int)qos}/{Guid.NewGuid():N}";
        var payload = $"QoS {(int)qos} message";

        var request = new MqttPublishRequest
        {
            Topic = topic,
            PayloadText = payload,
            QoS = qos
        };

        var receiveTask = WaitForMessageAsync(topic);

        // Act
        var publishResult = await _engine.PublishAsync(request);

        // Assert
        Assert.True(publishResult.Success, $"Publish at QoS {(int)qos} failed: {publishResult.ErrorMessage}");

        var received = await receiveTask;
        Assert.Equal(topic, received.ApplicationMessage.Topic);
        Assert.Equal(payload, Encoding.UTF8.GetString(received.ApplicationMessage.Payload));
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task Publish_WithRetain_RetainedMessageIsStored()
    {
        // Arrange
        await ConnectAndWaitForSubscriptionAsync();

        var topic = $"publish-test/retain/{Guid.NewGuid():N}";
        var payload = "retained payload";

        var request = new MqttPublishRequest
        {
            Topic = topic,
            PayloadText = payload,
            Retain = true,
            QoS = MqttQualityOfServiceLevel.AtLeastOnce
        };

        var receiveTask = WaitForMessageAsync(topic);

        // Act – publish a retained message
        var publishResult = await _engine.PublishAsync(request);

        // Assert – publish succeeded and message arrived
        Assert.True(publishResult.Success, $"Retain publish failed: {publishResult.ErrorMessage}");

        var received = await receiveTask;
        Assert.Equal(topic, received.ApplicationMessage.Topic);
        Assert.Equal(payload, Encoding.UTF8.GetString(received.ApplicationMessage.Payload));

        // Verify retain is stored by connecting a fresh engine and seeing if it gets delivered.
        var freshSettings = new MqttConnectionSettings
        {
            Hostname = _broker.Hostname,
            Port = _broker.Port,
            ClientId = $"retain-verify-{Guid.NewGuid():N}",
            CleanSession = true
        };

        using var verifier = new MqttEngine(freshSettings);
        verifier.LogMessage += (_, msg) => _output.WriteLine($"[Verifier] {msg}");

        var retainedTcs = new TaskCompletionSource<IdentifiedMqttApplicationMessageReceivedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        verifier.MessagesBatchReceived += (_, batch) =>
        {
            foreach (var msg in batch)
            {
                if (msg.ApplicationMessage.Topic == topic)
                {
                    retainedTcs.TrySetResult(msg);
                    return;
                }
            }
        };

        using var cts = new CancellationTokenSource(MessageTimeout);
        await verifier.ConnectAsync(cts.Token);
        // Wait for the auto-subscription and retained message delivery
        await Task.Delay(500);

        cts.Token.Register(() => retainedTcs.TrySetCanceled());

        var retainedMsg = await retainedTcs.Task;
        Assert.Equal(topic, retainedMsg.ApplicationMessage.Topic);
        Assert.Equal(payload, Encoding.UTF8.GetString(retainedMsg.ApplicationMessage.Payload));
        Assert.True(retainedMsg.IsEffectivelyRetained, "Message should be marked as retained.");

        // Cleanup – clear the retained message so it doesn't leak into other tests
        await _engine.PublishAsync(new MqttPublishRequest
        {
            Topic = topic,
            Payload = Array.Empty<byte>(),
            Retain = true,
            QoS = MqttQualityOfServiceLevel.AtLeastOnce
        });

        await verifier.DisconnectAsync(CancellationToken.None);
    }
}
