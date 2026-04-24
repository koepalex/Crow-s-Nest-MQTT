using System.Reflection;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using System.Linq;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Pure unit tests for MqttEngine that do NOT require a running MQTT broker.
/// Broker-dependent tests have been moved to Integration.Tests/MqttEngineIntegrationTests.cs.
/// </summary>
public class MqttEngineTests
{
    [Fact]
    public void MessageReceived_BatchEvent_Should_Fire_Once_Per_Batch()
    {
        // This test is a placeholder for the new batch event behavior.
        Assert.True(true, "Batch event test placeholder. Update after refactor.");
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
        using var engine = new MqttEngine(settings);

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
        using var engine = new MqttEngine(settings);

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
        using var engine = new MqttEngine(settings);

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
        using var engine = new MqttEngine(settings);

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
        using var engine = new MqttEngine(settings);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.SubscribeAsync("test/topic"));
        
        Assert.Equal("Client is not connected.", exception.Message);
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
        using var engine = new MqttEngine(settings);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.UnsubscribeAsync("test/topic"));
        
        Assert.Equal("Client is not connected.", exception.Message);
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
        using var engine = new MqttEngine(settings);

        // Act
        var messages = engine.GetMessagesForTopic("nonexistent/topic");

        // Assert
        Assert.Null(messages);
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
        using var engine = new MqttEngine(settings);

        // Act
        var topics = engine.GetBufferedTopics();

        // Assert
        Assert.NotNull(topics);
        Assert.Empty(topics);
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
        using var engine = new MqttEngine(settings);

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
        using var engine = new MqttEngine(settings);
        var messageId = Guid.NewGuid();

        // Act
        var result = engine.TryGetMessage("nonexistent/topic", messageId, out var message);

        // Assert
        Assert.False(result);
        Assert.Null(message);
    }

    [Fact]
    public void UpdateSettings_Should_Reapply_And_Trim_Existing_Topic_Buffers()
    {
        // Arrange: initial rule allows larger buffer for topic pattern
        var initialSettings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort,
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 50_000),
                new TopicBufferLimit("sensors/+/temp", 15_000)
            }
        };
        using var engine = new MqttEngine(initialSettings);

        string topic = "sensors/a/temp";
        int payloadSize = 1500; // bytes
        int messageCount = 12;  // total ~18 KB before trimming to 15 KB

        byte[] MakePayload()
        {
            var bytes = new byte[payloadSize];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(i % 251);
            return bytes;
        }

        for (int i = 0; i < messageCount; i++)
        {
            Assert.True(engine.InjectTestMessage(topic, MakePayload()), "Injection should succeed");
        }

        long sizeAfterInitial = engine.GetCurrentBufferedSize(topic);
        Assert.True(sizeAfterInitial <= 15_000, $"Initial buffer size should respect initial limit (<=15000), was {sizeAfterInitial}");

        // Act: shrink rule to 8 KB
        var shrinkSettings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort,
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 50_000),
                new TopicBufferLimit("sensors/+/temp", 8_000)
            }
        };
        engine.UpdateSettings(shrinkSettings);

        long sizeAfterShrink = engine.GetCurrentBufferedSize(topic);
        Assert.True(sizeAfterShrink <= 8_000, $"Buffer should be trimmed to new limit (<=8000), was {sizeAfterShrink}");

        // Inject more messages; size must not exceed 8 KB
        for (int i = 0; i < 10; i++)
        {
            engine.InjectTestMessage(topic, MakePayload());
        }
        long sizeAfterMoreInjected = engine.GetCurrentBufferedSize(topic);
        Assert.True(sizeAfterMoreInjected <= 8_000, $"After more injections, size must still respect 8K limit, was {sizeAfterMoreInjected}");

        // Act: expand rule to 20 KB
        var expandSettings = new MqttConnectionSettings
        {
            Hostname = TestConfiguration.MqttHostname,
            Port = TestConfiguration.MqttPort,
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 50_000),
                new TopicBufferLimit("sensors/+/temp", 20_000)
            }
        };
        engine.UpdateSettings(expandSettings);

        // Inject more messages to allow growth
        for (int i = 0; i < 10; i++)
        {
            engine.InjectTestMessage(topic, MakePayload());
        }

        long sizeAfterExpand = engine.GetCurrentBufferedSize(topic);
        Assert.True(sizeAfterExpand > 8_000, $"Buffer should have grown beyond previous 8K limit after expansion, was {sizeAfterExpand}");
        Assert.True(sizeAfterExpand <= 20_000, $"Buffer should not exceed new 20K limit, was {sizeAfterExpand}");
    }
}
