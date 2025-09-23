using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using MQTTnet;
using System;
using System.Threading.Tasks;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Test to reproduce the specific race condition that causes buffer size mismatches.
/// </summary>
public class BufferRaceConditionTest
{
    [Fact]
    public void Test_BufferCreatedWithWrongSize_Then_Fixed()
    {
        // Arrange: Create engine with specific buffer limits
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 6117121),
                new TopicBufferLimit("test/#", 6117121)
            }
        };

        var engine = new MqttEngine(settings);

        // Simulate the scenario where a buffer gets created with default size
        // This could happen if there's a race condition or timing issue
        
        // First, force creation of a buffer with the correct size
        engine.InjectTestMessage("test/viewer/image", new byte[100]);
        
        var actualSizeBefore = engine.GetActualBufferMaxSize("test/viewer/image");
        var calculatedSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");
        
        System.Console.WriteLine($"Before: Calculated={calculatedSize}, Actual={actualSizeBefore}");
        
        // Verify both are correct
        Assert.Equal(6117121, calculatedSize);
        Assert.Equal(6117121, actualSizeBefore);
        
        // Now simulate what happens when a large message comes in
        var largePayload = new byte[2460512]; // Same size as in the logs
        var success = engine.InjectTestMessage("test/viewer/image", largePayload);
        
        var actualSizeAfter = engine.GetActualBufferMaxSize("test/viewer/image");
        var messageCount = engine.GetBufferedMessageCount("test/viewer/image");
        var bufferedSize = engine.GetCurrentBufferedSize("test/viewer/image");
        
        System.Console.WriteLine($"After large message: Success={success}, Actual={actualSizeAfter}, Count={messageCount}, BufferedSize={bufferedSize}");
        
        // The large message should be accepted
        Assert.True(success, "Large message should be accepted");
        Assert.Equal(6117121, actualSizeAfter);
        Assert.Equal(2, messageCount); // Should have both small and large message
        Assert.True(bufferedSize > largePayload.Length, "Buffered size should include both messages");
    }

    [Fact]
    public void Test_SimulateOriginalProblem_WithDefaultSizeBuffer()
    {
        // This test tries to reproduce the exact scenario from the logs
        
        // Create settings that should result in 6MB buffers
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 6117121),
                new TopicBufferLimit("test/#", 6117121)
            }
        };

        var engine = new MqttEngine(settings);
        
        // The logs show that GetMaxBufferSizeForTopic correctly returns 6117121
        var calculatedSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");
        System.Console.WriteLine($"Calculated buffer size: {calculatedSize} bytes");
        Assert.Equal(6117121, calculatedSize);
        
        // But somehow a message gets rejected because the actual buffer limit is 1048576
        // Let's see if we can reproduce this by directly testing the TopicRingBuffer
        
        // Create a ring buffer with the wrong size (like what might happen in a race condition)
        var wrongSizeBuffer = new CrowsNestMqtt.Utils.TopicRingBuffer(1048576); // Default size
        
        var largeMessage = new MqttApplicationMessageBuilder()
            .WithTopic("test/viewer/image")
            .WithPayload(new byte[2460512]) // Size from logs
            .Build();
        
        // Try to add the message to the wrong-sized buffer
        wrongSizeBuffer.AddMessage(largeMessage, Guid.NewGuid());
        
        // Check what happened
        var count = wrongSizeBuffer.Count;
        var currentSize = wrongSizeBuffer.CurrentSizeInBytes;
        var maxSize = wrongSizeBuffer.MaxSizeInBytes;
        
        System.Console.WriteLine($"Wrong-sized buffer: Count={count}, CurrentSize={currentSize}, MaxSize={maxSize}");
        
        // With the wrong buffer size, the message should be rejected (count = 0)
        // This simulates the original problem
        Assert.Equal(1048576, maxSize); // Confirm this is the wrong size
        // The message is too large for this buffer, so it should either:
        // 1. Not be added (count = 0), or
        // 2. Be replaced with a proxy message
        
        // Let's check which behavior we get
        if (count == 0)
        {
            System.Console.WriteLine("Message was rejected - this reproduces the original problem!");
        }
        else
        {
            System.Console.WriteLine($"Message handling result: {count} messages, {currentSize} bytes");
        }
    }
}
