using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration; // Added for TopicBufferLimit

namespace CrowsNestMqtt.UnitTests;

// ReSharper disable once ClassNeverInstantiated.Global
public class MqttEngineTopicLimitTests
{
    // Test Cases for MatchTopic
    [Theory]
    [InlineData("sport/tennis/player1", "sport/tennis/player1", 1000)] // Exact match
    [InlineData("sport/tennis/player1", "sport/tennis/+", 25)]        // + wildcard
    [InlineData("sport/tennis/player1/stats", "sport/tennis/#", 21)]  // # wildcard at end
    [InlineData("sport/tennis", "sport/tennis/#", 21)]                // # matching zero levels
    [InlineData("some/topic", "#", 1)]                                // # as only char
    [InlineData("sport", "+", 5)]                                     // + as only char in filter
    [InlineData("sport/tennis", "sport/tennis/player1", -1)]          // No match (filter longer)
    [InlineData("sport/tennis/player1", "sport/tennis", -1)]          // No match (topic longer, filter no #)
    [InlineData("sport/football/player1", "sport/tennis/+", -1)]     // No match (segment mismatch)
    [InlineData("sport/tennis", "sport/#/player1", -1)]               // Invalid filter (# not last)
    [InlineData("a/b/c", "a/+/c", 25)]                                // Filter a/+/c vs topic a/b/c
    [InlineData("a/b/c/d", "a/b/+", -1)]                              // Filter a/b/+ vs topic a/b/c/d
    [InlineData("a/b", "a/b/#", 21)]                                  // # matching zero levels (same as another test but good to have)
    [InlineData("a", "#", 1)]                                         // Single segment topic, # filter
    [InlineData("a", "+", 5)]                                         // Single segment topic, + filter
    [InlineData("a/b/c", "a/b/d", -1)]                                // Segment mismatch at end
    [InlineData("a/b/c", "+/+/+", 15)]                                // All plus wildcards
    [InlineData("a/b/c", "+/b/c", 25)]                                // Leading plus wildcard
    [InlineData("a/b/c", "a/b/+", 25)]                                // Trailing plus wildcard
    [InlineData("a/b/c", "#", 1)]                                     // Topic with multiple segments, # filter
    [InlineData("", "#", -1)]                                         // Empty topic, # filter
    [InlineData("a/b", "", -1)]                                       // Non-empty topic, empty filter
    [InlineData("", "", -1)]                                          // Empty topic, empty filter
    [InlineData("root", "root/#", 11)] 
    [InlineData("root/child", "root/#", 11)] 
    [InlineData("root/child/grandchild", "root/#", 11)] 
    [InlineData("test/topic", "test/topic", 1000)]
    [InlineData("test/topic/sub", "test/topic/#", 21)] 
    public void MatchTopic_ReturnsCorrectScore(string topic, string filter, int expectedScore)
    {
        // Act
        int actualScore = MqttEngine.MatchTopic(topic, filter);

        // Assert
        Assert.Equal(expectedScore, actualScore);
    }

