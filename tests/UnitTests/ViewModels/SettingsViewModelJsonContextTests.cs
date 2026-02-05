using System.Text.Json;
using Xunit;
using CrowsNestMqtt.UI.ViewModels;
using System.Collections.ObjectModel;
using CrowsNestMqtt.BusinessLogic.Configuration;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class SettingsViewModelJsonContextTests
    {
        private sealed class TestViewModel
        {
            public ObservableCollection<TopicBufferLimitViewModel> Limits { get; set; } = 
                new ObservableCollection<TopicBufferLimitViewModel>();
        }
        
        [Fact]
        public void SettingsViewModel_Serialization()
        {
            // Arrange - Create a test settings view model
            var model = new SettingsViewModel();
            
            // Act
            var json = JsonSerializer.Serialize(model);
            
            // Assert
            Assert.NotNull(json);
            Assert.NotEqual("{}", json);
        }
        
        [Fact]
        public void SettingsData_Serialization()
        {
            // Arrange
            var settings = new SettingsData(
                "test.mqtt.server",  // Hostname
                8883,                // Port
                "test-client",       // ClientId
                60,                  // KeepAliveIntervalSeconds
                true,                // CleanSession
                300,                 // SessionExpiryIntervalSeconds
                new AnonymousAuthenticationMode() // AuthMode
            );
            
            // Act
            var json = JsonSerializer.Serialize(settings);
            
            // Assert
            Assert.NotNull(json);
            Assert.Contains("test.mqtt.server", json);
            Assert.Contains("8883", json);
        }
        
        [Fact]
        public void TestTopicBufferLimitViewModel_Serialization()
        {
            // Arrange
            var testViewModel = new TestViewModel();
            testViewModel.Limits.Add(new TopicBufferLimitViewModel 
            {
                TopicFilter = "test/topic",
                MaxSizeBytes = 1000
            });
            
            // Act
            var json = JsonSerializer.Serialize(testViewModel);
            
            // Assert
            Assert.NotNull(json);
            Assert.Contains("test/topic", json);
            Assert.Contains("1000", json);
        }
    }
}
