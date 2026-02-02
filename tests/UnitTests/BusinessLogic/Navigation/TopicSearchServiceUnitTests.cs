using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Navigation
{
    public class TopicSearchServiceUnitTests
    {
        private static TopicReference CreateTopicRef(string path) => 
            new(path, path, Guid.NewGuid());

        [Fact]
        public void Constructor_WithValidProvider_ShouldSucceed()
        {
            var service = new TopicSearchService(() => Array.Empty<TopicReference>());
            Assert.NotNull(service);
        }

        [Fact]
        public void Constructor_WithNullProvider_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new TopicSearchService(null!));
        }

        [Fact]
        public void ActiveSearchContext_Initially_ShouldBeNull()
        {
            var service = new TopicSearchService(() => Array.Empty<TopicReference>());
            Assert.Null(service.ActiveSearchContext);
        }

        [Fact]
        public void ExecuteSearch_WithValidTerm_ShouldReturnContext()
        {
            var topics = new[] { CreateTopicRef("test/topic") };
            var service = new TopicSearchService(() => topics);
            
            var context = service.ExecuteSearch("test");
            
            Assert.NotNull(context);
            Assert.Equal("test", context.SearchTerm);
        }

        [Fact]
        public void ExecuteSearch_WithNullSearchTerm_ShouldThrow()
        {
            var service = new TopicSearchService(() => Array.Empty<TopicReference>());
            Assert.Throws<ArgumentException>(() => service.ExecuteSearch(null!));
        }

        [Fact]
        public void ExecuteSearch_WithEmptySearchTerm_ShouldThrow()
        {
            var service = new TopicSearchService(() => Array.Empty<TopicReference>());
            Assert.Throws<ArgumentException>(() => service.ExecuteSearch(""));
        }

        [Fact]
        public void ExecuteSearch_WithWhitespaceSearchTerm_ShouldThrow()
        {
            var service = new TopicSearchService(() => Array.Empty<TopicReference>());
            Assert.Throws<ArgumentException>(() => service.ExecuteSearch("   "));
        }

        [Fact]
        public void ExecuteSearch_CaseInsensitive_ShouldFindMatches()
        {
            var topics = new[] 
            { 
                CreateTopicRef("sensor/TEMPERATURE"), 
                CreateTopicRef("sensor/humidity") 
            };
            var service = new TopicSearchService(() => topics);
            
            var context = service.ExecuteSearch("temperature");
            
            Assert.Equal(1, context.TotalMatches);
            Assert.Equal("sensor/TEMPERATURE", context.Matches[0].TopicPath);
        }

        [Fact]
        public void ExecuteSearch_SubstringMatch_ShouldFindMatches()
        {
            var topics = new[] 
            { 
                CreateTopicRef("sensor/temperature/bedroom"),
                CreateTopicRef("sensor/temperature/kitchen"),
                CreateTopicRef("sensor/humidity") 
            };
            var service = new TopicSearchService(() => topics);
            
            var context = service.ExecuteSearch("temp");
            
            Assert.Equal(2, context.TotalMatches);
        }

        [Fact]
        public void ExecuteSearch_NoMatches_ShouldReturnEmptyContext()
        {
            var topics = new[] { CreateTopicRef("sensor/temperature") };
            var service = new TopicSearchService(() => topics);
            
            var context = service.ExecuteSearch("notfound");
            
            Assert.Equal(0, context.TotalMatches);
            Assert.False(context.HasMatches);
        }

        [Fact]
        public void ExecuteSearch_ShouldUpdateActiveSearchContext()
        {
            var topics = new[] { CreateTopicRef("test/topic") };
            var service = new TopicSearchService(() => topics);
            
            var context = service.ExecuteSearch("test");
            
            Assert.Same(context, service.ActiveSearchContext);
        }

        [Fact]
        public void ExecuteSearch_MultipleTimes_ShouldUpdateActiveSearchContext()
        {
            var topics = new[] { CreateTopicRef("test/topic"), CreateTopicRef("other/topic") };
            var service = new TopicSearchService(() => topics);
            
            var context1 = service.ExecuteSearch("test");
            var context2 = service.ExecuteSearch("other");
            
            Assert.Same(context2, service.ActiveSearchContext);
            Assert.NotSame(context1, context2);
        }

        [Fact]
        public void ClearSearch_ShouldSetActiveSearchContextToNull()
        {
            var topics = new[] { CreateTopicRef("test/topic") };
            var service = new TopicSearchService(() => topics);
            
            service.ExecuteSearch("test");
            service.ClearSearch();
            
            Assert.Null(service.ActiveSearchContext);
        }

        [Fact]
        public void ClearSearch_WhenNoActiveSearch_ShouldBeNoOp()
        {
            var service = new TopicSearchService(() => Array.Empty<TopicReference>());
            
            service.ClearSearch();
            
            Assert.Null(service.ActiveSearchContext);
        }

        [Fact]
        public void ExecuteSearch_WithDynamicProvider_ShouldUseLatestTopics()
        {
            var topicsList = new List<TopicReference> { CreateTopicRef("topic1") };
            var service = new TopicSearchService(() => topicsList);
            
            var context1 = service.ExecuteSearch("topic");
            Assert.Equal(1, context1.TotalMatches);
            
            topicsList.Add(CreateTopicRef("topic2"));
            
            var context2 = service.ExecuteSearch("topic");
            Assert.Equal(2, context2.TotalMatches);
        }

        [Fact]
        public void ExecuteSearch_WithSpecialCharacters_ShouldFindMatches()
        {
            var topics = new[] { CreateTopicRef("sensor/temp-01"), CreateTopicRef("sensor/temp_02") };
            var service = new TopicSearchService(() => topics);
            
            var context = service.ExecuteSearch("temp-");
            
            Assert.Equal(1, context.TotalMatches);
        }
    }
}
