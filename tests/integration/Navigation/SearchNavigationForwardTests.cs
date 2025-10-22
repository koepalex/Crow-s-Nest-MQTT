/// <summary>
/// Integration tests for forward navigation through search results.
/// Tests 'n' key behavior with wrap-around.
/// Validates FR-008, FR-010, FR-023.
/// </summary>
namespace CrowsNestMQTT.Tests.Integration.Navigation;

using System;
using System.Collections.Generic;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

public class SearchNavigationForwardTests
{
    [Fact]
    public void MoveNext_AdvancesToSecondMatch()
    {
        // Arrange - FR-008: Navigate next search result with 'n'
        var context = CreateSearchContextWithMatches("sensor", 3);
        Assert.Equal(0, context.CurrentIndex); // Start at first

        // Act - Simulate 'n' key press
        context.MoveNext();

        // Assert
        Assert.Equal(1, context.CurrentIndex);
        Assert.Equal("sensor/humidity", context.GetCurrentMatch()?.TopicPath);
    }

    [Fact]
    public void MoveNext_AtLastMatch_WrapsToFirst()
    {
        // Arrange - FR-010: Wrap-around navigation
        var context = CreateSearchContextWithMatches("sensor", 3);

        // Move to last match
        context.MoveNext(); // index 1
        context.MoveNext(); // index 2 (last)
        Assert.Equal(2, context.CurrentIndex);

        // Act - Navigate from last match
        context.MoveNext();

        // Assert - Should wrap to first
        Assert.Equal(0, context.CurrentIndex);
        Assert.Equal("sensor/temperature", context.GetCurrentMatch()?.TopicPath);
    }

    [Fact]
    public void MoveNext_WithSingleMatch_StaysAtSameMatch()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("pressure", 1);
        Assert.Equal(0, context.CurrentIndex);

        // Act
        context.MoveNext();

        // Assert - Wraps back to same (only) match
        Assert.Equal(0, context.CurrentIndex);
    }

    [Fact]
    public void MoveNext_MultipleSequentialCalls_NavigatesCorrectly()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("sensor", 3);

        // Act - Navigate through all matches and wrap around
        context.MoveNext(); // 0 -> 1
        Assert.Equal(1, context.CurrentIndex);

        context.MoveNext(); // 1 -> 2
        Assert.Equal(2, context.CurrentIndex);

        context.MoveNext(); // 2 -> 0 (wrap)
        Assert.Equal(0, context.CurrentIndex);

        context.MoveNext(); // 0 -> 1
        Assert.Equal(1, context.CurrentIndex);
    }

    [Fact]
    public void MoveNext_FiresPropertyChangedForCurrentIndex()
    {
        // Arrange - FR-023: Update position indicator during navigation
        var context = CreateSearchContextWithMatches("sensor", 3);
        bool propertyChanged = false;

        context.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(SearchContext.CurrentIndex))
            {
                propertyChanged = true;
            }
        };

        // Act
        context.MoveNext();

        // Assert
        Assert.True(propertyChanged, "PropertyChanged should be raised for CurrentIndex");
    }

    [Fact]
    public void MoveNext_GetCurrentMatch_ReturnsCorrectMatch()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("sensor", 3);

        // Act & Assert - Verify GetCurrentMatch returns correct topic at each position
        Assert.Equal("sensor/temperature", context.GetCurrentMatch()?.TopicPath);

        context.MoveNext();
        Assert.Equal("sensor/humidity", context.GetCurrentMatch()?.TopicPath);

        context.MoveNext();
        Assert.Equal("sensor/pressure", context.GetCurrentMatch()?.TopicPath);

        context.MoveNext(); // Wrap
        Assert.Equal("sensor/temperature", context.GetCurrentMatch()?.TopicPath);
    }

    [Fact]
    public void NavigateSearchNext_WithKeyboardNavigationService_UpdatesContext()
    {
        // Arrange - Integration with KeyboardNavigationService
        var searchService = CreateSearchServiceWithMatches("sensor", 3);
        var messageNav = new MessageNavigationState();
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false // Shortcuts not suppressed
        );

        // Initial state
        Assert.Equal(0, searchService.ActiveSearchContext!.CurrentIndex);

        // Act - Simulate 'n' key press via service
        keyboardNav.NavigateSearchNext();

        // Assert
        Assert.Equal(1, searchService.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchNext_RapidSequentialCalls_AllProcessed()
    {
        // Arrange - Performance test
        var searchService = CreateSearchServiceWithMatches("sensor", 5);
        var messageNav = new MessageNavigationState();
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false
        );

        // Act - Simulate rapid 'n' key presses
        for (int i = 0; i < 10; i++)
        {
            keyboardNav.NavigateSearchNext();
        }

        // Assert - Should have wrapped around twice (10 % 5 = 0)
        Assert.Equal(0, searchService.ActiveSearchContext!.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchNext_NoActiveSearch_NoOp()
    {
        // Arrange
        var searchService = new TopicSearchService(() => new List<TopicReference>());
        var messageNav = new MessageNavigationState();
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false
        );

        // Act - Try to navigate without active search (should not throw)
        keyboardNav.NavigateSearchNext();

        // Assert - No exception thrown
        Assert.Null(searchService.ActiveSearchContext);
    }

    /// <summary>
    /// Creates a SearchContext with specified number of matching topics.
    /// </summary>
    private static SearchContext CreateSearchContextWithMatches(string searchTerm, int matchCount)
    {
        var matches = new List<TopicReference>();

        if (matchCount >= 1)
            matches.Add(new TopicReference("sensor/temperature", "Temperature", Guid.NewGuid()));
        if (matchCount >= 2)
            matches.Add(new TopicReference("sensor/humidity", "Humidity", Guid.NewGuid()));
        if (matchCount >= 3)
            matches.Add(new TopicReference("sensor/pressure", "Pressure", Guid.NewGuid()));
        if (matchCount >= 4)
            matches.Add(new TopicReference("sensor/light", "Light", Guid.NewGuid()));
        if (matchCount >= 5)
            matches.Add(new TopicReference("sensor/motion", "Motion", Guid.NewGuid()));

        return new SearchContext(searchTerm, matches);
    }

    /// <summary>
    /// Creates a TopicSearchService with active search context.
    /// </summary>
    private static ITopicSearchService CreateSearchServiceWithMatches(string searchTerm, int matchCount)
    {
        var matches = new List<TopicReference>();

        if (matchCount >= 1)
            matches.Add(new TopicReference("sensor/temperature", "Temperature", Guid.NewGuid()));
        if (matchCount >= 2)
            matches.Add(new TopicReference("sensor/humidity", "Humidity", Guid.NewGuid()));
        if (matchCount >= 3)
            matches.Add(new TopicReference("sensor/pressure", "Pressure", Guid.NewGuid()));
        if (matchCount >= 4)
            matches.Add(new TopicReference("sensor/light", "Light", Guid.NewGuid()));
        if (matchCount >= 5)
            matches.Add(new TopicReference("sensor/motion", "Motion", Guid.NewGuid()));

        var service = new TopicSearchService(() => matches);
        service.ExecuteSearch(searchTerm);
        return service;
    }
}
