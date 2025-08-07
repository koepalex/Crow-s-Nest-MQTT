using Xunit;
using CrowsNestMqtt.UI.ViewModels;

namespace UnitTests.ViewModels
{
    public class NodeViewModelTests
    {
        [Fact]
        public void Constructor_SetsNameAndParent()
        {
            var parent = new NodeViewModel("parent");
            var node = new NodeViewModel("child", parent);

            Assert.Equal("child", node.Name);
            Assert.Equal(parent, node.Parent);
        }

        [Fact]
        public void ParameterlessConstructor_SetsDefaults()
        {
            var node = new NodeViewModel();
            Assert.Equal("Default Node", node.Name);
            Assert.Null(node.Parent);
        }

        [Fact]
        public void Name_Property_RaisesChange()
        {
            var node = new NodeViewModel("n");
            bool raised = false;
            node.PropertyChanged += (s, e) => { if (e.PropertyName == "Name") raised = true; };
            node.Name = "new";
            Assert.True(raised);
            Assert.Equal("new", node.Name);
        }

        [Fact]
        public void MessageCount_Property_RaisesChange()
        {
            var node = new NodeViewModel("n");
            bool raised = false;
            node.PropertyChanged += (s, e) => { if (e.PropertyName == "MessageCount") raised = true; };
            node.MessageCount = 5;
            Assert.True(raised);
            Assert.Equal(5, node.MessageCount);
        }

        [Fact]
        public void IsExpanded_Property_RaisesChange()
        {
            var node = new NodeViewModel("n");
            bool raised = false;
            node.PropertyChanged += (s, e) => { if (e.PropertyName == "IsExpanded") raised = true; };
            node.IsExpanded = true;
            Assert.True(raised);
            Assert.True(node.IsExpanded);
        }

        [Fact]
        public void IsVisible_Property_RaisesChange()
        {
            var node = new NodeViewModel("n");
            bool raised = false;
            node.PropertyChanged += (s, e) => { if (e.PropertyName == "IsVisible") raised = true; };
            node.IsVisible = false;
            Assert.True(raised);
            Assert.False(node.IsVisible);
        }

        [Fact]
        public void IncrementMessageCount_IncrementsAndPropagates()
        {
            var parent = new NodeViewModel("parent");
            var node = new NodeViewModel("child", parent);

            node.IncrementMessageCount();

            Assert.Equal(1, node.MessageCount);
            Assert.Equal(1, parent.MessageCount);
        }
    }
}
