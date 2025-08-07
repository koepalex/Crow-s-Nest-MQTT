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
    }
}
