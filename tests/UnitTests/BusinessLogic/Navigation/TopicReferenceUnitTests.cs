using System;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Navigation
{
    public class TopicReferenceUnitTests
    {
        [Fact]
        public void Constructor_WithValidData_ShouldSucceed()
        {
            var id = Guid.NewGuid();
            var topicRef = new TopicReference("test/topic", "Test Topic", id);
            Assert.Equal("test/topic", topicRef.TopicPath);
            Assert.Equal("Test Topic", topicRef.DisplayName);
            Assert.Equal(id, topicRef.TopicId);
        }

        [Fact]
        public void Constructor_WithNullTopicPath_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new TopicReference(null!, "display", Guid.NewGuid()));
        }

        [Fact]
        public void Constructor_WithEmptyTopicPath_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new TopicReference("", "display", Guid.NewGuid()));
        }

        [Fact]
        public void Constructor_WithWhitespaceTopicPath_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new TopicReference("   ", "display", Guid.NewGuid()));
        }

        [Fact]
        public void Constructor_WithNullDisplayName_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new TopicReference("path", null!, Guid.NewGuid()));
        }

        [Fact]
        public void Constructor_WithEmptyDisplayName_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new TopicReference("path", "", Guid.NewGuid()));
        }

        [Fact]
        public void Constructor_WithEmptyGuid_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new TopicReference("path", "display", Guid.Empty));
        }

        [Fact]
        public void Equals_WithSameTopicId_ShouldReturnTrue()
        {
            var id = Guid.NewGuid();
            var ref1 = new TopicReference("path1", "display1", id);
            var ref2 = new TopicReference("path2", "display2", id);
            Assert.True(ref1.Equals(ref2));
        }

        [Fact]
        public void Equals_WithDifferentTopicId_ShouldReturnFalse()
        {
            var ref1 = new TopicReference("path", "display", Guid.NewGuid());
            var ref2 = new TopicReference("path", "display", Guid.NewGuid());
            Assert.False(ref1.Equals(ref2));
        }

        [Fact]
        public void Equals_WithNull_ShouldReturnFalse()
        {
            var topicRef = new TopicReference("path", "display", Guid.NewGuid());
            Assert.False(topicRef.Equals(null));
        }

        [Fact]
        public void Equals_WithSameReference_ShouldReturnTrue()
        {
            var topicRef = new TopicReference("path", "display", Guid.NewGuid());
            Assert.True(topicRef.Equals(topicRef));
        }

        [Fact]
        public void EqualsObject_WithSameTopicId_ShouldReturnTrue()
        {
            var id = Guid.NewGuid();
            var ref1 = new TopicReference("path", "display", id);
            object ref2 = new TopicReference("path", "display", id);
            Assert.True(ref1.Equals(ref2));
        }

        [Fact]
        public void EqualsObject_WithNonTopicReference_ShouldReturnFalse()
        {
            var topicRef = new TopicReference("path", "display", Guid.NewGuid());
            Assert.False(topicRef.Equals("string"));
        }

        [Fact]
        public void GetHashCode_WithSameTopicId_ShouldBeEqual()
        {
            var id = Guid.NewGuid();
            var ref1 = new TopicReference("path1", "display1", id);
            var ref2 = new TopicReference("path2", "display2", id);
            Assert.Equal(ref1.GetHashCode(), ref2.GetHashCode());
        }

        [Fact]
        public void ToString_ShouldReturnTopicPath()
        {
            var topicRef = new TopicReference("test/topic", "display", Guid.NewGuid());
            Assert.Equal("test/topic", topicRef.ToString());
        }

        [Fact]
        public void OperatorEquals_WithSameTopicId_ShouldReturnTrue()
        {
            var id = Guid.NewGuid();
            var ref1 = new TopicReference("path", "display", id);
            var ref2 = new TopicReference("path", "display", id);
            Assert.True(ref1 == ref2);
        }

        [Fact]
        public void OperatorEquals_WithBothNull_ShouldReturnTrue()
        {
            TopicReference? ref1 = null;
            TopicReference? ref2 = null;
            Assert.True(ref1 == ref2);
        }

        [Fact]
        public void OperatorEquals_WithOneNull_ShouldReturnFalse()
        {
            var ref1 = new TopicReference("path", "display", Guid.NewGuid());
            TopicReference? ref2 = null;
            Assert.False(ref1 == ref2);
            Assert.False(ref2 == ref1);
        }

        [Fact]
        public void OperatorNotEquals_WithDifferentTopicId_ShouldReturnTrue()
        {
            var ref1 = new TopicReference("path", "display", Guid.NewGuid());
            var ref2 = new TopicReference("path", "display", Guid.NewGuid());
            Assert.True(ref1 != ref2);
        }

        [Fact]
        public void OperatorNotEquals_WithSameTopicId_ShouldReturnFalse()
        {
            var id = Guid.NewGuid();
            var ref1 = new TopicReference("path", "display", id);
            var ref2 = new TopicReference("path", "display", id);
            Assert.False(ref1 != ref2);
        }
    }
}
