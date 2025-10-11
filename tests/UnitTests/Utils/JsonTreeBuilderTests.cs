using System.Text.Json;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Utils
{
    public class JsonTreeBuilderTests
    {
        [Fact]
        public void BuildTree_FlatJson_ReturnsRootOnly()
        {
            // Arrange
            var json = "{\"name\":\"value\"}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert
            Assert.NotNull(root);
            Assert.Equal(1, root.Depth);
            Assert.Equal("root", root.Key);
            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            Assert.Single(root.Children); // "name" property
            Assert.True(root.IsExpanded); // depth 1, expandable
        }

        [Fact]
        public void BuildTree_3LevelNested_AllExpanded()
        {
            // Arrange
            var json = "{\"level1\":{\"level2\":{\"level3\":{\"value\":\"deepest\"}}}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert - Root (depth 1)
            Assert.Equal(1, root.Depth);
            Assert.True(root.IsExpanded);

            // Assert - Level 1 child (depth 2)
            var level1 = root.Children[0];
            Assert.Equal("level1", level1.Key);
            Assert.Equal(2, level1.Depth);
            Assert.True(level1.IsExpanded);

            // Assert - Level 2 child (depth 3)
            var level2 = level1.Children[0];
            Assert.Equal("level2", level2.Key);
            Assert.Equal(3, level2.Depth);
            Assert.True(level2.IsExpanded);

            // Assert - Level 3 child (depth 4)
            var level3 = level2.Children[0];
            Assert.Equal("level3", level3.Key);
            Assert.Equal(4, level3.Depth);
            Assert.True(level3.IsExpanded);
        }

        [Fact]
        public void BuildTree_5LevelNested_AllExpanded()
        {
            // Arrange
            var json = "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"value\":\"level 5\"}}}}}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert - Traverse to depth 5 and verify all expanded
            var current = root;
            for (int expectedDepth = 1; expectedDepth <= 5; expectedDepth++)
            {
                Assert.Equal(expectedDepth, current.Depth);
                Assert.True(current.IsExpanded, $"Depth {expectedDepth} should be expanded");

                if (expectedDepth < 5)
                {
                    Assert.NotEmpty(current.Children);
                    current = current.Children[0];
                }
            }
        }

        [Fact]
        public void BuildTree_6LevelNested_OnlyFirst5Expanded()
        {
            // Arrange
            var json = "{\"l1\":{\"l2\":{\"l3\":{\"l4\":{\"l5\":{\"l6\":{\"value\":\"too deep\"}}}}}}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert - Depths 1-5 expanded
            var current = root;
            for (int expectedDepth = 1; expectedDepth <= 5; expectedDepth++)
            {
                Assert.Equal(expectedDepth, current.Depth);
                Assert.True(current.IsExpanded, $"Depth {expectedDepth} should be expanded");
                Assert.NotEmpty(current.Children);
                current = current.Children[0];
            }

            // Assert - Depth 6 collapsed
            Assert.Equal(6, current.Depth);
            Assert.False(current.IsExpanded, "Depth 6 should be collapsed");
        }

        [Fact]
        public void BuildTree_7LevelNested_Depth6And7Collapsed()
        {
            // Arrange
            var json = "{\"l1\":{\"l2\":{\"l3\":{\"l4\":{\"l5\":{\"l6\":{\"l7\":{\"value\":\"very deep\"}}}}}}}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert - Navigate to depth 6
            var current = root;
            for (int i = 1; i <= 5; i++)
            {
                current = current.Children[0];
            }

            // Assert - Depth 6 collapsed
            Assert.Equal(6, current.Depth);
            Assert.False(current.IsExpanded);

            // Assert - Depth 7 exists but collapsed
            Assert.NotEmpty(current.Children);
            var level7 = current.Children[0];
            Assert.Equal(7, level7.Depth);
            Assert.False(level7.IsExpanded);
        }

        [Fact]
        public void BuildTree_MixedTypes_CorrectExpansion()
        {
            // Arrange - Mix of objects and arrays
            var json = "{\"obj\":{\"arr\":[1,2,{\"nested\":\"value\"}]}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert - Root expanded
            Assert.True(root.IsExpanded);

            // Assert - "obj" property expanded (depth 2)
            var objNode = root.Children[0];
            Assert.Equal("obj", objNode.Key);
            Assert.Equal(JsonValueKind.Object, objNode.ValueKind);
            Assert.True(objNode.IsExpanded);

            // Assert - "arr" property expanded (depth 3)
            var arrNode = objNode.Children[0];
            Assert.Equal("arr", arrNode.Key);
            Assert.Equal(JsonValueKind.Array, arrNode.ValueKind);
            Assert.True(arrNode.IsExpanded);

            // Assert - Array items exist
            Assert.Equal(3, arrNode.Children.Count); // [1, 2, {...}]
        }

        [Fact]
        public void BuildTree_EmptyObject_NotExpandable()
        {
            // Arrange
            var json = "{\"empty\":{}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert
            var emptyNode = root.Children[0];
            Assert.Equal("empty", emptyNode.Key);
            Assert.Equal(JsonValueKind.Object, emptyNode.ValueKind);
            Assert.False(emptyNode.IsExpandable);
            Assert.Empty(emptyNode.Children);
        }

        [Fact]
        public void BuildTree_EmptyArray_NotExpandable()
        {
            // Arrange
            var json = "{\"emptyArr\":[]}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert
            var emptyArrNode = root.Children[0];
            Assert.Equal("emptyArr", emptyArrNode.Key);
            Assert.Equal(JsonValueKind.Array, emptyArrNode.ValueKind);
            Assert.False(emptyArrNode.IsExpandable);
            Assert.Empty(emptyArrNode.Children);
        }

        [Fact]
        public void BuildTree_ScalarValues_NotExpandable()
        {
            // Arrange
            var json = "{\"str\":\"text\",\"num\":42,\"bool\":true,\"null\":null}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert - All scalar children not expandable
            Assert.Equal(4, root.Children.Count);
            foreach (var child in root.Children)
            {
                Assert.False(child.IsExpandable);
                Assert.Empty(child.Children);
            }
        }

        [Fact]
        public void BuildTree_ParentChildRelationships_EstablishedCorrectly()
        {
            // Arrange
            var json = "{\"parent\":{\"child\":\"value\"}}";
            var document = JsonDocument.Parse(json);
            var builder = new CrowsNestMqtt.Utils.JsonTreeBuilder();

            // Act
            var root = builder.BuildTree(document);

            // Assert
            var parentNode = root.Children[0];
            Assert.Equal("parent", parentNode.Key);
            Assert.Equal(root, parentNode.Parent);

            var childNode = parentNode.Children[0];
            Assert.Equal("child", childNode.Key);
            Assert.Equal(parentNode, childNode.Parent);
        }
    }
}
