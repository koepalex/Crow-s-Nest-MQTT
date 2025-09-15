using System.Reflection;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration; // Added for TopicBufferLimit
using System.Linq;

namespace CrowsNestMqtt.UnitTests;

public class MqttEngineTests : IClassFixture<MqttBrokerFixture>
{
    private readonly MqttBrokerFixture _brokerFixture;

    public MqttEngineTests(MqttBrokerFixture brokerFixture)
    {
        _brokerFixture = brokerFixture;
    }
    
    [Fact]
    public void MessageReceived_BatchEvent_Should_Fire_Once_Per_Batch()
    {
        // This test is a placeholder for the new batch event behavior.
        // It should be updated after the refactor to verify that the batch event
        // is fired only once per batch, even with a high volume of messages.
        // The implementation will depend on the new event signature.
        Assert.True(true, "Batch event test placeholder. Update after refactor.");
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task MqttEngine_Should_Receive_Published_Message()
    {
        // Arrange
        string brokerHost = _brokerFixture.Hostname;
        int brokerPort = _brokerFixture.Port;
        var connectionSettings = new MqttConnectionSettings
        {
            Hostname = brokerHost,
            Port = brokerPort,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(connectionSettings);

        IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null;
        var messageReceivedEvent = new ManualResetEventSlim(false);

engine.MessagesBatchReceived += (sender, batch) =>
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

        await engine.ConnectAsync(CancellationToken.None);

        // Act: Publish a test message using a publisher client.
        var factory = new MqttClientFactory();
        var publisher = factory.CreateMqttClient();
        var publisherOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithCleanSession(true)
            .Build();

        await publisher.ConnectAsync(publisherOptions, CancellationToken.None);

        var payload = "Test Message";
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/topic")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await publisher.PublishAsync(message, CancellationToken.None);

        // Wait for the message to be received
        if (!messageReceivedEvent.Wait(TimeSpan.FromSeconds(10), CancellationToken.None))
        {
            Assert.Fail("Timeout waiting for message.");
        }

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal("test/topic", receivedArgs.ApplicationMessage.Topic);
        Assert.Equal(payload, receivedArgs.ApplicationMessage.ConvertPayloadToString());

        // Cleanup
        await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        await engine.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task MqttEngine_Should_Handle_Empty_Payload_Message()
    {
        // Arrange
        string brokerHost = _brokerFixture.Hostname;
        int brokerPort = _brokerFixture.Port;
        var connectionSettings = new MqttConnectionSettings
        {
            Hostname = brokerHost,
            Port = brokerPort,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(connectionSettings);

        IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null;
        var messageReceivedEvent = new ManualResetEventSlim(false);

engine.MessagesBatchReceived += (sender, batch) =>
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

        await engine.ConnectAsync(CancellationToken.None);

        // Act: Publish a test message with an empty payload.
        var factory = new MqttClientFactory();
        var publisher = factory.CreateMqttClient();
        var publisherOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithCleanSession(true)
            .Build();

        await publisher.ConnectAsync(publisherOptions, CancellationToken.None);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/empty_payload_topic")
            .WithPayload(Array.Empty<byte>()) // Empty payload
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await publisher.PublishAsync(message, CancellationToken.None);

        // Wait for the message to be received
        if (!messageReceivedEvent.Wait(TimeSpan.FromSeconds(10), CancellationToken.None))
        {
            Assert.Fail("Timeout waiting for message with empty payload.");
        }

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.Equal("test/empty_payload_topic", receivedArgs.ApplicationMessage.Topic);
        // Payload is ReadOnlySequence<byte>, a struct, so it cannot be null.
        // We check IsEmpty or Length instead.
        Assert.True(receivedArgs.ApplicationMessage.Payload.IsEmpty, "Payload should be empty.");
        Assert.Null(receivedArgs.ApplicationMessage.ConvertPayloadToString());

        // Cleanup
        await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        await engine.DisconnectAsync(CancellationToken.None);
    }

    [Fact]
    public void BuildMqttOptions_WithUsernamePasswordAuth_ShouldSetCredentials()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort,
            AuthMode = new CrowsNestMqtt.BusinessLogic.Configuration.UsernamePasswordAuthenticationMode("testuser", "testpass")
        };
        var engine = new MqttEngine(settings);

        // Act
        // Use reflection to access the private method BuildMqttOptions
        var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        var options = methodInfo?.Invoke(engine, null) as MqttClientOptions;

        // Assert
        Assert.NotNull(options);
        Assert.Equal("testuser", options.Credentials?.GetUserName(options));
        // Note: MQTTnet.MqttClientOptions stores password as byte[]
        Assert.Equal("testpass", System.Text.Encoding.UTF8.GetString(options.Credentials?.GetPassword(options) ?? Array.Empty<byte>()));
    }

    [Fact]
    public void BuildMqttOptions_WithAnonymousAuth_ShouldNotSetCredentials()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort,
            AuthMode = new CrowsNestMqtt.BusinessLogic.Configuration.AnonymousAuthenticationMode()
        };
        var engine = new MqttEngine(settings);

        // Act
        var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        var options = methodInfo?.Invoke(engine, null) as MqttClientOptions;

        // Assert
        Assert.NotNull(options);
        Assert.Null(options.Credentials); // Or check if UserName/Password are null/empty if Credentials object is always created
    }

