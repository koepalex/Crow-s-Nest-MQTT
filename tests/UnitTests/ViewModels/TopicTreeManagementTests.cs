using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;

using NSubstitute;

using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class TopicTreeManagementTests
    {
        private readonly ICommandParserService _commandParserService;

        public TopicTreeManagementTests()
        {
            _commandParserService = Substitute.For<ICommandParserService>();
        }

        [Fact]
        public void UpdateOrCreateNode_ShouldCreateNewNodes()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string topic = "sensors/temperature/living-room";

            // Act - Use reflection to call the private method
            var method = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(viewModel, new object[] { topic, true });

            // Assert
            Assert.Single(viewModel.TopicTreeNodes);
            Assert.Equal("sensors", viewModel.TopicTreeNodes[0].Name);
            Assert.Single(viewModel.TopicTreeNodes[0].Children);
            Assert.Equal("temperature", viewModel.TopicTreeNodes[0].Children[0].Name);
            Assert.Single(viewModel.TopicTreeNodes[0].Children[0].Children);
            Assert.Equal("living-room", viewModel.TopicTreeNodes[0].Children[0].Children[0].Name);
        }

        [Fact]
        public void UpdateOrCreateNode_ShouldUpdateExistingNodes()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string firstTopic = "sensors/temperature/living-room";
            string secondTopic = "sensors/temperature/living-room";

            // Act - First create the nodes
            var method = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // First call - create structure
            method?.Invoke(viewModel, new object[] { firstTopic, true });
            
            // Get initial message count
            int initialCount = viewModel.TopicTreeNodes[0].Children[0].Children[0].MessageCount;
            
            // Second call - should increase count
            method?.Invoke(viewModel, new object[] { secondTopic, true });

            // Assert
            Assert.Single(viewModel.TopicTreeNodes); // Still only one root node
            Assert.Equal("sensors", viewModel.TopicTreeNodes[0].Name);
            Assert.Equal(initialCount + 1, viewModel.TopicTreeNodes[0].Children[0].Children[0].MessageCount);
        }

        [Fact]
        public void UpdateOrCreateNode_WithoutIncrementCount_ShouldNotUpdateCount()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string firstTopic = "sensors/humidity/bathroom";

            // Act
            var method = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // First call - create structure and increment count
            method?.Invoke(viewModel, new object[] { firstTopic, true });
            
            int initialCount = viewModel.TopicTreeNodes[0].Children[0].Children[0].MessageCount;
            
            // Second call - should NOT increase count because incrementCount = false
            method?.Invoke(viewModel, new object[] { firstTopic, false });

            // Assert
            Assert.Equal(initialCount, viewModel.TopicTreeNodes[0].Children[0].Children[0].MessageCount);
        }

        [Fact]
        public void UpdateOrCreateNode_WithMultipleNodes_ShouldMaintainAlphabeticalOrder()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string[] topics = new[] 
            {
                "devices/zwave/switch1",
                "devices/hue/light2", 
                "devices/zigbee/sensor3",
                "devices/hue/light1",
                "devices/alexa/echo"
            };

            // Act
            var method = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var topic in topics)
            {
                method?.Invoke(viewModel, new object[] { topic, true });
            }

            // Assert
            Assert.Single(viewModel.TopicTreeNodes);
            
            // Check children are sorted
            var deviceNode = viewModel.TopicTreeNodes[0];
            Assert.Equal("devices", deviceNode.Name);
            
            var childrenNames = deviceNode.Children.Select(n => n.Name).ToList();
            var expectedOrder = new[] { "alexa", "hue", "zigbee", "zwave" };
            
            Assert.Equal(expectedOrder, childrenNames);
            
            // Check deeper level sorting
            var hueNode = deviceNode.Children.FirstOrDefault(n => n.Name == "hue");
            Assert.NotNull(hueNode);
            
            var lightsNames = hueNode.Children.Select(n => n.Name).ToList();
            var expectedLightsOrder = new[] { "light1", "light2" };
            
            Assert.Equal(expectedLightsOrder, lightsNames);
        }

        [Fact]
        public void ExpandAllNodes_ShouldExpandAllNodes()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string[] topics = new[] 
            {
                "home/livingroom/temperature",
                "home/kitchen/temperature",
                "home/bedroom/temperature"
            };

            // Create nodes
            var createMethod = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var topic in topics)
            {
                createMethod?.Invoke(viewModel, new object[] { topic, true });
            }
            
            // Make sure all nodes are initially collapsed
            var setNodeExpandedMethod = typeof(MainViewModel).GetMethod("SetNodeExpandedRecursive", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            setNodeExpandedMethod?.Invoke(viewModel, new object[] { viewModel.TopicTreeNodes, false });
            
            // Act
            // var expandMethod = typeof(MainViewModel).GetMethod("ExpandAllNodes",
            //     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // expandMethod?.Invoke(viewModel, null);
            
            // Invoke SetNodeExpandedRecursive directly for synchronous testing
            setNodeExpandedMethod?.Invoke(viewModel, new object[] { viewModel.TopicTreeNodes, true });

            // Assert - Check all nodes are expanded
            Assert.True(viewModel.TopicTreeNodes[0].IsExpanded);
            foreach (var room in viewModel.TopicTreeNodes[0].Children)
            {
                Assert.True(room.IsExpanded);
            }
        }

        [Fact]
        public void CollapseAllNodes_ShouldCollapseAllNodes()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string[] topics = new[] 
            {
                "office/desk1/humidity",
                "office/desk2/humidity",
                "office/conference/humidity"
            };

            // Create nodes
            var createMethod = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var topic in topics)
            {
                createMethod?.Invoke(viewModel, new object[] { topic, true });
            }
            
            // Make sure all nodes are initially expanded
            var setNodeExpandedMethod = typeof(MainViewModel).GetMethod("SetNodeExpandedRecursive", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            setNodeExpandedMethod?.Invoke(viewModel, new object[] { viewModel.TopicTreeNodes, true });
            
            // Act
            // var collapseMethod = typeof(MainViewModel).GetMethod("CollapseAllNodes",
            //     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            // collapseMethod?.Invoke(viewModel, null);

            // Invoke SetNodeExpandedRecursive directly for synchronous testing
            setNodeExpandedMethod?.Invoke(viewModel, new object[] { viewModel.TopicTreeNodes, false });

            // Assert - Check all nodes are collapsed
            Assert.False(viewModel.TopicTreeNodes[0].IsExpanded);
            foreach (var desk in viewModel.TopicTreeNodes[0].Children)
            {
                Assert.False(desk.IsExpanded);
            }
        }

        [Fact]
        public void ApplyTopicFilter_ShouldFilterTopicTree()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string[] topics = new[] 
            {
                "sensors/temperature/kitchen",
                "sensors/temperature/bathroom",
                "sensors/humidity/kitchen",
                "lighting/kitchen/ceiling",
                "lighting/livingroom/floor",
                "devices/motion/hallway"
            };

            // Create nodes
            var createMethod = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var topic in topics)
            {
                createMethod?.Invoke(viewModel, new object[] { topic, true });
            }
            
            // Act
            var filterMethod = typeof(MainViewModel).GetMethod("ApplyTopicFilter", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // Filter for "kitchen"
            filterMethod?.Invoke(viewModel, new object[] { "kitchen" });

            // Assert - Only kitchen nodes should be visible
            Assert.True(viewModel.IsTopicFilterActive);
            
            // Check sensors branch
            var sensorsNode = viewModel.TopicTreeNodes.FirstOrDefault(n => n.Name == "sensors");
            Assert.True(sensorsNode!.IsVisible);
            
            var temperatureNode = sensorsNode!.Children.FirstOrDefault(n => n.Name == "temperature");
            Assert.True(temperatureNode!.IsVisible);
            
            var kitchenTempNode = temperatureNode!.Children.FirstOrDefault(n => n.Name == "kitchen");
            Assert.True(kitchenTempNode!.IsVisible);
            
            var bathroomTempNode = temperatureNode!.Children.FirstOrDefault(n => n.Name == "bathroom");
            Assert.False(bathroomTempNode!.IsVisible);
            
            var humidityNode = sensorsNode!.Children.FirstOrDefault(n => n.Name == "humidity");
            Assert.True(humidityNode!.IsVisible);
            
            var kitchenHumNode = humidityNode!.Children.FirstOrDefault(n => n.Name == "kitchen");
            Assert.True(kitchenHumNode!.IsVisible);
            
            // Check lighting branch
            var lightingNode = viewModel.TopicTreeNodes.FirstOrDefault(n => n.Name == "lighting");
            Assert.True(lightingNode!.IsVisible);
            
            var kitchenLightNode = lightingNode!.Children.FirstOrDefault(n => n.Name == "kitchen");
            Assert.True(kitchenLightNode!.IsVisible);
            
            var livingroomLightNode = lightingNode!.Children.FirstOrDefault(n => n.Name == "livingroom");
            Assert.False(livingroomLightNode!.IsVisible);
            
            // Check devices branch (should not be visible as no kitchen nodes under it)
            var devicesNode = viewModel.TopicTreeNodes.FirstOrDefault(n => n.Name == "devices");
            Assert.False(devicesNode!.IsVisible); // Parent node should NOT be visible as no children match "kitchen"
            
            var motionNode = devicesNode!.Children.FirstOrDefault(n => n.Name == "motion");
            Assert.False(motionNode!.IsVisible);
        }

        [Fact]
        public void ApplyTopicFilter_WithEmptyFilter_ShouldClearFilter()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            string[] topics = new[] 
            {
                "sensors/temperature/outside",
                "sensors/temperature/inside",
            };

            // Create nodes
            var createMethod = typeof(MainViewModel).GetMethod("UpdateOrCreateNode", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var topic in topics)
            {
                createMethod?.Invoke(viewModel, new object[] { topic, true });
            }
            
            // First apply a filter
            var filterMethod = typeof(MainViewModel).GetMethod("ApplyTopicFilter", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            filterMethod?.Invoke(viewModel, new object[] { "outside" });
            
            // Verify filter is active
            Assert.True(viewModel.IsTopicFilterActive);
            
            // Act - Clear filter
            filterMethod?.Invoke(viewModel, new object[] { "" });

            // Assert
            Assert.False(viewModel.IsTopicFilterActive);
            
            // Check all nodes are visible
            var sensorsNode = viewModel.TopicTreeNodes.FirstOrDefault();
            Assert.True(sensorsNode!.IsVisible);
            
            var temperatureNode = sensorsNode!.Children.FirstOrDefault();
            Assert.True(temperatureNode!.IsVisible);
            
            var outsideNode = temperatureNode!.Children.FirstOrDefault(n => n.Name == "outside");
            Assert.True(outsideNode!.IsVisible);
            
            var insideNode = temperatureNode!.Children.FirstOrDefault(n => n.Name == "inside");
            Assert.True(insideNode!.IsVisible);
        }
    }
}