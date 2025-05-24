using Xunit;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Configuration;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class SettingsViewModelTopicLimitTests
    {
        [Fact]
        public void From_PopulatesTopicSpecificLimitsCorrectly()
        {
            // Arrange
            var settingsData = new SettingsData(
                Hostname: "test.host",
                Port: 1883,
                ClientId: "testClient",
                KeepAliveIntervalSeconds: 60,
                CleanSession: true,
                SessionExpiryIntervalSeconds: 0
            ) // Primary constructor
            { 
                // Initialize the list property
                TopicSpecificBufferLimits = new List<TopicBufferLimit>
                {
                    new TopicBufferLimit { TopicFilter = "topic/1", MaxSizeBytes = 1000 },
                    new TopicBufferLimit { TopicFilter = "topic/2", MaxSizeBytes = 2000 }
                }
            };
            var viewModel = new SettingsViewModel();

            // Act
            viewModel.From(settingsData);

            // Assert
            Assert.Equal(settingsData.TopicSpecificBufferLimits.Count, viewModel.TopicSpecificLimits.Count);
            for (int i = 0; i < settingsData.TopicSpecificBufferLimits.Count; i++)
            {
                Assert.Equal(settingsData.TopicSpecificBufferLimits[i].TopicFilter, viewModel.TopicSpecificLimits[i].TopicFilter);
                Assert.Equal(settingsData.TopicSpecificBufferLimits[i].MaxSizeBytes, viewModel.TopicSpecificLimits[i].MaxSizeBytes);
            }
        }

        [Fact]
        public void Into_PopulatesSettingsDataCorrectly()
        {
            // Arrange
            var viewModel = new SettingsViewModel(); // SettingsViewModel constructor loads settings, ensure it's clean or mock dependencies if needed
            viewModel.TopicSpecificLimits.Add(new TopicBufferLimitViewModel { TopicFilter = "vm/topic/1", MaxSizeBytes = 3000 });
            viewModel.TopicSpecificLimits.Add(new TopicBufferLimitViewModel { TopicFilter = "vm/topic/2", MaxSizeBytes = 4000 });
            
            // Set some other basic properties to ensure they are also mapped
            viewModel.Hostname = "vm.host";
            viewModel.Port = 1884;

            // Act
            SettingsData settingsData = viewModel.Into();

            // Assert
            Assert.Equal(viewModel.Hostname, settingsData.Hostname);
            Assert.Equal(viewModel.Port, settingsData.Port);
            Assert.Equal(viewModel.TopicSpecificLimits.Count, settingsData.TopicSpecificBufferLimits.Count);
            for (int i = 0; i < viewModel.TopicSpecificLimits.Count; i++)
            {
                Assert.Equal(viewModel.TopicSpecificLimits[i].TopicFilter, settingsData.TopicSpecificBufferLimits[i].TopicFilter);
                Assert.Equal(viewModel.TopicSpecificLimits[i].MaxSizeBytes, settingsData.TopicSpecificBufferLimits[i].MaxSizeBytes);
            }
        }

        [Fact]
        public void SettingsData_TopicSpecificBufferLimits_SerializesAndDeserializesCorrectly()
        {
            // Arrange
            var originalSettingsData = new SettingsData(
                Hostname: "serialize.test",
                Port: 1885,
                ClientId: "serializeClient",
                KeepAliveIntervalSeconds: 30,
                CleanSession: false,
                SessionExpiryIntervalSeconds: 300
            )
            {
                TopicSpecificBufferLimits = new List<TopicBufferLimit>
                {
                    new TopicBufferLimit { TopicFilter = "serialize/topic/a", MaxSizeBytes = 5000 },
                    new TopicBufferLimit { TopicFilter = "serialize/topic/b", MaxSizeBytes = 6000 }
                }
            };

            // Act
            // Need the JsonContext from SettingsViewModel.cs
            // [JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.SettingsData))]
            // internal partial class SettingsViewModelJsonContext : JsonSerializerContext {}
            // We assume SettingsViewModelJsonContext.Default.SettingsData is correctly configured
            
            string jsonString = JsonSerializer.Serialize(originalSettingsData, SettingsViewModelJsonContext.Default.SettingsData);
            var deserializedSettingsData = JsonSerializer.Deserialize(jsonString, SettingsViewModelJsonContext.Default.SettingsData);

            // Assert
            Assert.NotNull(deserializedSettingsData);
            Assert.Equal(originalSettingsData.Hostname, deserializedSettingsData.Hostname);
            Assert.NotNull(deserializedSettingsData.TopicSpecificBufferLimits);
            Assert.Equal(originalSettingsData.TopicSpecificBufferLimits.Count, deserializedSettingsData.TopicSpecificBufferLimits.Count);
            for (int i = 0; i < originalSettingsData.TopicSpecificBufferLimits.Count; i++)
            {
                Assert.Equal(originalSettingsData.TopicSpecificBufferLimits[i].TopicFilter, deserializedSettingsData.TopicSpecificBufferLimits[i].TopicFilter);
                Assert.Equal(originalSettingsData.TopicSpecificBufferLimits[i].MaxSizeBytes, deserializedSettingsData.TopicSpecificBufferLimits[i].MaxSizeBytes);
            }
        }
        
        [Fact]
        public void SettingsData_WithEmptyTopicSpecificBufferLimits_SerializesAndDeserializesCorrectly()
        {
            // Arrange
            var originalSettingsData = new SettingsData(
                Hostname: "empty.list.test",
                Port: 1886,
                ClientId: "emptyListClient",
                KeepAliveIntervalSeconds: 45,
                CleanSession: true,
                SessionExpiryIntervalSeconds: null
            )
            {
                TopicSpecificBufferLimits = new List<TopicBufferLimit>() // Empty list
            };

            // Act
            string jsonString = JsonSerializer.Serialize(originalSettingsData, SettingsViewModelJsonContext.Default.SettingsData);
            var deserializedSettingsData = JsonSerializer.Deserialize(jsonString, SettingsViewModelJsonContext.Default.SettingsData);

            // Assert
            Assert.NotNull(deserializedSettingsData);
            Assert.NotNull(deserializedSettingsData.TopicSpecificBufferLimits);
            Assert.Empty(deserializedSettingsData.TopicSpecificBufferLimits);
        }
        
        [Fact]
        public void SettingsData_WithNullTopicSpecificBufferLimits_SerializesAsEmptyAndDeserializesAsEmpty()
        {
            // Arrange
            // The record property `IList<TopicBufferLimit> TopicSpecificBufferLimits { get; init; } = new List<TopicBufferLimit>();`
            // is initialized to an empty list, so it won't be null from the constructor.
            // This test verifies behavior if it *could* be null (e.g. if loaded from older JSON without the property)
            // or if the default initializer was removed.
            // For System.Text.Json, non-nullable reference types are usually initialized if not present in JSON.
            // Let's test deserialization of JSON that explicitly sets it to null or omits it.

            // Scenario 1: Property is omitted in JSON (should deserialize to default empty list)
            string jsonOmitted = @"{""Hostname"":""null.list.test"", ""Port"":1887}";
            var deserializedOmitted = JsonSerializer.Deserialize(jsonOmitted, SettingsViewModelJsonContext.Default.SettingsData);
            Assert.NotNull(deserializedOmitted);
            Assert.NotNull(deserializedOmitted.TopicSpecificBufferLimits);
            Assert.Empty(deserializedOmitted.TopicSpecificBufferLimits);


            // Scenario 2: Property is explicitly null in JSON (should deserialize to default empty list for non-nullable record prop)
            // However, if the property in SettingsData was `IList<TopicBufferLimit>?` (nullable), then it would be null.
            // Since it's `IList<TopicBufferLimit>` (non-nullable), System.Text.Json might throw or use default.
            // Given `init; } = new List<TopicBufferLimit>();`, it should robustly handle null from JSON by falling back to the initializer.
            string jsonNull = @"{""Hostname"":""null.list.test"", ""Port"":1887, ""TopicSpecificBufferLimits"":null}";
            var deserializedNull = JsonSerializer.Deserialize(jsonNull, SettingsViewModelJsonContext.Default.SettingsData);
            Assert.NotNull(deserializedNull);
            Assert.NotNull(deserializedNull.TopicSpecificBufferLimits); // Should be initialized to new List<>() by the record's default
            Assert.Empty(deserializedNull.TopicSpecificBufferLimits); // And thus empty
        }
    }
}