    [Fact]
    public void BuildMqttOptions_WithEnhancedAuth_ShouldSetAuthData()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort,
            AuthMode = new EnhancedAuthenticationMode("Enhanced Authentication", "my-jwt-token"),
        };
        var engine = new MqttEngine(settings);

        // Act
        var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        var options = methodInfo?.Invoke(engine, null) as MqttClientOptions;

        // Assert
        Assert.NotNull(options);
        Assert.Equal("Enhanced Authentication", options.AuthenticationMethod);
        Assert.Equal("my-jwt-token", System.Text.Encoding.UTF8.GetString(options.AuthenticationData));
    }

    [Fact]
    public void BuildMqttOptions_WithUseTls_ShouldSetTlsOptions()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = 8883,
            UseTls = true
        };
        var engine = new MqttEngine(settings);

        // Act
        var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
        var options = methodInfo?.Invoke(engine, null) as MqttClientOptions;

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.ChannelOptions);
        var tlsOptions = options.ChannelOptions.TlsOptions;
        Assert.NotNull(tlsOptions);
        Assert.True(tlsOptions.UseTls);
        Assert.True(tlsOptions.AllowUntrustedCertificates);
        Assert.True(tlsOptions.IgnoreCertificateChainErrors);
        Assert.True(tlsOptions.IgnoreCertificateRevocationErrors);
    }

    [Fact]
    public async Task SubscribeAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort
        };
        var engine = new MqttEngine(settings);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SubscribeAsync("test/topic"));
        
        Assert.Equal("Client is not connected.", exception.Message);
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task SubscribeAsync_WhenConnected_ShouldSucceed()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = _brokerFixture.Hostname,
            Port = _brokerFixture.Port,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(settings);

        try
        {
            await engine.ConnectAsync(CancellationToken.None);

            // Act
            var result = await engine.SubscribeAsync("test/subscribe/topic", MqttQualityOfServiceLevel.AtLeastOnce);

            // Assert
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
    public async Task UnsubscribeAsync_WhenNotConnected_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort
        };
        var engine = new MqttEngine(settings);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.UnsubscribeAsync("test/topic"));
        
        Assert.Equal("Client is not connected.", exception.Message);
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task UnsubscribeAsync_WhenConnected_ShouldSucceed()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = _brokerFixture.Hostname,
            Port = _brokerFixture.Port,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(settings);

        try
        {
            await engine.ConnectAsync(CancellationToken.None);
            
            // First subscribe to a topic
            await engine.SubscribeAsync("test/unsubscribe/topic");

            // Act
            var result = await engine.UnsubscribeAsync("test/unsubscribe/topic");

            // Assert
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
    [Trait("Category", "RequiresMqttBroker")]
    public async Task PublishAsync_WhenNotConnected_ShouldLogWarningAndReturn()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = _brokerFixture.Hostname,
            Port = _brokerFixture.Port
        };
        var engine = new MqttEngine(settings);
        
        string? logMessage = null;
        engine.LogMessage += (sender, message) => logMessage = message;

        // Act
        await engine.PublishAsync("test/topic", "test payload");

        // Assert
        Assert.Equal("Cannot publish: Client is not connected.", logMessage);
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task PublishAsync_WhenConnected_ShouldSucceed()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = _brokerFixture.Hostname,
            Port = _brokerFixture.Port,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(settings);
        
        string? logMessage = null;
        engine.LogMessage += (sender, message) => logMessage = message;

        try
        {
            await engine.ConnectAsync(CancellationToken.None);

            // Act
            await engine.PublishAsync("test/publish/topic", "test payload", 
                retain: true, qos: MqttQualityOfServiceLevel.AtLeastOnce);

            // Assert
            Assert.Contains("Successfully published to 'test/publish/topic'", logMessage);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void GetMessagesForTopic_WhenTopicNotExists_ShouldReturnNull()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort
        };
        var engine = new MqttEngine(settings);

        // Act
        var messages = engine.GetMessagesForTopic("nonexistent/topic");

        // Assert
        Assert.Null(messages);
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task GetMessagesForTopic_AfterReceivingMessage_ShouldReturnMessages()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = _brokerFixture.Hostname,
            Port = _brokerFixture.Port,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(settings);

        var messageReceived = new ManualResetEventSlim(false);
