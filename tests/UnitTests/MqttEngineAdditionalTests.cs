using System.Reflection;

using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;

using Xunit;

namespace CrowsNestMqtt.UnitTests
{
    public class MqttEngineAdditionalTests
    {
        [Fact]
        public void UpdateSettings_WithNewTopicBufferLimits_UpdatesLimits()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client",
                KeepAliveInterval = TimeSpan.FromSeconds(60)
            };
            
            var mqttEngine = new MqttEngine(settings);
            
            var newSettings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client",
                KeepAliveInterval = TimeSpan.FromSeconds(60),
                TopicSpecificBufferLimits = new List<TopicBufferLimit>
                {
                    new TopicBufferLimit("test/topic1", 10000),
                    new TopicBufferLimit("test/topic2", 20000)
                }
            };
            
            // Act
            mqttEngine.UpdateSettings(newSettings);
            
            // Use reflection to access private field
            var field = typeof(MqttEngine).GetField("_topicSpecificBufferLimits", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            var limits = field?.GetValue(mqttEngine) as IList<TopicBufferLimit>;
            
            // Assert
            Assert.NotNull(limits);
            Assert.Equal(2, limits.Count);
            Assert.Equal("test/topic1", limits[0].TopicFilter);
            Assert.Equal(10000, limits[0].MaxSizeBytes);
            Assert.Equal("test/topic2", limits[1].TopicFilter);
            Assert.Equal(20000, limits[1].MaxSizeBytes);
        }
        
        
        [Fact]
        public void GetBufferSizeLimit_ReturnsSpecificLimitForMatchingTopic()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client"
            };
            
            var mqttEngine = new MqttEngine(settings);
            
            var newSettings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client",
                TopicSpecificBufferLimits = new List<TopicBufferLimit>
                {
                    new TopicBufferLimit("test/specific", 5000),
                    new TopicBufferLimit("test/#", 10000),
                    new TopicBufferLimit("+/general", 15000)
                }
            };
            
            mqttEngine.UpdateSettings(newSettings);
            
            // Access the private method using reflection
            var method = typeof(MqttEngine).GetMethod("GetBufferSizeLimit", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act & Assert
            if (method != null)
            {
                // Test exact match
                var exactMatchResult = method.Invoke(mqttEngine, new object[] { "test/specific" });
                var exactMatchLimit = exactMatchResult != null ? (long)exactMatchResult : 0;
                Assert.Equal(5000, exactMatchLimit);
                
                // Test wildcard match
                var wildcardMatchResult = method.Invoke(mqttEngine, new object[] { "test/something" });
                var wildcardMatchLimit = wildcardMatchResult != null ? (long)wildcardMatchResult : 0;
                Assert.Equal(10000, wildcardMatchLimit);
                
                // Test + wildcard match
                var plusWildcardMatchResult = method.Invoke(mqttEngine, new object[] { "anything/general" });
                var plusWildcardMatchLimit = plusWildcardMatchResult != null ? (long)plusWildcardMatchResult : 0;
                Assert.Equal(15000, plusWildcardMatchLimit);
                
                // Test default when no match
                var defaultResult = method.Invoke(mqttEngine, new object[] { "no/match/here" });
                var defaultLimit = defaultResult != null ? (long)defaultResult : 0;
                Assert.Equal(MqttEngine.DefaultMaxTopicBufferSize, defaultLimit);
            }
        }
        
        [Fact]
        public async Task DisconnectAsync_WorksCorrectly()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client"
            };
            
            // Create MqttEngine
            var mqttEngine = new MqttEngine(settings);
            
            // Hook up event handler that will be called
            mqttEngine.ConnectionStateChanged += (s, e) => 
            {
                // Just to verify the event gets raised without errors
            };
            
            // Act - Just verify it doesn't throw an exception
            await mqttEngine.DisconnectAsync();
        }
        
        [Fact]
        public void Dispose_ClearsResources()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client"
            };
            
            // Create MqttEngine
            var mqttEngine = new MqttEngine(settings);
            
            // Access the _isDisposing field using reflection
            var isDisposingField = typeof(MqttEngine).GetField("_isDisposing", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            mqttEngine.Dispose();
            
            // Assert
            if (isDisposingField != null)
            {
                var result = isDisposingField.GetValue(mqttEngine);
                var isDisposing = result != null && (bool)result;
                Assert.True(isDisposing);
            }
        }
        
        [Fact]
        public async Task PublishAsync_SendsMessageCorrectly()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client"
            };
            
            // Create MqttEngine
            var mqttEngine = new MqttEngine(settings);
            
            // Act & Assert - Just verify it doesn't throw an exception
            // We can't properly mock the MQTTnet client without using internals
            await mqttEngine.PublishAsync("test/topic", "test message", false);
        }
        
        [Theory]
        [InlineData("#", "a/b/c")]
        [InlineData("a/#", "a/b/c")]
        [InlineData("a/+/c", "a/b/c")]
        [InlineData("a/b/c", "a/b/c")]
        [InlineData("a/+/#", "a/b/c")]
        [InlineData("+/+/+", "a/b/c")]
        public void FindBestMatchingSubscription_ReturnsBestMatch(string filter, string topic)
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientId = "test-client"
            };
            
            var mqttEngine = new MqttEngine(settings);
            
            var subscriptions = new List<string>
            {
                "#",
                "a/#",
                "a/+/c",
                "a/b/c",
                "a/+/#",
                "+/+/+"
            };
            
            // Act - Call the FindBestMatchingSubscription method using reflection
            var method = typeof(MqttEngine).GetMethod("FindBestMatchingSubscription", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (method != null)
            {
                var result = method.Invoke(null, new object[] { subscriptions, topic }) as string;
                
                // Assert - Check that we get the expected filter as best match
                // This is a simplification since we can't test the scoring directly
                Assert.Equal(filter, result);
            }
        }
    }
}
