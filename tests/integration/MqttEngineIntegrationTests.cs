using System.Text;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Integration tests for MqttEngine that require an embedded MQTT broker.
/// Migrated from UnitTests/MqttEngineTests.cs to use the integration test broker fixture.
/// </summary>
public class MqttEngineIntegrationTests : IClassFixture<MqttBrokerFixture>
{
    private readonly MqttBrokerFixture _broker;
    private readonly ITestOutputHelper _output;
    private static readonly TimeSpan TestConnectTimeout = TimeSpan.FromSeconds(30);

    public MqttEngineIntegrationTests(MqttBrokerFixture broker, ITestOutputHelper output)
    {
        _broker = broker;
        _output = output;
    }

    private void LogBrokerState(string testName)
    {
        _output.WriteLine($"[{testName}] Broker: IsRunning={_broker.IsRunning}, Port={_broker.Port}");
        if (_broker.StartupError != null)
            _output.WriteLine($"[{testName}] Broker startup error: {_broker.StartupError}");
    }

    private BusinessLogic.MqttEngine CreateEngine(string testName)
    {
        var settings = new BusinessLogic.MqttConnectionSettings
        {
            Hostname = _broker.Hostname,
            Port = _broker.Port,
            ClientId = $"test-client-{Guid.NewGuid():N}",
            CleanSession = true
        };
        var engine = new BusinessLogic.MqttEngine(settings);
        engine.LogMessage += (_, msg) => _output.WriteLine($"[{testName}] Engine: {msg}");
        engine.ConnectionStateChanged += (_, args) =>
            _output.WriteLine($"[{testName}] ConnectionState: IsConnected={args.IsConnected}, Status={args.ConnectionStatus}, Error={args.ErrorMessage}");
        return engine;
    }