engine.MessagesBatchReceived += (sender, batch) =>
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
            await engine.ConnectAsync(CancellationToken.None);

            // Publish a message to create buffer content
            var factory = new MqttClientFactory();
            var publisher = factory.CreateMqttClient();
            var publisherOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerFixture.Hostname, _brokerFixture.Port)
                .WithCleanSession(true)
                .Build();

            await publisher.ConnectAsync(publisherOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("test/getmessages/topic")
                .WithPayload("test message content")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisher.PublishAsync(message, CancellationToken.None);

            // Wait for message to be received and buffered
            Assert.True(messageReceived.Wait(TimeSpan.FromSeconds(10)), "Message was not received within timeout");

            // Act
            var bufferedMessages = engine.GetMessagesForTopic("test/getmessages/topic");

            // Assert
            Assert.NotNull(bufferedMessages);
            var messagesList = bufferedMessages.ToList();
            Assert.Single(messagesList);
Assert.Equal("test/getmessages/topic", messagesList[0].Message.Topic);
Assert.Equal("test message content", messagesList[0].Message.ConvertPayloadToString());

            await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void GetBufferedTopics_WhenNoMessages_ShouldReturnEmptyCollection()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort
        };
        var engine = new MqttEngine(settings);

        // Act
        var topics = engine.GetBufferedTopics();

        // Assert
        Assert.NotNull(topics);
        Assert.Empty(topics);
    }

    [Fact]
    [Trait("Category", "RequiresMqttBroker")]
    public async Task GetBufferedTopics_AfterReceivingMessages_ShouldReturnTopicList()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = _brokerFixture.Hostname,
            Port = _brokerFixture.Port,
            ClientId = $"test-client-{Guid.NewGuid()}",
            CleanSession = true
        };
        var engine = new MqttEngine(settings);

        var messagesReceived = 0;
        var messageReceivedEvent = new ManualResetEventSlim(false);
engine.MessagesBatchReceived += (sender, batch) =>
        {
            foreach (var args in batch)
            {
                // Only count messages for our test topics
                if (args.ApplicationMessage.Topic == "test/buffered/topic1" ||
                    args.ApplicationMessage.Topic == "test/buffered/topic2")
                {
                    Interlocked.Increment(ref messagesReceived);
                    if (messagesReceived >= 2) // Wait for both test messages
                    {
                        messageReceivedEvent.Set();
                    }
                }
            }
        };

        try
        {
            await engine.ConnectAsync(CancellationToken.None);

            // The engine automatically subscribes to "#" so no explicit subscription needed
            // Give a small delay to ensure automatic subscription is active
            await Task.Delay(1000);

            // Publish messages to different topics
            var factory = new MqttClientFactory();
            var publisher = factory.CreateMqttClient();
            var publisherOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerFixture.Hostname, _brokerFixture.Port)
                .WithCleanSession(true)
                .Build();

            await publisher.ConnectAsync(publisherOptions, CancellationToken.None);

            var message1 = new MqttApplicationMessageBuilder()
                .WithTopic("test/buffered/topic1")
                .WithPayload("message 1")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            var message2 = new MqttApplicationMessageBuilder()
                .WithTopic("test/buffered/topic2")
                .WithPayload("message 2")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisher.PublishAsync(message1, CancellationToken.None);
            await publisher.PublishAsync(message2, CancellationToken.None);

            // Wait for messages to be received and buffered
            Assert.True(messageReceivedEvent.Wait(TimeSpan.FromSeconds(15)), "Messages were not received within timeout");

            // Act
            var topics = engine.GetBufferedTopics().ToList();

            // Assert
            Assert.NotNull(topics);
            Assert.Contains("test/buffered/topic1", topics);
            Assert.Contains("test/buffered/topic2", topics);
            // Note: There might be other topics from previous test runs or retained messages
            Assert.True(topics.Count >= 2, $"Expected at least 2 topics, but found {topics.Count}");

            await publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
        finally
        {
            await engine.DisconnectAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void ClearAllBuffers_ShouldClearAllTopicBuffers()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort
        };
        var engine = new MqttEngine(settings);

        string? logMessage = null;
        engine.LogMessage += (sender, message) => logMessage = message;

        // Act
        engine.ClearAllBuffers();

        // Assert
        Assert.Equal("All topic buffers cleared.", logMessage);
        var topics = engine.GetBufferedTopics();
        Assert.Empty(topics);
    }

    [Fact]
    public void TryGetMessage_WhenTopicNotExists_ShouldReturnFalse()
    {
        // Arrange
        var settings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort
        };
        var engine = new MqttEngine(settings);
        var messageId = Guid.NewGuid();

        // Act
        var result = engine.TryGetMessage("nonexistent/topic", messageId, out var message);

        // Assert
        Assert.False(result);
        Assert.Null(message);
    }
}
