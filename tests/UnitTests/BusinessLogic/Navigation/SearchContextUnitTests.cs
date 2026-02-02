using System;
using System.Collections.Generic;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Navigation
{
    public class SearchContextUnitTests
    {
        private static TopicReference CreateTopicRef(string path) => 
            new(path, path, Guid.NewGuid());

        [Fact]
        public void Constructor_WithValidData_ShouldSucceed()
        {
            var matches = new[] { CreateTopicRef("test/topic") };
            var context = new SearchContext("test", matches);
            Assert.Equal("test", context.SearchTerm);
            Assert.Equal(1, context.TotalMatches);
            Assert.Equal(0, context.CurrentIndex);
        }

        [Fact]
        public void Constructor_WithNullSearchTerm_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new SearchContext(null!, Array.Empty<TopicReference>()));
        }

        [Fact]
        public void Constructor_WithEmptySearchTerm_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new SearchContext("", Array.Empty<TopicReference>()));
        }

        [Fact]
        public void Constructor_WithWhitespaceSearchTerm_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => 
                new SearchContext("   ", Array.Empty<TopicReference>()));
        }

        [Fact]
        public void Constructor_WithNullMatches_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => 
                new SearchContext("test", null!));
        }

        [Fact]
        public void Constructor_WithNoMatches_ShouldSetIndexToNegativeOne()
        {
            var context = new SearchContext("test", Array.Empty<TopicReference>());
            Assert.Equal(-1, context.CurrentIndex);
            Assert.False(context.HasMatches);
            Assert.False(context.IsActive);
        }

        [Fact]
        public void Constructor_WithMatches_ShouldSetIndexToZero()
        {
            var matches = new[] { CreateTopicRef("test/topic") };
            var context = new SearchContext("test", matches);
            Assert.Equal(0, context.CurrentIndex);
            Assert.True(context.HasMatches);
            Assert.True(context.IsActive);
        }

        [Fact]
        public void CurrentIndex_SetValidIndex_ShouldUpdateAndRaisePropertyChanged()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2"), CreateTopicRef("t3") };
            var context = new SearchContext("test", matches);
            var eventRaised = false;
            var propertyNames = new List<string>();
            context.PropertyChanged += (s, e) => { eventRaised = true; propertyNames.Add(e.PropertyName!); };
            context.CurrentIndex = 1;
            Assert.Equal(1, context.CurrentIndex);
            Assert.True(eventRaised);
            Assert.Contains("CurrentIndex", propertyNames);
        }

        [Fact]
        public void CurrentIndex_SetSameValue_ShouldNotRaisePropertyChanged()
        {
            var matches = new[] { CreateTopicRef("topic1") };
            var context = new SearchContext("test", matches);
            var eventCount = 0;
            context.PropertyChanged += (s, e) => eventCount++;
            context.CurrentIndex = 0;
            Assert.Equal(0, eventCount);
        }

        [Fact]
        public void CurrentIndex_SetOutOfRange_ShouldThrow()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2") };
            var context = new SearchContext("test", matches);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.CurrentIndex = 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => context.CurrentIndex = -2);
        }

        [Fact]
        public void MoveNext_WithMatches_ShouldAdvanceIndex()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2"), CreateTopicRef("t3") };
            var context = new SearchContext("test", matches);
            Assert.Equal(0, context.CurrentIndex);
            context.MoveNext();
            Assert.Equal(1, context.CurrentIndex);
            context.MoveNext();
            Assert.Equal(2, context.CurrentIndex);
        }

        [Fact]
        public void MoveNext_AtEnd_ShouldWrapToStart()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2") };
            var context = new SearchContext("test", matches);
            context.CurrentIndex = 1;
            context.MoveNext();
            Assert.Equal(0, context.CurrentIndex);
        }

        [Fact]
        public void MoveNext_WithNoMatches_ShouldBeNoOp()
        {
            var context = new SearchContext("test", Array.Empty<TopicReference>());
            context.MoveNext();
            Assert.Equal(-1, context.CurrentIndex);
        }

        [Fact]
        public void MovePrevious_WithMatches_ShouldDecrementIndex()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2"), CreateTopicRef("t3") };
            var context = new SearchContext("test", matches);
            context.CurrentIndex = 2;
            context.MovePrevious();
            Assert.Equal(1, context.CurrentIndex);
            context.MovePrevious();
            Assert.Equal(0, context.CurrentIndex);
        }

        [Fact]
        public void MovePrevious_AtStart_ShouldWrapToEnd()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2") };
            var context = new SearchContext("test", matches);
            context.MovePrevious();
            Assert.Equal(1, context.CurrentIndex);
        }

        [Fact]
        public void MovePrevious_WithNoMatches_ShouldBeNoOp()
        {
            var context = new SearchContext("test", Array.Empty<TopicReference>());
            context.MovePrevious();
            Assert.Equal(-1, context.CurrentIndex);
        }

        [Fact]
        public void GetCurrentMatch_WithValidIndex_ShouldReturnMatch()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2") };
            var context = new SearchContext("test", matches);
            context.CurrentIndex = 1;
            var match = context.GetCurrentMatch();
            Assert.NotNull(match);
            Assert.Equal("t2", match.TopicPath);
        }

        [Fact]
        public void GetCurrentMatch_WithNoMatches_ShouldReturnNull()
        {
            var context = new SearchContext("test", Array.Empty<TopicReference>());
            var match = context.GetCurrentMatch();
            Assert.Null(match);
        }

        [Fact]
        public void CreateEmpty_ShouldReturnContextWithNoMatches()
        {
            var context = SearchContext.CreateEmpty("test");
            Assert.Equal("test", context.SearchTerm);
            Assert.Equal(0, context.TotalMatches);
            Assert.Equal(-1, context.CurrentIndex);
            Assert.False(context.HasMatches);
            Assert.False(context.IsActive);
        }

        [Fact]
        public void PropertyChanged_OnCurrentIndexChange_ShouldRaiseIsActiveChanged()
        {
            var matches = new[] { CreateTopicRef("t1"), CreateTopicRef("t2") };
            var context = new SearchContext("test", matches);
            var propertyNames = new List<string>();
            context.PropertyChanged += (s, e) => propertyNames.Add(e.PropertyName!);
            context.CurrentIndex = 1;
            Assert.Contains("CurrentIndex", propertyNames);
            Assert.Contains("IsActive", propertyNames);
        }
    }
}
