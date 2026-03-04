using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Debug test to check what buffer sizes are actually being calculated.
/// </summary>
public class DebugBufferSizeTest
{
    [Fact]
    public void Debug_GetMaxBufferSizeForTopic_UserSettings()
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

        // Act: Check what buffer sizes are calculated
        var imageTopicSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");
        var videoTopicSize = engine.GetMaxBufferSizeForTopic("test/viewer/video");
        var otherTopicSize = engine.GetMaxBufferSizeForTopic("other/topic");

        // Debug output what we're getting
        System.Console.WriteLine($"test/viewer/image -> {imageTopicSize} bytes");
        System.Console.WriteLine($"test/viewer/video -> {videoTopicSize} bytes");
        System.Console.WriteLine($"other/topic -> {otherTopicSize} bytes");

        // Verify the values
        Assert.Equal(6117121, imageTopicSize);
        Assert.Equal(6117121, videoTopicSize);
        Assert.Equal(6117121, otherTopicSize);
    }

    [Fact]
    public void Debug_TopicMatching_Scores()
    {
        // Debug the topic matching scores
        var topic = "test/viewer/image";
        
        var hashScore = MqttEngine.MatchTopic(topic, "#");
        var testHashScore = MqttEngine.MatchTopic(topic, "test/#");
        
        System.Console.WriteLine($"Topic: {topic}");
        System.Console.WriteLine($"  '#' score: {hashScore}");
        System.Console.WriteLine($"  'test/#' score: {testHashScore}");
        
        // Verify test/# has higher score than #
        Assert.True(testHashScore > hashScore, $"test/# score ({testHashScore}) should be higher than # score ({hashScore})");
    }

    [Fact]
    public void Debug_EnsureDefaultTopicLimit_Logic()
    {
        // Test the EnsureDefaultTopicLimit logic with user's settings
        var userLimits = new List<TopicBufferLimit>
        {
            new TopicBufferLimit("#", 6117121),
            new TopicBufferLimit("test/#", 6117121)
        };

        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = userLimits
        };

        using var engine = new MqttEngine(settings);

        // Verify that the settings are correctly applied by checking the buffer sizes
        var hashTopicSize = engine.GetMaxBufferSizeForTopic("some/random/topic");
        var testTopicSize = engine.GetMaxBufferSizeForTopic("test/viewer/image");

        System.Console.WriteLine($"Random topic -> {hashTopicSize} bytes (should be 6117121)");
        System.Console.WriteLine($"Test topic -> {testTopicSize} bytes (should be 6117121)");

        // Both should use user's configured limits, not the default 1MB
        Assert.Equal(6117121, hashTopicSize);
        Assert.Equal(6117121, testTopicSize);
        Assert.NotEqual(1048576, hashTopicSize); // Should NOT be default 1MB
        Assert.NotEqual(1048576, testTopicSize); // Should NOT be default 1MB
    }
}
