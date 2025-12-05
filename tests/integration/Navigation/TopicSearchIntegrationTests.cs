/// <summary>
/// Integration tests for topic search functionality.
/// Tests TopicSearchService with realistic topic data.
/// Validates FR-001 through FR-007.
/// </summary>
namespace CrowsNestMQTT.Tests.Integration.Navigation;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

public class TopicSearchIntegrationTests
{
    [Fact]
    public void ExecuteSearch_WithMultipleTopics_FindsMatchingTopics()
    {
        // Arrange - FR-001: Search mechanism triggered by /[term] command
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act - Search for "sensor"
        var result = service.ExecuteSearch("sensor");

        // Assert - FR-002, FR-003: Case-insensitive substring matching
        Assert.NotNull(result);
        Assert.True(result.HasMatches);
        Assert.Equal(2, result.TotalMatches); // sensor/temperature and sensor/humidity
        Assert.Equal("sensor", result.SearchTerm);

        // FR-004: Auto-select first matching topic
        Assert.Equal(0, result.CurrentIndex);
        Assert.NotNull(result.GetCurrentMatch());
        Assert.Contains("sensor", result.GetCurrentMatch()!.TopicPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExecuteSearch_NoMatches_ReturnsEmptySearchContext()
    {
        // Arrange
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act - Search for non-existent topic
        var result = service.ExecuteSearch("nonexistent");

        // Assert - FR-007: Feedback when no matches found
        Assert.NotNull(result);
        Assert.False(result.HasMatches);
        Assert.Equal(0, result.TotalMatches);
        Assert.Equal("nonexistent", result.SearchTerm);
        Assert.Equal(-1, result.CurrentIndex);
        Assert.Null(result.GetCurrentMatch());
    }

    [Fact]
    public void ExecuteSearch_CaseInsensitive_MatchesAllVariations()
    {
        // Arrange - FR-002: Case-insensitive partial matching
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act - Search with different casing
        var resultLower = service.ExecuteSearch("sensor");
        var resultUpper = service.ExecuteSearch("SENSOR");
        var resultMixed = service.ExecuteSearch("SeNsOr");

        // Assert - All should find the same matches
        Assert.Equal(resultLower.TotalMatches, resultUpper.TotalMatches);
        Assert.Equal(resultLower.TotalMatches, resultMixed.TotalMatches);
        Assert.Equal(2, resultLower.TotalMatches);
    }

    [Fact]
    public void ExecuteSearch_SubstringMatch_FindsPartialMatches()
    {
        // Arrange - FR-003: Substring matching within topic names
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act - Search for substring "temp"
        var result = service.ExecuteSearch("temp");

        // Assert - Should find "sensor/temperature"
        Assert.True(result.HasMatches);
        Assert.Single(result.Matches);
        Assert.Contains("temperature", result.Matches[0].TopicPath);
    }

    [Fact]
    public void ExecuteSearch_UpdatesActiveSearchContext()
    {
        // Arrange
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act
        var result = service.ExecuteSearch("sensor");

        // Assert - Service tracks active search context
        Assert.NotNull(service.ActiveSearchContext);
        Assert.Same(result, service.ActiveSearchContext);
    }

    [Fact]
    public void ClearSearch_RemovesActiveSearchContext()
    {
        // Arrange
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);
        service.ExecuteSearch("sensor");

        // Act
        service.ClearSearch();

        // Assert
        Assert.Null(service.ActiveSearchContext);
    }

    [Fact]
    public void ExecuteSearch_PreservesSearchTermCasing()
    {
        // Arrange
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act - Search with specific casing
        var result = service.ExecuteSearch("SeNsOr");

        // Assert - Search term casing preserved for display
        Assert.Equal("SeNsOr", result.SearchTerm);
    }

    [Fact]
    public void ExecuteSearch_EmptyTerm_ThrowsArgumentException()
    {
        // Arrange
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ExecuteSearch(""));
        Assert.Throws<ArgumentException>(() => service.ExecuteSearch("   "));
    }

    [Fact]
    public void ExecuteSearch_ReplacesExistingSearch()
    {
        // Arrange
        var topics = CreateSampleTopics();
        var service = new TopicSearchService(() => topics);
        var firstSearch = service.ExecuteSearch("sensor");

        // Act - Execute new search
        var secondSearch = service.ExecuteSearch("device");

        // Assert - Active context updated to new search
        Assert.NotSame(firstSearch, secondSearch);
        Assert.Same(secondSearch, service.ActiveSearchContext);
        Assert.Equal("device", service.ActiveSearchContext.SearchTerm);
    }

    [Fact]
    public void ExecuteSearch_WithDynamicTopics_ReflectsCurrentState()
    {
        // Arrange - Simulate dynamic topic list
        var topicList = CreateSampleTopics().ToList();
        var service = new TopicSearchService(() => topicList);

        // Act - Initial search
        var result1 = service.ExecuteSearch("sensor");
        Assert.Equal(2, result1.TotalMatches);

        // Add new sensor topic
        topicList.Add(new TopicReference("sensor/pressure", "sensor/pressure", Guid.NewGuid()));

        // Execute search again
        var result2 = service.ExecuteSearch("sensor");

        // Assert - New search reflects updated topic list
        Assert.Equal(3, result2.TotalMatches);
    }

    /// <summary>
    /// Creates sample topics for testing.
    /// </summary>
    private static List<TopicReference> CreateSampleTopics()
    {
        return new List<TopicReference>
        {
            new TopicReference("sensor/temperature", "Temperature Sensor", Guid.NewGuid()),
            new TopicReference("sensor/humidity", "Humidity Sensor", Guid.NewGuid()),
            new TopicReference("device/status", "Device Status", Guid.NewGuid()),
            new TopicReference("device/control", "Device Control", Guid.NewGuid()),
            new TopicReference("system/health", "System Health", Guid.NewGuid())
        };
    }
}
