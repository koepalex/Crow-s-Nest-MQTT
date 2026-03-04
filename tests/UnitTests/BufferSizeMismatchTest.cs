using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using MQTTnet;
using System;
using System.Threading.Tasks;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Test to reproduce the buffer size mismatch issue from the logs.
/// </summary>
public class BufferSizeMismatchTest
{
    [Fact]
    public void Test_BufferSizeMismatch_Scenario()
    {
        // Arrange: User's exact settings from the log
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 6117121),        // User configured 6MB limit
                new TopicBufferLimit("test/#", 6117121)    // User configured 6MB limit for test topics
            }
        };

        using var engine = new MqttEngine(settings);

        // Simulate a large message similar to the one in the logs (2460512 bytes)
        var largePayload = new byte[2460512];
        Random.Shared.NextBytes(largePayload);

        var testMessage = new MqttApplicationMessageBuilder()
            .WithTopic("test/viewer/image")
            .WithPayload(largePayload)
            .Build();

        // Act: Inject the message and check what happens
        var success = engine.InjectTestMessage("test/viewer/image", largePayload);

        // Get buffer information
        var bufferedSize = engine.GetCurrentBufferedSize("test/viewer/image");
        var messageCount = engine.GetBufferedMessageCount("test/viewer/image");
        var calculatedBufferSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");

        // Debug output
        System.Console.WriteLine($"Message injection success: {success}");
        System.Console.WriteLine($"Calculated buffer size: {calculatedBufferSize} bytes");
        System.Console.WriteLine($"Current buffered size: {bufferedSize} bytes");
        System.Console.WriteLine($"Message count: {messageCount}");
        System.Console.WriteLine($"Message size: {largePayload.Length} bytes");

        // Verify the message was added successfully
        Assert.True(success, "Large message should be added successfully to the buffer");
        Assert.Equal(6117121, calculatedBufferSize);
        Assert.True(bufferedSize > 0, "Buffer should contain the message");
        Assert.True(messageCount > 0, "Buffer should contain at least one message");
        
        // The message should fit since 2460512 < 6117121
        Assert.True(largePayload.Length <= calculatedBufferSize, 
            $"Message size ({largePayload.Length}) should be less than buffer limit ({calculatedBufferSize})");
    }

    [Fact]
    public void Test_BufferCreation_WithCorrectSize()
    {
        // Arrange: Settings that should create a 6MB buffer
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 6117121),
                new TopicBufferLimit("test/#", 6117121)
            }
        };

        using var engine = new MqttEngine(settings);

        // Act: Force buffer creation by injecting a small message first
        var smallPayload = new byte[100];
        var success = engine.InjectTestMessage("test/viewer/image", smallPayload);

        // Get the buffer and check its max size
        var messages = engine.GetBufferedMessagesForTopic("test/viewer/image");
        var calculatedSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");

        // Debug output
        System.Console.WriteLine($"Small message injection success: {success}");
        System.Console.WriteLine($"Calculated buffer size: {calculatedSize} bytes");
        System.Console.WriteLine($"Messages in buffer: {messages?.Count() ?? 0}");

        // Verify
        Assert.True(success);
        Assert.Equal(6117121, calculatedSize);
        Assert.NotNull(messages);
        Assert.Single(messages);

        // Now try the large message
        var largePayload = new byte[2460512];
        var largeSuccess = engine.InjectTestMessage("test/viewer/image", largePayload);
        var largeMessages = engine.GetBufferedMessagesForTopic("test/viewer/image");

        System.Console.WriteLine($"Large message injection success: {largeSuccess}");
        System.Console.WriteLine($"Messages in buffer after large message: {largeMessages?.Count() ?? 0}");

        Assert.True(largeSuccess, "Large message should be accepted");
        Assert.Equal(2, largeMessages?.Count());
    }

    [Fact]
    public void Test_Buffer_MaxSizeProperty()
    {
        // This test verifies that buffers created have the correct MaxSizeInBytes property
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 6117121),
                new TopicBufferLimit("test/#", 6117121)
            }
        };

        using var engine = new MqttEngine(settings);

        // Force buffer creation
        engine.InjectTestMessage("test/viewer/image", new byte[100]);

        // Check both calculated and actual buffer sizes
        var calculatedSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");
        var actualBufferSize = engine.GetActualBufferMaxSize("test/viewer/image");
        
        System.Console.WriteLine($"Calculated buffer size: {calculatedSize} bytes");
        System.Console.WriteLine($"Actual buffer max size: {actualBufferSize} bytes");
        
        Assert.Equal(6117121, calculatedSize);
        Assert.Equal(6117121, actualBufferSize);
        Assert.Equal(calculatedSize, actualBufferSize); // They should match!
    }
}
