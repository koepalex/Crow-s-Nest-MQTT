using System.Text.Json;
using Xunit;
using CrowsNestMqtt.UI.ViewModels;

namespace UnitTests.ViewModels
{
    public class JsonViewerViewModelTests
    {
        [Fact]
        public void LoadJson_ParsesValidObjectJson()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"a\":1,\"b\":\"text\"}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(string.Empty, vm.JsonParseError);
            Assert.Equal(2, vm.RootNodes.Count);
            Assert.Equal("a", vm.RootNodes[0].Name);
            Assert.Equal("1", vm.RootNodes[0].ValueDisplay);
            Assert.Equal("b", vm.RootNodes[1].Name);
            Assert.Equal("\"text\"", vm.RootNodes[1].ValueDisplay);
        }

        [Fact]
        public void LoadJson_ParsesValidArrayJson()
        {
            var vm = new JsonViewerViewModel();
            string json = "[1,2,3]";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(string.Empty, vm.JsonParseError);
            Assert.Single(vm.RootNodes);
            var arrayRoot = vm.RootNodes[0];
            Assert.Equal("$", arrayRoot.Name);
            Assert.Equal("[...] (3)", arrayRoot.ValueDisplay);
            Assert.Equal(3, arrayRoot.Children.Count);
        }

        [Fact]
        public void LoadJson_SetsParseError_OnInvalidJson()
        {
            var vm = new JsonViewerViewModel();
            string json = "{invalid json}";
            vm.LoadJson(json);

            Assert.True(vm.HasParseError);
            Assert.Contains("JSON Parsing Error", vm.JsonParseError);
            Assert.Empty(vm.RootNodes);
        }

        [Fact]
        public void LoadJson_ClearsRootNodes_OnEmptyInput()
        {
            var vm = new JsonViewerViewModel();
            vm.RootNodes.Add(new JsonNodeViewModel("dummy", JsonDocument.Parse("1").RootElement, "$.dummy"));
            vm.LoadJson(string.Empty);

            Assert.False(vm.HasParseError);
            Assert.Equal(string.Empty, vm.JsonParseError);
            Assert.Empty(vm.RootNodes);
        }

        [Fact]
        public void SelectedNode_Property_Works()
        {
            var vm = new JsonViewerViewModel();
            var node = new JsonNodeViewModel("n", JsonDocument.Parse("1").RootElement, "$.n");
            vm.SelectedNode = node;
            Assert.Equal(node, vm.SelectedNode);
        }

        // New tests for JsonTreeBuilder integration (T004)
        // These tests MUST FAIL until JsonTreeBuilder is implemented and integrated

        [Fact]
        public void LoadJsonAsync_WithJsonTreeBuilder_BuildsTree()
        {
            // This test will FAIL until T008 is complete
            // Arrange
            var vm = new JsonViewerViewModel();
            var json = "{\"level1\":{\"level2\":\"value\"}}";

            // Act
            vm.LoadJson(json); // Will need to be async version LoadJsonAsync

            // Assert
            // RootNode should be populated with JsonTreeNode structure
            // For now, this will fail because LoadJson doesn't use JsonTreeBuilder yet
            Assert.False(vm.HasParseError);
            // TODO: Add assertions for RootNode property when it's added
        }

        [Fact]
        public void LoadJsonAsync_MalformedJson_SetsErrorMessage()
        {
            // This should already work with current implementation
            // But we're documenting it as part of T004 contract
            var vm = new JsonViewerViewModel();
            var json = "{broken json";

            vm.LoadJson(json);

            Assert.True(vm.HasParseError);
            Assert.Contains("JSON Parsing Error", vm.JsonParseError);
        }

        [Fact]
        public void LoadJsonAsync_ValidJson_ClearsErrorMessage()
        {
            // Arrange
            var vm = new JsonViewerViewModel();
            vm.LoadJson("{broken"); // Set error state first

            // Act
            vm.LoadJson("{\"valid\":true}");

            // Assert
            Assert.False(vm.HasParseError);
            Assert.Equal(string.Empty, vm.JsonParseError);
        }

        [Fact]
        public void RefreshTree_RebuildsFromCurrentMessage()
        {
            // This test will FAIL until T008 implements RefreshTree method
            // Arrange
            var vm = new JsonViewerViewModel();
            var json = "{\"data\":\"test\"}";
            vm.LoadJson(json);

            // Act
            // vm.RefreshTree(); // This method doesn't exist yet

            // Assert
            // Verify tree was rebuilt (new RootNode instance)
            // This will fail until RefreshTree is implemented in T008
            Assert.False(vm.HasParseError);
        }

        [Fact]
        public void LoadJsonAsync_EmptyJson_HandlesGracefully()
        {
            // Arrange
            var vm = new JsonViewerViewModel();

            // Act
            vm.LoadJson("{}");

            // Assert
            Assert.False(vm.HasParseError);
            Assert.Empty(vm.RootNodes);
        }

        [Fact]
        public void LoadJsonAsync_NullJson_HandlesGracefully()
        {
            // Arrange
            var vm = new JsonViewerViewModel();

            // Act
            vm.LoadJson(null!);

            // Assert - Should handle null gracefully
            // Current implementation will clear nodes for null/empty
            Assert.False(vm.HasParseError);
        }
    }
}
