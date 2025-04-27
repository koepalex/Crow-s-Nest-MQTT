using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.Businesslogic.Commands;
using CrowsNestMqtt.Businesslogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Services; // Added for IStatusBarService
using DynamicData;
using NSubstitute;
using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Xunit;

namespace CrowsNestMqtt.Tests.ViewModels
{
    public class SearchFilteringTests
    {
       private readonly ICommandParserService _commandParserService;
       private readonly IMqttService _mqttServiceMock; // Add mock for MessageViewModel
       private readonly IStatusBarService _statusBarServiceMock; // Add mock for MessageViewModel

       public SearchFilteringTests()
       {
           _commandParserService = Substitute.For<ICommandParserService>();
           _mqttServiceMock = Substitute.For<IMqttService>(); // Initialize mock
           _statusBarServiceMock = Substitute.For<IStatusBarService>(); // Initialize mock
       }

        [Fact]
        public void CurrentSearchTerm_WhenChanged_ShouldUpdateFilteredMessageHistory()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Add test messages
            AddTestMessages(viewModel, new[]
            {
                ("sensor/temperature", "temperature reading: 25.0C"),
                ("sensor/humidity", "humidity reading: 45%"),
                ("sensor/temperature", "temperature reading: 26.0C")
            });
            
            // Act
            viewModel.CurrentSearchTerm = "humidity";
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert
            Assert.Single(viewModel.FilteredMessageHistory);
            Assert.Contains("humidity", viewModel.FilteredMessageHistory[0].PayloadPreview);
        }

        [Fact]
        public void CurrentSearchTerm_WhenCleared_ShouldShowAllMessagesForSelectedTopic()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Add test messages
            AddTestMessages(viewModel, new[]
            {
                ("sensor/temperature", "temperature reading: 25.0C"),
                ("sensor/temperature", "temperature reading: 26.0C")
            });
            
            // First apply a search term
            viewModel.CurrentSearchTerm = "25";
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Verify initial filter
            Assert.Single(viewModel.FilteredMessageHistory);
            
            // Create and select a topic node to filter by topic as well
            AddTopicNode(viewModel, "sensor/temperature");
            viewModel.SelectedNode = FindNode(viewModel.TopicTreeNodes, "sensor", "temperature");
            
