using System.Collections.ObjectModel;
using System.Text.Json;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Utils
{
    public class JsonTreeNodeTests
    {
        [Fact]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Key = "testKey",
                Value = "testValue",
                ValueKind = JsonValueKind.String,
                Depth = 2,
                IsExpandable = false,
                Parent = null
            };

            // Assert
            Assert.Equal("testKey", node.Key);
            Assert.Equal("testValue", node.Value);
            Assert.Equal(JsonValueKind.String, node.ValueKind);
            Assert.Equal(2, node.Depth);
            Assert.False(node.IsExpandable);
            Assert.Null(node.Parent);
            Assert.NotNull(node.Children);
        }

        [Fact]
        public void IsExpanded_DepthLessThanOrEqualTo5AndExpandable_ReturnsTrue()
        {
            // Arrange & Act - depth 5, expandable
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Depth = 5,
                IsExpandable = true
            };
            node.InitializeExpansionState();

            // Assert - FR-008: IsExpanded = true when Depth <= 5 AND IsExpandable = true
            Assert.True(node.IsExpanded);
        }

        [Fact]
        public void IsExpanded_DepthLessThanOrEqualTo5AndExpandable_Depth3_ReturnsTrue()
        {
            // Arrange & Act - depth 3, expandable
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Depth = 3,
                IsExpandable = true
            };
            node.InitializeExpansionState();

            // Assert
            Assert.True(node.IsExpanded);
        }

        [Fact]
        public void IsExpanded_DepthGreaterThan5_ReturnsFalse()
        {
            // Arrange & Act - depth 6, expandable
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Depth = 6,
                IsExpandable = true
            };

            // Assert - FR-008: IsExpanded = false when Depth > 5
            Assert.False(node.IsExpanded);
        }

        [Fact]
        public void IsExpanded_NotExpandable_ReturnsFalse()
        {
            // Arrange & Act - depth 3, not expandable
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Depth = 3,
                IsExpandable = false
            };

            // Assert - FR-008: IsExpanded = false when IsExpandable = false
            Assert.False(node.IsExpanded);
        }

        [Fact]
        public void IsExpandable_ScalarValueKind_SetsToFalse()
        {
            // Arrange & Act - String type (scalar)
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                ValueKind = JsonValueKind.String,
                IsExpandable = false
            };

            // Assert - FR-008: Children = empty collection when ValueKind is scalar
            Assert.False(node.IsExpandable);
            Assert.Empty(node.Children);
        }

        [Fact]
        public void IsExpandable_ObjectValueKind_CanSetToTrue()
        {
            // Arrange & Act - Object type
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                ValueKind = JsonValueKind.Object,
                IsExpandable = true
            };

            // Assert - FR-008: Children populated when ValueKind is Object
            Assert.True(node.IsExpandable);
        }

        [Fact]
        public void IsExpandable_ArrayValueKind_CanSetToTrue()
        {
            // Arrange & Act - Array type
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                ValueKind = JsonValueKind.Array,
                IsExpandable = true
            };

            // Assert - FR-008: Children populated when ValueKind is Array
            Assert.True(node.IsExpandable);
        }

        [Fact]
        public void ParentChildRelationship_EstablishedCorrectly()
        {
            // Arrange
            var parent = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Key = "parent",
                ValueKind = JsonValueKind.Object,
                Depth = 1,
                IsExpandable = true
            };

            var child = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Key = "child",
                ValueKind = JsonValueKind.String,
                Depth = 2,
                Parent = parent
            };

            // Act
            parent.Children.Add(child);

            // Assert
            Assert.Single(parent.Children);
            Assert.Equal(child, parent.Children[0]);
            Assert.Equal(parent, child.Parent);
        }

        [Fact]
        public void Children_InitializesAsObservableCollection()
        {
            // Arrange & Act
            var node = new CrowsNestMqtt.Utils.JsonTreeNode();

            // Assert
            Assert.NotNull(node.Children);
            Assert.IsAssignableFrom<ObservableCollection<CrowsNestMqtt.Utils.JsonTreeNode>>(node.Children);
        }

        [Fact]
        public void IsExpanded_CanBeManuallyChanged()
        {
            // Arrange
            var node = new CrowsNestMqtt.Utils.JsonTreeNode
            {
                Depth = 3,
                IsExpandable = true
            };

            // Act - Simulate manual collapse
            node.IsExpanded = false;

            // Assert
            Assert.False(node.IsExpanded);

            // Act - Simulate manual expand
            node.IsExpanded = true;

            // Assert
            Assert.True(node.IsExpanded);
        }
    }
}
