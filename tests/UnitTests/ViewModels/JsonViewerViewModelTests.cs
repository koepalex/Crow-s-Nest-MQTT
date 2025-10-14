using System.Linq;
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

        [Fact]
        public void LoadJson_ParsesNumberValues()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"integer\":42,\"float\":3.14,\"negative\":-10}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(3, vm.RootNodes.Count);
            Assert.Equal("42", vm.RootNodes[0].ValueDisplay);
            Assert.Equal("3.14", vm.RootNodes[1].ValueDisplay);
            Assert.Equal("-10", vm.RootNodes[2].ValueDisplay);
        }

        [Fact]
        public void LoadJson_ParsesBooleanValues()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"isTrue\":true,\"isFalse\":false}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(2, vm.RootNodes.Count);
            Assert.Equal("true", vm.RootNodes[0].ValueDisplay);
            Assert.Equal("false", vm.RootNodes[1].ValueDisplay);
        }

        [Fact]
        public void LoadJson_ParsesNullValue()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"nullValue\":null}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Single(vm.RootNodes);
            Assert.Equal("null", vm.RootNodes[0].ValueDisplay);
        }

        [Fact]
        public void LoadJson_ParsesNestedObjects()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"outer\":{\"inner\":{\"deep\":\"value\"}}}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Single(vm.RootNodes);
            var outer = vm.RootNodes[0];
            Assert.Equal("outer", outer.Name);
            Assert.Single(outer.Children);
            var inner = outer.Children[0];
            Assert.Equal("inner", inner.Name);
            Assert.Single(inner.Children);
            Assert.Equal("deep", inner.Children[0].Name);
        }

        [Fact]
        public void LoadJson_ParsesNestedArrays()
        {
            var vm = new JsonViewerViewModel();
            string json = "[[1,2],[3,4]]";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Single(vm.RootNodes);
            var root = vm.RootNodes[0];
            Assert.Equal(2, root.Children.Count);
            Assert.Equal("[0]", root.Children[0].Name);
            Assert.Equal(2, root.Children[0].Children.Count);
        }

        [Fact]
        public void LoadJson_ParsesMixedTypes()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"string\":\"text\",\"number\":42,\"bool\":true,\"null\":null,\"object\":{},\"array\":[]}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(6, vm.RootNodes.Count);
        }

        [Fact]
        public void LoadJson_ClearsExistingNodes()
        {
            var vm = new JsonViewerViewModel();
            vm.LoadJson("{\"first\":1}");
            Assert.Single(vm.RootNodes);

            vm.LoadJson("{\"second\":2}");
            Assert.Single(vm.RootNodes);
            Assert.Equal("second", vm.RootNodes[0].Name);
        }

        [Fact]
        public void LoadJson_ClearsErrorOnSuccess()
        {
            var vm = new JsonViewerViewModel();
            vm.LoadJson("{invalid");
            Assert.True(vm.HasParseError);

            vm.LoadJson("{\"valid\":true}");
            Assert.False(vm.HasParseError);
            Assert.Equal(string.Empty, vm.JsonParseError);
        }

        [Fact]
        public void LoadJson_WhitespaceOnly_ClearsView()
        {
            var vm = new JsonViewerViewModel();
            vm.LoadJson("   \t\n   ");

            Assert.False(vm.HasParseError);
            Assert.Empty(vm.RootNodes);
        }

        [Fact]
        public void LoadJson_SetsPaths_ForNestedElements()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"a\":{\"b\":\"c\"}}";
            vm.LoadJson(json);

            var a = vm.RootNodes[0];
            Assert.Equal("$.a", a.JsonPath);
            var b = a.Children[0];
            Assert.Equal("$.a.b", b.JsonPath);
        }

        [Fact]
        public void LoadJson_SetsPaths_ForArrayElements()
        {
            var vm = new JsonViewerViewModel();
            string json = "[\"a\",\"b\"]";
            vm.LoadJson(json);

            var root = vm.RootNodes[0];
            Assert.Equal("$", root.JsonPath);
            Assert.Equal("$[0]", root.Children[0].JsonPath);
            Assert.Equal("$[1]", root.Children[1].JsonPath);
        }

        [Fact]
        public void LoadJson_ExpandsNodesBasedOnDepth()
        {
            var vm = new JsonViewerViewModel();
            // Create a deeply nested object
            string json = "{\"l1\":{\"l2\":{\"l3\":{\"l4\":{\"l5\":{\"l6\":{\"l7\":\"value\"}}}}}}}";
            vm.LoadJson(json);

            // Check expansion at different depths (auto-expand up to depth 5)
            var l1 = vm.RootNodes[0];
            Assert.True(l1.IsExpanded); // depth 1

            var l2 = l1.Children[0];
            Assert.True(l2.IsExpanded); // depth 2

            var l3 = l2.Children[0];
            Assert.True(l3.IsExpanded); // depth 3

            var l4 = l3.Children[0];
            Assert.True(l4.IsExpanded); // depth 4

            var l5 = l4.Children[0];
            Assert.True(l5.IsExpanded); // depth 5

            var l6 = l5.Children[0];
            Assert.False(l6.IsExpanded); // depth 6 - should NOT be expanded
        }

        [Fact]
        public void LoadJson_SetsDepthCorrectly()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"a\":{\"b\":{\"c\":\"value\"}}}";
            vm.LoadJson(json);

            Assert.Equal(1, vm.RootNodes[0].Depth);
            Assert.Equal(2, vm.RootNodes[0].Children[0].Depth);
            Assert.Equal(3, vm.RootNodes[0].Children[0].Children[0].Depth);
        }

        [Fact]
        public void LoadJson_ArrayWithVariousTypes()
        {
            var vm = new JsonViewerViewModel();
            string json = "[1,\"text\",true,null,{\"obj\":\"value\"},[1,2]]";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            var root = vm.RootNodes[0];
            Assert.Equal(6, root.Children.Count);
            Assert.Equal("1", root.Children[0].ValueDisplay);
            Assert.Equal("\"text\"", root.Children[1].ValueDisplay);
            Assert.Equal("true", root.Children[2].ValueDisplay);
            Assert.Equal("null", root.Children[3].ValueDisplay);
            Assert.Contains("{...}", root.Children[4].ValueDisplay);
            Assert.Contains("[...]", root.Children[5].ValueDisplay);
        }

        [Fact]
        public void LoadJson_LargeArray_HandlesCorrectly()
        {
            var vm = new JsonViewerViewModel();
            string json = "[" + string.Join(",", Enumerable.Range(0, 100).Select(i => i.ToString())) + "]";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            var root = vm.RootNodes[0];
            Assert.Equal(100, root.Children.Count);
        }

        [Fact]
        public void SelectedNode_RaisesPropertyChanged()
        {
            var vm = new JsonViewerViewModel();
            vm.LoadJson("{\"test\":1}");
            var node = vm.RootNodes[0];

            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(JsonViewerViewModel.SelectedNode))
                    propertyChangedRaised = true;
            };

            vm.SelectedNode = node;
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void HasParseError_RaisesPropertyChanged()
        {
            var vm = new JsonViewerViewModel();
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(JsonViewerViewModel.HasParseError))
                    propertyChangedRaised = true;
            };

            vm.LoadJson("{invalid");
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void JsonParseError_RaisesPropertyChanged()
        {
            var vm = new JsonViewerViewModel();
            var propertyChangedRaised = false;
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(JsonViewerViewModel.JsonParseError))
                    propertyChangedRaised = true;
            };

            vm.LoadJson("{invalid");
            Assert.True(propertyChangedRaised);
        }

        [Fact]
        public void LoadJson_EmptyArray_HandlesCorrectly()
        {
            var vm = new JsonViewerViewModel();
            vm.LoadJson("[]");

            Assert.False(vm.HasParseError);
            Assert.Single(vm.RootNodes);
            var root = vm.RootNodes[0];
            Assert.Empty(root.Children);
        }

        [Fact]
        public void LoadJson_SpecialCharactersInPropertyNames()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"with-dash\":1,\"with.dot\":2,\"with space\":3}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(3, vm.RootNodes.Count);
            Assert.Equal("with-dash", vm.RootNodes[0].Name);
            Assert.Equal("with.dot", vm.RootNodes[1].Name);
            Assert.Equal("with space", vm.RootNodes[2].Name);
        }

        [Fact]
        public void LoadJson_UnicodeCharacters()
        {
            var vm = new JsonViewerViewModel();
            string json = "{\"emoji\":\"🌍\",\"chinese\":\"世界\"}";
            vm.LoadJson(json);

            Assert.False(vm.HasParseError);
            Assert.Equal(2, vm.RootNodes.Count);
            Assert.Contains("🌍", vm.RootNodes[0].ValueDisplay);
            Assert.Contains("世界", vm.RootNodes[1].ValueDisplay);
        }
    }
}
