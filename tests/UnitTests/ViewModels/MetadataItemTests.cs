using Xunit;
using CrowsNestMqtt.UI.ViewModels;

namespace UnitTests.ViewModels
{
    public class MetadataItemTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var item = new MetadataItem("foo", "bar");
            Assert.Equal("foo", item.Key);
            Assert.Equal("bar", item.Value);
        }

        [Fact]
        public void Equality_Works()
        {
            var a = new MetadataItem("k", "v");
            var b = new MetadataItem("k", "v");
            var c = new MetadataItem("k", "other");

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        [Fact]
        public void ToString_ContainsKeyAndValue()
        {
            var item = new MetadataItem("k", "v");
            var str = item.ToString();
            Assert.Contains("k", str);
            Assert.Contains("v", str);
        }
    }
}
