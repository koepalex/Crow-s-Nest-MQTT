using Xunit;
using CrowsNestMqtt.Utils;
using MQTTnet;
using System;
using System.Collections.Generic;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Test to verify that TopicMessageStore correctly handles wildcard patterns like # and +.
/// </summary>
public class TopicMessageStoreWildcardTest
{
    [Fact]
    public void TopicMessageStore_Should_Use_Wildcard_Patterns_For_Buffer_Limits()
    {
        // Arrange: Create a TopicMessageStore with wildcard patterns
        var limits = new Dictionary<string, long>
        {
            { "#", 6117121 },           // Global wildcard
            { "test/#", 6117121 },      // Test topic wildcard
            { "other/+/data", 2000000 } // Single-level wildcard
        };
        
        var store = new TopicMessageStore(1048576, limits); // Default 1MB, but should use wildcards

        // Create a large message that would exceed 1MB but fit in 6MB
        var largePayload = new byte[2460512]; // Same size as in the original issue
        var message = new MqttApplicationMessageBuilder()
            .WithTopic("test/viewer/image")
            .WithPayload(largePayload)
            .Build();

        var messageId = Guid.NewGuid();

        // Act: Add the message to the store
        var batch = new[] { (messageId, "test/viewer/image", message) };
        var (added, evicted) = store.AddBatch(batch);

        // Assert: The message should be added successfully (not rejected)
        Assert.Single(added);
        Assert.Empty(evicted);
        Assert.Equal(messageId, added[0].MessageId);
        Assert.Equal("test/viewer/image", added[0].Topic);
        
        // Verify we can retrieve the message
        var retrievedMessages = store.GetBufferedMessages("test/viewer/image");
        Assert.Single(retrievedMessages);
        Assert.Equal(messageId, retrievedMessages.First().MessageId);
    }

    [Fact]
    public void TopicMessageStore_Should_Match_Hash_Wildcard_Correctly()
    {
        // Arrange: Create store with # wildcard only
        var limits = new Dictionary<string, long>
        {
            { "#", 5000000 } // 5MB for all topics
        };
        
        var store = new TopicMessageStore(1048576, limits); // Default 1MB
        
        var largePayload = new byte[2000000]; // 2MB payload
        var topics = new[] { "any/topic", "a/b/c/d", "single", "very/deep/nested/topic/structure" };
        
        // Act & Assert: All topics should use the # wildcard limit
        foreach (var topic in topics)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(largePayload)
                .Build();

            var messageId = Guid.NewGuid();
            var batch = new[] { (messageId, topic, message) };
            var (added, evicted) = store.AddBatch(batch);

            // Should be added successfully with the 5MB limit, not rejected with 1MB limit
            Assert.Single(added);
            Assert.Empty(evicted);
        }
    }

    [Fact]
    public void TopicMessageStore_Should_Match_Plus_Wildcard_Correctly()
    {
        // Arrange: Create store with + wildcard
        var limits = new Dictionary<string, long>
        {
            { "sensor/+/data", 3000000 }, // 3MB for sensor data
            { "#", 1048576 }              // 1MB default
        };
        
        var store = new TopicMessageStore(1048576, limits);
        
        var largePayload = new byte[2000000]; // 2MB payload
        
        // Act & Assert: Topics matching sensor/+/data should use 3MB limit
        var matchingTopics = new[] { "sensor/temperature/data", "sensor/humidity/data", "sensor/pressure/data" };
        
        foreach (var topic in matchingTopics)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(largePayload)
                .Build();

            var messageId = Guid.NewGuid();
            var batch = new[] { (messageId, topic, message) };
            var (added, evicted) = store.AddBatch(batch);

            Assert.Single(added);
            Assert.Empty(evicted);
        }
        
        // Topics NOT matching the pattern should use the # fallback (1MB) and be rejected
        var nonMatchingTopics = new[] { "sensor/temperature/config", "other/temperature/data" };
        
        foreach (var topic in nonMatchingTopics)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(largePayload)
                .Build();

            var messageId = Guid.NewGuid();
            var batch = new[] { (messageId, topic, message) };
            var (added, evicted) = store.AddBatch(batch);

            // Should be rejected because 2MB > 1MB limit from # fallback
            Assert.Empty(added);
        }
    }
}
