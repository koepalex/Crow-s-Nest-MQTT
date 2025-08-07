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
        var engine = CreateEngineWithRules(rules);

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
        var engine = CreateEngineWithRules(rules);

        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("some/other/topic"));
    }
    
    [Fact]
    public void GetMaxBufferSizeForTopic_HandlesEmptyRulesList()
    {
        var engine = CreateEngineWithRules(new List<TopicBufferLimit>());
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("any/topic"));
    }

    [Fact]
    public void GetMaxBufferSizeForTopic_HandlesNullRulesListInSettings()
    {
        var settings = new MqttConnectionSettings { TopicSpecificBufferLimits = null! }; // Test null explicitly
        var engine = new MqttEngine(settings);
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
        var engine = CreateEngineWithRules(rules);

        Assert.Equal(10, engine.GetMaxBufferSizeForTopic("foo/bar"));       // Matches "foo/bar"
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
        var engine = CreateEngineWithRules(rules);

        Assert.Equal(200, engine.GetMaxBufferSizeForTopic("real/topic"));
        Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, engine.GetMaxBufferSizeForTopic("another/topic"));
    }
}