            // Act - Clear the search term
            viewModel.CurrentSearchTerm = string.Empty;
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert - Should show all messages for the selected topic
            Assert.Equal(2, viewModel.FilteredMessageHistory.Count);
        }

        [Fact]
        public void SelectedNode_WhenChanged_ShouldFilterMessagesByTopic()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Add test messages for multiple topics
            AddTestMessages(viewModel, new[]
            {
                ("sensor/temperature", "temperature reading: 25.0C"),
                ("sensor/humidity", "humidity reading: 45%"),
                ("sensor/temperature", "temperature reading: 26.0C"),
                ("sensor/pressure", "pressure reading: 1013hPa")
            });
            
            // Create topic nodes
            AddTopicNode(viewModel, "sensor/temperature");
            AddTopicNode(viewModel, "sensor/humidity");
            AddTopicNode(viewModel, "sensor/pressure");
            
            // Act - Select temperature node
            viewModel.SelectedNode = FindNode(viewModel.TopicTreeNodes, "sensor", "temperature");
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert
            Assert.Equal(2, viewModel.FilteredMessageHistory.Count);
           Assert.All(viewModel.FilteredMessageHistory,
               msg => Assert.Equal("sensor/temperature", msg.Topic)); // Use msg.Topic directly
       }

        [Fact]
        public void SelectedNode_WhenSetToNull_ShouldShowAllTopics()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Add test messages for multiple topics
            AddTestMessages(viewModel, new[]
            {
                ("sensor/temperature", "temperature reading: 25.0C"),
                ("sensor/humidity", "humidity reading: 45%"),
                ("sensor/pressure", "pressure reading: 1013hPa")
            });
            
            // Create topic nodes
            AddTopicNode(viewModel, "sensor/temperature");
            AddTopicNode(viewModel, "sensor/humidity");
            AddTopicNode(viewModel, "sensor/pressure");
            
            // First select a specific topic to filter
            viewModel.SelectedNode = FindNode(viewModel.TopicTreeNodes, "sensor", "temperature");
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Verify initial filter
            Assert.Single(viewModel.FilteredMessageHistory);
            
            // Act - Clear selection
            viewModel.SelectedNode = null;
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert - Should show all messages
            Assert.Equal(3, viewModel.FilteredMessageHistory.Count);
        }

        [Fact]
        public void CombinedFiltering_ShouldApplyBothTopicAndSearchFilters()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Add test messages
            AddTestMessages(viewModel, new[]
            {
                ("sensor/temperature", "temperature reading: 25.0C"),
                ("sensor/humidity", "humidity reading: 45%"),
                ("sensor/temperature", "temperature reading: 26.0C"),
                ("sensor/temperature", "error: sensor disconnected")
            });
            
            // Create and select topic node
            AddTopicNode(viewModel, "sensor/temperature");
            viewModel.SelectedNode = FindNode(viewModel.TopicTreeNodes, "sensor", "temperature");
            
            // Wait for topic filter to apply
            Thread.Sleep(300);
            
            // Verify topic filter shows 3 temperature messages
            Assert.Equal(3, viewModel.FilteredMessageHistory.Count);
            
            // Act - Add search term
            viewModel.CurrentSearchTerm = "reading";
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert - Should only show temperature readings (not the error message)
            Assert.Equal(2, viewModel.FilteredMessageHistory.Count);
           Assert.All(viewModel.FilteredMessageHistory, msg =>
           {
               Assert.Equal("sensor/temperature", msg.Topic); // Use msg.Topic directly
               Assert.Contains("reading", msg.PayloadPreview);
           });
        }

        [Fact]
        public void Search_ShouldBeCaseInsensitive()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Add test messages with mixed case
            AddTestMessages(viewModel, new[]
            {
                ("sensor/test", "Temperature high"),
                ("sensor/test", "TEMPERATURE low"),
                ("sensor/test", "temperature normal"),
                ("sensor/test", "Not matching")
            });
            
            // Act - Search with lowercase
            viewModel.CurrentSearchTerm = "temperature";
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert
            Assert.Equal(3, viewModel.FilteredMessageHistory.Count);
            
            // Act - Search with uppercase
            viewModel.CurrentSearchTerm = "TEMPERATURE";
            
            // Allow time for the reactive filter to process
            Thread.Sleep(300);
            
            // Assert - Same result regardless of case
            Assert.Equal(3, viewModel.FilteredMessageHistory.Count);
        }

        [Fact]
        public void ApplyTopicFilter_WithFuzzyMatching_ShouldShowPartialMatches()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            
            // Create a variety of topics
            string[] topics = new[] 
            {
                "devices/temperature/living-room",
                "devices/temperature/bedroom",
                "devices/humidity/kitchen",
                "devices/door-sensor/front-door",
                "devices/door-sensor/back-door",
                "devices/motion/hallway"
            };
            
            // Add topics to tree structure
            foreach (var topic in topics)
            {
                AddTopicNode(viewModel, topic);
            }

            // Get the method via reflection
            var filterMethod = typeof(MainViewModel).GetMethod("ApplyTopicFilter", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act - Filter with fuzzy match "door"
            filterMethod?.Invoke(viewModel, new object[] { "door" });
            
            // Assert - Should match door-sensor and any door mentions
            bool anyNonDoorNodeVisible = false;
            bool allDoorNodesVisible = true;
            
            // Check devices node
            var devicesNode = viewModel.TopicTreeNodes.FirstOrDefault(n => n.Name == "devices");
            Assert.True(devicesNode!.IsVisible);
            
            // Check temperature branch - should not be visible
            var temperatureNode = devicesNode.Children.FirstOrDefault(n => n.Name == "temperature");
            if (temperatureNode != null && temperatureNode.IsVisible)
            {
                anyNonDoorNodeVisible = true;
            }
            
            // Check humidity branch - should not be visible
            var humidityNode = devicesNode.Children.FirstOrDefault(n => n.Name == "humidity");
            if (humidityNode != null && humidityNode.IsVisible)
            {
                anyNonDoorNodeVisible = true;
            }
            
            // Check door-sensor branch - should be visible
            var doorSensorNode = devicesNode.Children.FirstOrDefault(n => n.Name == "door-sensor");
            if (doorSensorNode == null || !doorSensorNode.IsVisible)
            {
                allDoorNodesVisible = false;
            }
            else
            {
                // Check child nodes
                foreach (var doorNode in doorSensorNode.Children)
                {
                    if (!doorNode.IsVisible)
                    {
                        allDoorNodesVisible = false;
                        break;
                    }
                }
            }
            
            // Assert results
            Assert.True(allDoorNodesVisible, "All door nodes should be visible");
            Assert.False(anyNonDoorNodeVisible, "Non-door nodes should not be visible");
            Assert.True(viewModel.IsTopicFilterActive, "Topic filter should be active");
        }

        [Fact]
        public void DispatchCommand_Search_ShouldSetSearchTerm()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            var searchCommand = new ParsedCommand(CommandType.Search, new List<string> { "testTerm" });
            
            // Use reflection to get the DispatchCommand method
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            dispatchMethod?.Invoke(viewModel, new object[] { searchCommand });
            
            // Assert
            Assert.Equal("testTerm", viewModel.CurrentSearchTerm);
            Assert.Contains("Search filter applied", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_SearchWithNoArguments_ShouldClearSearchTerm()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            viewModel.CurrentSearchTerm = "previousTerm";
            
            var clearSearchCommand = new ParsedCommand(CommandType.Search, new List<string>()); // Empty arguments list
            
            // Use reflection to get the DispatchCommand method
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            dispatchMethod?.Invoke(viewModel, new object[] { clearSearchCommand });
            
            // Assert
            Assert.Equal(string.Empty, viewModel.CurrentSearchTerm);
            Assert.Contains("Search cleared", viewModel.StatusBarText);
        }

        #region Helper Methods
        
        private void AddTestMessages(MainViewModel viewModel, IEnumerable<(string topic, string payload)> messages)
        {
            // Use reflection to access the private _messageHistorySource field
            var messageSourceField = typeof(MainViewModel).GetField("_messageHistorySource", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            var messageSource = messageSourceField?.GetValue(viewModel) as SourceList<MessageViewModel>;
            
            if (messageSource == null)
                throw new InvalidOperationException("Could not access _messageHistorySource field");
            
           foreach (var (topic, payload) in messages)
           {
               var messageId = Guid.NewGuid();
               var timestamp = DateTime.Now;
               // No need to mock TryGetMessage here as these messages aren't selected in these specific tests
               var message = new MessageViewModel(messageId, topic, timestamp, payload, _mqttServiceMock, _statusBarServiceMock);

               messageSource.Add(message);
            }
            
            // Allow time for the reactive pipeline to process
            Thread.Sleep(100);
        }
        
        private void AddTopicNode(MainViewModel viewModel, string topic)
        {
            // Use reflection to call the private UpdateOrCreateNode method
            var method = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            method?.Invoke(viewModel, new object[] { topic, true });
        }
        
        private NodeViewModel? FindNode(IEnumerable<NodeViewModel> nodes, params string[] path)
        {
            if (path.Length == 0 || nodes == null || !nodes.Any())
                return null;
            
            var currentNode = nodes.FirstOrDefault(n => n.Name == path[0]);
            if (currentNode == null)
                return null;
            
            if (path.Length == 1)
                return currentNode;
            
            return FindNode(currentNode.Children, path.Skip(1).ToArray());
        }
        
        #endregion
    }
}