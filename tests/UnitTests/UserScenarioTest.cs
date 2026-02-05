using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Test to verify the fix for the user's specific scenario where buffer limits were not working correctly.
/// </summary>
public class UserScenarioTest
{
    [Fact]
    public void UserScenario_BufferLimitsWorkCorrectly()
    {
        // Arrange: User's settings configuration
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("#", 6117121),        // User configured 6MB limit for all topics
                new TopicBufferLimit("test/#", 6117121)    // User configured 6MB limit for test topics
            }
        };

        using var engine = new MqttEngine(settings);

        // Act & Assert: Verify that the user's buffer limits are respected
        
        // For test/viewer/image topic - should use test/# rule (score 21) over # rule (score 1)
        var imageTopicLimit = engine.GetMaxBufferSizeForTopic("test/viewer/image");
        Assert.Equal(6117121, imageTopicLimit);
        
        // For test/viewer/video topic - should use test/# rule (score 21) over # rule (score 1)
        var videoTopicLimit = engine.GetMaxBufferSizeForTopic("test/viewer/video");
        Assert.Equal(6117121, videoTopicLimit);
        
        // For other topics - should use # rule
        var otherTopicLimit = engine.GetMaxBufferSizeForTopic("other/topic");
        Assert.Equal(6117121, otherTopicLimit);
        
        // Verify these are NOT the old 1MB default that was causing the problem
        Assert.NotEqual(1048576, imageTopicLimit); // 1MB = 1048576 bytes
        Assert.NotEqual(1048576, videoTopicLimit);
        Assert.NotEqual(1048576, otherTopicLimit);
        
        // The messages that were failing should now fit:
        // - test/viewer/image (2460512 bytes) < 6117121 bytes ✓
        // - test/viewer/video (6117121 bytes) = 6117121 bytes ✓
        Assert.True(2460512 <= imageTopicLimit, "Image message (2460512 bytes) should fit in buffer");
        Assert.True(6117121 <= videoTopicLimit, "Video message (6117121 bytes) should fit in buffer");
    }
    
    [Fact]
    public void UserScenario_WithoutUserHashRule_UsesCustomDefault()
    {
        // Arrange: User settings without # rule but with custom default
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit("test/#", 6117121)
            },
            DefaultTopicBufferSizeBytes = 10 * 1024 * 1024 // 10MB custom default
        };

        using var engine = new MqttEngine(settings);

        // Act & Assert
        
        // test/viewer topics should use the specific test/# rule
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("test/viewer/image"));
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("test/viewer/video"));
        
        // Other topics should use the custom 10MB default, not the old 1MB default
        var otherTopicLimit = engine.GetMaxBufferSizeForTopic("other/topic");
        Assert.Equal(10 * 1024 * 1024, otherTopicLimit);
        Assert.NotEqual(1048576, otherTopicLimit); // Should NOT be 1MB default
    }
}
