using System.Reflection;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration; // Added for TopicBufferLimit

namespace CrowsNestMqtt.UnitTests
{
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

    public class MqttEngineTests
    {
        [Fact]
        [Trait("Category", "LocalOnly")] // Add this trait to mark the test as local-only
        public async Task MqttEngine_Should_Receive_Published_Message()
        {
            // Arrange
            string brokerHost = "localhost"; // Assuming a local broker for testing
            int brokerPort = 1883; // Default MQTT port
            var connectionSettings = new MqttConnectionSettings
            {
                Hostname = brokerHost,
                Port = brokerPort,
                // Add other necessary default settings for the test if needed
                ClientId = $"test-client-{Guid.NewGuid()}",
                CleanSession = true
            };
            var engine = new MqttEngine(connectionSettings);

           IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null; // Changed type here
           var messageReceivedEvent = new ManualResetEventSlim(false);

            engine.MessageReceived += (sender, args) =>
            {
                if (args.ApplicationMessage.Topic == "test/topic") 
                {
                    receivedArgs = args;
                    messageReceivedEvent.Set();
                }
            };

            await engine.ConnectAsync(CancellationToken.None);

            // Act: Publish a test message using a publisher client.
            var factory = new MqttClientFactory();
            var publisher = factory.CreateMqttClient();
            var publisherOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort) // Removed AddressFamily, let MQTTnet handle defaults
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
        [Trait("Category", "LocalOnly")]
        public async Task MqttEngine_Should_Handle_Empty_Payload_Message()
        {
            // Arrange
            string brokerHost = "localhost"; // Assuming a local broker for testing
            int brokerPort = 1883; // Default MQTT port
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

            engine.MessageReceived += (sender, args) =>
            {
                if (args.ApplicationMessage.Topic == "test/empty_payload_topic")
                {
                    receivedArgs = args;
                    messageReceivedEvent.Set();
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
                Hostname = "localhost",
                Port = 1883,
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
                Hostname = "localhost",
                Port = 1883,
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
    }
}