    private MqttEngine CreateEngineWithRules(List<TopicBufferLimit> rules)
    {
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = rules
        };
        return new MqttEngine(settings);
    }

    // Test Cases for GetMaxBufferSizeForTopic
    [Fact]
    public void GetMaxBufferSizeForTopic_ReturnsCorrectSize_BasedOnRules()
    {
        var rules = new List<TopicBufferLimit>
        {
            new TopicBufferLimit(TopicFilter: "exact/match", MaxSizeBytes: 100 ),
            new TopicBufferLimit(TopicFilter: "wildcard/+/one", MaxSizeBytes: 200 ),
            new TopicBufferLimit(TopicFilter: "wildcard/multi/#", MaxSizeBytes: 300 ),
            new TopicBufferLimit(TopicFilter: "long/specific/filter/then/plus/+", MaxSizeBytes: 350 ),
            new TopicBufferLimit(TopicFilter: "long/specific/filter/then/hash/#", MaxSizeBytes: 380 ),
            new TopicBufferLimit(TopicFilter: "#", MaxSizeBytes: 50 ) // Least specific
        };
        using var engine = CreateEngineWithRules(rules);

        Assert.Equal(100, engine.GetMaxBufferSizeForTopic("exact/match"));
        Assert.Equal(200, engine.GetMaxBufferSizeForTopic("wildcard/test/one"));
        Assert.Equal(300, engine.GetMaxBufferSizeForTopic("wildcard/multi/foo/bar"));
        Assert.Equal(350, engine.GetMaxBufferSizeForTopic("long/specific/filter/then/plus/another"));
        Assert.Equal(380, engine.GetMaxBufferSizeForTopic("long/specific/filter/then/hash/another/level"));
        Assert.Equal(50, engine.GetMaxBufferSizeForTopic("unmatched/topic")); // Falls back to "#" rule
        Assert.Equal(300, engine.GetMaxBufferSizeForTopic("wildcard/multi/foo")); // Testing '#' match
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_ReturnsDefault_WhenNoRulesMatchAndNoHashallRule()
    {
        var rules = new List<TopicBufferLimit>
        {
            new TopicBufferLimit(TopicFilter: "specific/topic", MaxSizeBytes: 100),
            // No "#" rule
        };
        using var engine = CreateEngineWithRules(rules);

        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("some/other/topic"));
    }
    
    [Fact]
    public void GetMaxBufferSizeForTopic_HandlesEmptyRulesList()
    {
        using var engine = CreateEngineWithRules(new List<TopicBufferLimit>());
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("any/topic"));
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_HandlesNullRulesListInSettings()
    {
        var settings = new MqttConnectionSettings { TopicSpecificBufferLimits = null! }; // Test null explicitly
        using var engine = new MqttEngine(settings);
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("any/topic"));
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_PrecedenceTest()
    {
        var rules = new List<TopicBufferLimit>
        {
            new TopicBufferLimit(TopicFilter: "foo/bar", MaxSizeBytes: 10 ),      // Most specific for foo/bar
            new TopicBufferLimit(TopicFilter: "foo/+", MaxSizeBytes: 20 ),       // Specific for foo/anything
            new TopicBufferLimit(TopicFilter: "foo/#", MaxSizeBytes: 30 ),       // General for foo/anything/anddeeper
            new TopicBufferLimit(TopicFilter: "#", MaxSizeBytes: 5 ),             // Catch all
        };
        using var engine = CreateEngineWithRules(rules);

        Assert.Equal(10, engine.GetMaxBufferSizeForTopic("foo/bar"));// Matches "foo/bar"
        Assert.Equal(20, engine.GetMaxBufferSizeForTopic("foo/baz"));       // Matches "foo/+"
        Assert.Equal(30, engine.GetMaxBufferSizeForTopic("foo/bar/baz"));   // Matches "foo/#"
        Assert.Equal(5, engine.GetMaxBufferSizeForTopic("other/topic"));  // Matches "#"
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_RuleWithEmptyFilter_IsIgnored()
    {
        var rules = new List<TopicBufferLimit>
        {
            new TopicBufferLimit(TopicFilter: "", MaxSizeBytes: 10000 ), // Empty filter
            new TopicBufferLimit(TopicFilter: "real/topic", MaxSizeBytes: 200 )
        };
        using var engine = CreateEngineWithRules(rules);

        Assert.Equal(200, engine.GetMaxBufferSizeForTopic("real/topic"));
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("another/topic"));
    }

    [Fact]
    public void EnsureDefaultTopicLimit_RespectsUserConfiguredHashRule()
    {
        // Test that user-configured "#" rules are respected and not overridden
        var rules = new List<TopicBufferLimit>
        {
            new TopicBufferLimit(TopicFilter: "#", MaxSizeBytes: 6117121), // User configured 6MB limit
            new TopicBufferLimit(TopicFilter: "test/#", MaxSizeBytes: 6117121)
        };
        using var engine = CreateEngineWithRules(rules);

        // Should use the user-configured "#" rule, not add a default one
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("test/viewer/image"));
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("other/topic"));
    }

    [Fact]
    public void EnsureDefaultTopicLimit_AddsDefaultWhenNoHashRule()
    {
        // Test that default "#" rule is added when none exists
        var rules = new List<TopicBufferLimit>
        {
            new TopicBufferLimit(TopicFilter: "test/#", MaxSizeBytes: 6117121)
        };
        using var engine = CreateEngineWithRules(rules);

        // Should match test/# rule for test topics
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("test/viewer/image"));
        // Should use default 1MB for other topics
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("other/topic"));
    }

    [Fact]
    public void EnsureDefaultTopicLimit_UsesCustomDefaultSize()
    {
        // Test that custom default size is used when no "#" rule exists
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit(TopicFilter: "test/#", MaxSizeBytes: 6117121)
            },
            DefaultTopicBufferSizeBytes = 10 * 1024 * 1024 // 10MB custom default
        };
        using var engine = new MqttEngine(settings);

        // Should match test/# rule for test topics
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("test/viewer/image"));
        // Should use custom 10MB default for other topics
        Assert.Equal(10 * 1024 * 1024, engine.GetMaxBufferSizeForTopic("other/topic"));
    }

    [Fact]
    public void EnsureDefaultTopicLimit_DoesNotOverrideUserHashRule()
    {
        // Test that existing user "#" rule is not overridden even with custom default
        var settings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit(TopicFilter: "#", MaxSizeBytes: 6117121), // User's 6MB rule
                new TopicBufferLimit(TopicFilter: "test/#", MaxSizeBytes: 2000000)
            },
            DefaultTopicBufferSizeBytes = 10 * 1024 * 1024 // 10MB custom default (should be ignored)
        };
        using var engine = new MqttEngine(settings);

        // Should use the more specific test/# rule
        Assert.Equal(2000000, engine.GetMaxBufferSizeForTopic("test/viewer/image"));
        // Should use user's "#" rule, not the custom default
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("other/topic"));
    }

    [Fact]
    public void UpdateSettings_RespectsCustomDefaultSize()
    {
        // Test that UpdateSettings also respects custom default size
        var initialSettings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit(TopicFilter: "test/#", MaxSizeBytes: 1000000)
            }
        };
        using var engine = new MqttEngine(initialSettings);

        var updatedSettings = new MqttConnectionSettings
        {
            TopicSpecificBufferLimits = new List<TopicBufferLimit>
            {
                new TopicBufferLimit(TopicFilter: "test/#", MaxSizeBytes: 6117121)
            },
            DefaultTopicBufferSizeBytes = 8 * 1024 * 1024 // 8MB custom default
        };

        engine.UpdateSettings(updatedSettings);

        // Should use updated test/# rule
        Assert.Equal(6117121, engine.GetMaxBufferSizeForTopic("test/viewer/image"));
        // Should use custom 8MB default for other topics
        Assert.Equal(8 * 1024 * 1024, engine.GetMaxBufferSizeForTopic("other/topic"));
    }
}
