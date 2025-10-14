using CrowsNestMqtt.UI.Services;
using CrowsNestMqtt.UI.ViewModels;
using System;
using System.Linq;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class TopicTreeManagerTests
{
    [Fact]
    public void Constructor_InitializesEmptyTree()
    {
        // Arrange & Act
        var manager = new TopicTreeManager();

        // Assert
        Assert.NotNull(manager.TopicTreeNodes);
        Assert.Empty(manager.TopicTreeNodes);
        Assert.False(manager.IsTopicFilterActive);
    }

    [Fact]
    public void UpdateOrCreateNode_WithSimpleTopic_CreatesNode()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNode("sensor");

        // Assert
        Assert.Single(manager.TopicTreeNodes);
        var node = manager.TopicTreeNodes.First();
        Assert.Equal("sensor", node.Name);
        Assert.Equal("sensor", node.FullPath);
        Assert.Equal(1, node.MessageCount);
    }

    [Fact]
    public void UpdateOrCreateNode_WithHierarchicalTopic_CreatesNodeHierarchy()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNode("sensor/temperature/livingroom");

        // Assert
        Assert.Single(manager.TopicTreeNodes);
        var sensor = manager.TopicTreeNodes.First();
        Assert.Equal("sensor", sensor.Name);

        Assert.Single(sensor.Children);
        var temperature = sensor.Children.First();
        Assert.Equal("temperature", temperature.Name);
        Assert.Equal("sensor/temperature", temperature.FullPath);

        Assert.Single(temperature.Children);
        var livingroom = temperature.Children.First();
        Assert.Equal("livingroom", livingroom.Name);
        Assert.Equal("sensor/temperature/livingroom", livingroom.FullPath);
        Assert.Equal(1, livingroom.MessageCount);
    }

    [Fact]
    public void UpdateOrCreateNode_WithExistingTopic_IncrementsCount()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temp");

        // Act
        manager.UpdateOrCreateNode("sensor/temp");

        // Assert
        var temp = manager.TopicTreeNodes.First().Children.First();
        Assert.Equal(2, temp.MessageCount);
    }

    [Fact]
    public void UpdateOrCreateNode_WithIncrementCountFalse_DoesNotIncrementCount()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNode("sensor/temp", incrementCount: false);

        // Assert
        var temp = manager.TopicTreeNodes.First().Children.First();
        Assert.Equal(0, temp.MessageCount);
    }

    [Fact]
    public void UpdateOrCreateNode_MaintainsSortedOrder()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act - Add in non-alphabetical order
        manager.UpdateOrCreateNode("sensor/zebra");
        manager.UpdateOrCreateNode("sensor/alpha");
        manager.UpdateOrCreateNode("sensor/middle");

        // Assert - Should be sorted
        var children = manager.TopicTreeNodes.First().Children;
        Assert.Equal("alpha", children[0].Name);
        Assert.Equal("middle", children[1].Name);
        Assert.Equal("zebra", children[2].Name);
    }

    [Fact]
    public void UpdateOrCreateNodeWithCount_IncrementsCountBySpecifiedAmount()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNodeWithCount("sensor/temp", 5);

        // Assert
        var temp = manager.TopicTreeNodes.First().Children.First();
        Assert.Equal(5, temp.MessageCount);
    }

    [Fact]
    public void UpdateOrCreateNodeWithCount_WithExistingNode_AddsToCount()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNodeWithCount("sensor/temp", 3);

        // Act
        manager.UpdateOrCreateNodeWithCount("sensor/temp", 2);

        // Assert
        var temp = manager.TopicTreeNodes.First().Children.First();
        Assert.Equal(5, temp.MessageCount);
    }

    [Fact]
    public void UpdateOrCreateNodeWithCount_WithZeroIncrement_DoesNothing()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNodeWithCount("sensor/temp", 0);

        // Assert
        Assert.Empty(manager.TopicTreeNodes);
    }

    [Fact]
    public void UpdateOrCreateNode_WithNullTopic_DoesNothing()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNode(null!);

        // Assert
        Assert.Empty(manager.TopicTreeNodes);
    }

    [Fact]
    public void UpdateOrCreateNode_WithEmptyTopic_DoesNothing()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        manager.UpdateOrCreateNode("");

        // Assert
        Assert.Empty(manager.TopicTreeNodes);
    }

    [Fact]
    public void ApplyTopicFilter_WithMatchingFilter_FiltersNodes()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature");
        manager.UpdateOrCreateNode("sensor/humidity");
        manager.UpdateOrCreateNode("device/status");

        // Act
        var matchCount = manager.ApplyTopicFilter("temp");

        // Assert
        Assert.True(manager.IsTopicFilterActive);
        Assert.Equal(1, matchCount);

        var sensor = manager.TopicTreeNodes.First(n => n.Name == "sensor");
        var temp = sensor.Children.First(n => n.Name == "temperature");
        var humidity = sensor.Children.First(n => n.Name == "humidity");

        Assert.True(temp.IsVisible);
        Assert.False(humidity.IsVisible);
    }

    [Fact]
    public void ApplyTopicFilter_ShowsParentNodesForMatchingChildren()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature/living");

        // Act
        manager.ApplyTopicFilter("living");

        // Assert
        var sensor = manager.TopicTreeNodes.First();
        var temperature = sensor.Children.First();
        var living = temperature.Children.First();

        // All parents should be visible because child matches
        Assert.True(sensor.IsVisible);
        Assert.True(temperature.IsVisible);
        Assert.True(living.IsVisible);
    }

    [Fact]
    public void ClearTopicFilter_RemovesFilter()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature");
        manager.UpdateOrCreateNode("device/status");
        manager.ApplyTopicFilter("temp");

        // Act
        manager.ClearTopicFilter();

        // Assert
        Assert.False(manager.IsTopicFilterActive);
        foreach (var node in manager.TopicTreeNodes)
        {
            Assert.True(node.IsVisible);
        }
    }

    [Fact]
    public void ApplyTopicFilter_WithNullFilter_ClearsFilter()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temp");
        manager.ApplyTopicFilter("temp");

        // Act
        var matchCount = manager.ApplyTopicFilter(null);

        // Assert
        Assert.False(manager.IsTopicFilterActive);
        Assert.Equal(1, matchCount); // Returns count of all nodes
    }

    [Fact]
    public void ExpandAllNodes_ExpandsAllNodes()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature/living");
        manager.UpdateOrCreateNode("sensor/humidity/bedroom");

        // Act
        manager.ExpandAllNodes();

        // Assert
        var sensor = manager.TopicTreeNodes.First();
        Assert.True(sensor.IsExpanded);
        foreach (var child in sensor.Children)
        {
            Assert.True(child.IsExpanded);
        }
    }

    [Fact]
    public void CollapseAllNodes_CollapsesAllNodes()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature/living");
        manager.ExpandAllNodes();

        // Act
        manager.CollapseAllNodes();

        // Assert
        var sensor = manager.TopicTreeNodes.First();
        Assert.False(sensor.IsExpanded);
        foreach (var child in sensor.Children)
        {
            Assert.False(child.IsExpanded);
        }
    }

    [Fact]
    public void FindTopicNode_WithExactMatch_ReturnsNode()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature/living");

        // Act
        var node = manager.FindTopicNode("sensor/temperature");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("temperature", node.Name);
        Assert.Equal("sensor/temperature", node.FullPath);
    }

    [Fact]
    public void FindTopicNode_WithNonExistentTopic_ReturnsNull()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature");

        // Act
        var node = manager.FindTopicNode("device/status");

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void FindTopicNode_WithNullTopic_ReturnsNull()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act
        var node = manager.FindTopicNode(null!);

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void FindTopicNode_WithTrailingSlash_NormalizesAndFinds()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature");

        // Act
        var node = manager.FindTopicNode("sensor/temperature/");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("temperature", node.Name);
    }

    [Fact]
    public void FindTopicNode_IsCaseInsensitive()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("Sensor/Temperature");

        // Act
        var node = manager.FindTopicNode("sensor/temperature");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("Temperature", node.Name);
    }

    [Fact]
    public void LoadInitialTopics_CreatesNodesWithoutIncrementingCount()
    {
        // Arrange
        var manager = new TopicTreeManager();
        var topics = new[] { "sensor/temp", "sensor/humidity", "device/status" };

        // Act
        manager.LoadInitialTopics(topics);

        // Assert
        Assert.Equal(2, manager.TopicTreeNodes.Count); // "sensor" and "device"
        var sensor = manager.TopicTreeNodes.First(n => n.Name == "sensor");
        Assert.Equal(2, sensor.Children.Count);

        // Counts should be 0 for initial load
        foreach (var child in sensor.Children)
        {
            Assert.Equal(0, child.MessageCount);
        }
    }

    [Fact]
    public void FilterChanged_EventRaisedOnApplyFilter()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temperature");

        string? receivedFilter = null;
        int receivedMatchCount = 0;
        bool receivedIsActive = false;

        manager.FilterChanged += (sender, args) =>
        {
            receivedFilter = args.Filter;
            receivedMatchCount = args.MatchCount;
            receivedIsActive = args.IsActive;
        };

        // Act
        manager.ApplyTopicFilter("temp");

        // Assert
        Assert.Equal("temp", receivedFilter);
        Assert.Equal(1, receivedMatchCount);
        Assert.True(receivedIsActive);
    }

    [Fact]
    public void FilterChanged_EventRaisedOnClearFilter()
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode("sensor/temp");
        manager.ApplyTopicFilter("temp");

        string? receivedFilter = "not-cleared";
        bool receivedIsActive = true;

        manager.FilterChanged += (sender, args) =>
        {
            receivedFilter = args.Filter;
            receivedIsActive = args.IsActive;
        };

        // Act
        manager.ClearTopicFilter();

        // Assert
        Assert.Null(receivedFilter);
        Assert.False(receivedIsActive);
    }

    [Fact]
    public void UpdateOrCreateNode_CreatesDeepHierarchy()
    {
        // Arrange
        var manager = new TopicTreeManager();

        // Act - Create a deep hierarchy in one call
        manager.UpdateOrCreateNode("sensor/room/temperature/value");

        // Assert - Verify structure
        var sensor = manager.TopicTreeNodes.First();
        Assert.Equal("sensor", sensor.Name);

        var room = sensor.Children.First();
        Assert.Equal("room", room.Name);

        var temperature = room.Children.First();
        Assert.Equal("temperature", temperature.Name);

        var value = temperature.Children.First();
        Assert.Equal("value", value.Name);
        Assert.Equal("sensor/room/temperature/value", value.FullPath);
    }

    [Theory]
    [InlineData("sensor/temperature/living", "sensor")]
    [InlineData("sensor/temperature/living", "temperature")]
    [InlineData("sensor/temperature/living", "living")]
    public void ApplyTopicFilter_WithFuzzyMatch_FindsNodes(string topic, string filter)
    {
        // Arrange
        var manager = new TopicTreeManager();
        manager.UpdateOrCreateNode(topic);

        // Act
        var matchCount = manager.ApplyTopicFilter(filter);

        // Assert
        Assert.True(matchCount > 0);
        Assert.True(manager.IsTopicFilterActive);
    }
}