    private async Task<IMqttClient> CreatePublisherAsync(CancellationToken ct)
    {
        var factory = new MqttClientFactory();
        var publisher = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_broker.Hostname, _broker.Port)
            .WithCleanSession(true)
            .Build();
        await publisher.ConnectAsync(options, ct);
        return publisher;
    }

    [Fact]
    public async Task MqttEngine_Should_Receive_Published_Message()
    {
        const string testName = nameof(MqttEngine_Should_Receive_Published_Message);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        BusinessLogic.IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null;
        using var messageReceivedEvent = new ManualResetEventSlim(false);

        engine.MessagesBatchReceived += (_, batch) =>
        {
            foreach (var args in batch)
            {
                if (args.ApplicationMessage.Topic == "test/topic")
                {
                    receivedArgs = args;
                    messageReceivedEvent.Set();
                    break;
                }
            }
        };

        await engine.ConnectAsync(cts.Token);

        using var publisher = await CreatePublisherAsync(cts.Token);
        var payload = "Test Message";
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/topic")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await publisher.PublishAsync(message, cts.Token);

        Assert.True(messageReceivedEvent.Wait(TimeSpan.FromSeconds(10), cts.Token),
            "Timeout waiting for message.");

        Assert.NotNull(receivedArgs);
        Assert.Equal("test/topic", receivedArgs.ApplicationMessage.Topic);
        Assert.Equal(payload, Encoding.UTF8.GetString(receivedArgs.ApplicationMessage.Payload));

        await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        await engine.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task MqttEngine_Should_Handle_Empty_Payload_Message()
    {
        const string testName = nameof(MqttEngine_Should_Handle_Empty_Payload_Message);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        BusinessLogic.IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null;
        using var messageReceivedEvent = new ManualResetEventSlim(false);

        engine.MessagesBatchReceived += (_, batch) =>
        {
            foreach (var args in batch)
            {
                if (args.ApplicationMessage.Topic == "test/empty_payload_topic")
                {
                    receivedArgs = args;
                    messageReceivedEvent.Set();
                    break;
                }
            }
        };

        await engine.ConnectAsync(cts.Token);
        // Wait briefly to ensure subscription is active
        await Task.Delay(500, cts.Token);

        using var publisher = await CreatePublisherAsync(cts.Token);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/empty_payload_topic")
            .WithPayload(Array.Empty<byte>())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await publisher.PublishAsync(message, cts.Token);

        Assert.True(messageReceivedEvent.Wait(TimeSpan.FromSeconds(15), cts.Token),
            "Timeout waiting for message with empty payload.");

        Assert.NotNull(receivedArgs);
        Assert.Equal("test/empty_payload_topic", receivedArgs.ApplicationMessage.Topic);
        Assert.True(receivedArgs.ApplicationMessage.Payload.IsEmpty, "Payload should be empty.");

        await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        await engine.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SubscribeAsync_WhenConnected_ShouldSucceed()
    {
        const string testName = nameof(SubscribeAsync_WhenConnected_ShouldSucceed);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        try
        {
            await engine.ConnectAsync(cts.Token);

            var result = await engine.SubscribeAsync("test/subscribe/topic", MqttQualityOfServiceLevel.AtLeastOnce);

            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Equal("test/subscribe/topic", result.Items.First().TopicFilter.Topic);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenConnected_ShouldSucceed()
    {
        const string testName = nameof(UnsubscribeAsync_WhenConnected_ShouldSucceed);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        try
        {
            await engine.ConnectAsync(cts.Token);
            await engine.SubscribeAsync("test/unsubscribe/topic");

            var result = await engine.UnsubscribeAsync("test/unsubscribe/topic");

            Assert.NotNull(result);
            Assert.Single(result.Items);
            Assert.Equal("test/unsubscribe/topic", result.Items.First().TopicFilter);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenNotConnected_ShouldLogWarningAndReturn()
    {
        const string testName = nameof(PublishAsync_WhenNotConnected_ShouldLogWarningAndReturn);
        LogBrokerState(testName);

        var settings = new BusinessLogic.MqttConnectionSettings
        {
            Hostname = _broker.Hostname,
            Port = _broker.Port
        };
        using var engine = new BusinessLogic.MqttEngine(settings);

        string? logMessage = null;
        engine.LogMessage += (_, message) => logMessage = message;

        await engine.PublishAsync("test/topic", "test payload");

        Assert.Equal("Cannot publish: Client is not connected.", logMessage);
    }

    [Fact]
    public async Task PublishAsync_WhenConnected_ShouldSucceed()
    {
        const string testName = nameof(PublishAsync_WhenConnected_ShouldSucceed);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        string? logMessage = null;
        engine.LogMessage += (_, message) => logMessage = message;

        try
        {
            await engine.ConnectAsync(cts.Token);

            await engine.PublishAsync("test/publish/topic", "test payload",
                retain: true, qos: MqttQualityOfServiceLevel.AtLeastOnce);

            Assert.Contains("Successfully published to 'test/publish/topic'", logMessage);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetMessagesForTopic_AfterReceivingMessage_ShouldReturnMessages()
    {
        const string testName = nameof(GetMessagesForTopic_AfterReceivingMessage_ShouldReturnMessages);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        using var messageReceived = new ManualResetEventSlim(false);
        engine.MessagesBatchReceived += (_, batch) =>
        {
            foreach (var args in batch)
            {
                if (args.ApplicationMessage.Topic == "test/getmessages/topic")
                {
                    messageReceived.Set();
                    break;
                }
            }
        };

        try
        {
            await engine.ConnectAsync(cts.Token);

            using var publisher = await CreatePublisherAsync(cts.Token);
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("test/getmessages/topic")
                .WithPayload("test message content")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await publisher.PublishAsync(message, cts.Token);

            Assert.True(messageReceived.Wait(TimeSpan.FromSeconds(10)),
                "Message was not received within timeout");

            var bufferedMessages = engine.GetMessagesForTopic("test/getmessages/topic");

            Assert.NotNull(bufferedMessages);
            var messagesList = bufferedMessages.ToList();
            Assert.Single(messagesList);
            Assert.Equal("test/getmessages/topic", messagesList[0].Message.Topic);
            Assert.Equal("test message content", Encoding.UTF8.GetString(messagesList[0].Message.Payload));

            await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetBufferedTopics_AfterReceivingMessages_ShouldReturnTopicList()
    {
        const string testName = nameof(GetBufferedTopics_AfterReceivingMessages_ShouldReturnTopicList);
        LogBrokerState(testName);
        Assert.True(_broker.IsRunning, "Embedded MQTT broker is not running.");

        using var cts = new CancellationTokenSource(TestConnectTimeout);
        using var engine = CreateEngine(testName);

        var messagesReceived = 0;
        using var messageReceivedEvent = new ManualResetEventSlim(false);
        engine.MessagesBatchReceived += (_, batch) =>
        {
            foreach (var args in batch)
            {
                if (args.ApplicationMessage.Topic == "test/buffered/topic1" ||
                    args.ApplicationMessage.Topic == "test/buffered/topic2")
                {
                    Interlocked.Increment(ref messagesReceived);
                    if (messagesReceived >= 2)
                        messageReceivedEvent.Set();
                }
            }
        };

        try
        {
            await engine.ConnectAsync(cts.Token);
            await Task.Delay(1000, cts.Token);

            using var publisher = await CreatePublisherAsync(cts.Token);
            var msg1 = new MqttApplicationMessageBuilder()
                .WithTopic("test/buffered/topic1")
                .WithPayload("message 1")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            var msg2 = new MqttApplicationMessageBuilder()
                .WithTopic("test/buffered/topic2")
                .WithPayload("message 2")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisher.PublishAsync(msg1, cts.Token);
            await publisher.PublishAsync(msg2, cts.Token);

            Assert.True(messageReceivedEvent.Wait(TimeSpan.FromSeconds(15)),
                "Messages were not received within timeout");

            var topics = engine.GetBufferedTopics().ToList();

            Assert.NotNull(topics);
            Assert.Contains("test/buffered/topic1", topics);
            Assert.Contains("test/buffered/topic2", topics);
            Assert.True(topics.Count >= 2, $"Expected at least 2 topics, but found {topics.Count}");

            await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }
}
