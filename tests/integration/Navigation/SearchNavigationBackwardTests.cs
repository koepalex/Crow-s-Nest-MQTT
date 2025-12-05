/// <summary>
/// Integration tests for backward navigation through search results.
/// Tests 'N' (Shift+n) key behavior with wrap-around.
/// Validates FR-009, FR-010, FR-023.
/// </summary>
namespace CrowsNestMQTT.Tests.Integration.Navigation;

using System;
using System.Collections.Generic;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

public class SearchNavigationBackwardTests
{
    [Fact]
    public void MovePrevious_FromFirstMatch_WrapsToLast()
    {
        // Arrange - FR-009: Navigate previous search result with 'N'
        var context = CreateSearchContextWithMatches("sensor", 3);
        Assert.Equal(0, context.CurrentIndex); // Start at first

        // Act - Simulate 'N' (Shift+n) key press
        context.MovePrevious();

        // Assert - FR-010: Should wrap to last match
        Assert.Equal(2, context.CurrentIndex);
        Assert.Equal("sensor/pressure", context.GetCurrentMatch()?.TopicPath);
    }

    [Fact]
    public void MovePrevious_FromSecondMatch_MovesToFirst()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("sensor", 3);
        context.MoveNext(); // Move to second match
        Assert.Equal(1, context.CurrentIndex);

        // Act
        context.MovePrevious();

        // Assert
        Assert.Equal(0, context.CurrentIndex);
        Assert.Equal("sensor/temperature", context.GetCurrentMatch()?.TopicPath);
    }

    [Fact]
    public void MovePrevious_WithSingleMatch_StaysAtSameMatch()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("pressure", 1);
        Assert.Equal(0, context.CurrentIndex);

        // Act
        context.MovePrevious();

        // Assert - Wraps back to same (only) match
        Assert.Equal(0, context.CurrentIndex);
    }

    [Fact]
    public void MovePrevious_MultipleSequentialCalls_NavigatesCorrectly()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("sensor", 3);

        // Act - Navigate backward through all matches
        context.MovePrevious(); // 0 -> 2 (wrap to last)
        Assert.Equal(2, context.CurrentIndex);

        context.MovePrevious(); // 2 -> 1
        Assert.Equal(1, context.CurrentIndex);

        context.MovePrevious(); // 1 -> 0
        Assert.Equal(0, context.CurrentIndex);

        context.MovePrevious(); // 0 -> 2 (wrap again)
        Assert.Equal(2, context.CurrentIndex);
    }

    [Fact]
    public void MovePrevious_FiresPropertyChangedForCurrentIndex()
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
        context.MovePrevious();

        // Assert
        Assert.True(propertyChanged, "PropertyChanged should be raised for CurrentIndex");
    }

    [Fact]
    public void NavigateSearchPrevious_WithKeyboardNavigationService_UpdatesContext()
    {
        // Arrange - Integration with KeyboardNavigationService
        var searchService = CreateSearchServiceWithMatches("sensor", 3);
        var messageNav = new MessageNavigationState();
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false // Shortcuts not suppressed
        );

        // Initial state - first match
        Assert.Equal(0, searchService.ActiveSearchContext!.CurrentIndex);

        // Act - Simulate 'N' key press via service
        keyboardNav.NavigateSearchPrevious();

        // Assert - Should wrap to last
        Assert.Equal(2, searchService.ActiveSearchContext.CurrentIndex);
    }

    [Fact]
    public void MixedForwardBackwardNavigation_MaintainsCorrectPosition()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("sensor", 5);
        Assert.Equal(0, context.CurrentIndex);

        // Act - Mix forward and backward navigation
        context.MoveNext();    // 0 -> 1
        context.MoveNext();    // 1 -> 2
        context.MovePrevious(); // 2 -> 1
        context.MoveNext();    // 1 -> 2
        context.MoveNext();    // 2 -> 3
        context.MovePrevious(); // 3 -> 2

        // Assert
        Assert.Equal(2, context.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchPrevious_RapidSequentialCalls_AllProcessed()
    {
        // Arrange - Performance test
        var searchService = CreateSearchServiceWithMatches("sensor", 5);
        var messageNav = new MessageNavigationState();
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false
        );

        // Act - Simulate rapid 'N' key presses (10 times backward from index 0)
        for (int i = 0; i < 10; i++)
        {
            keyboardNav.NavigateSearchPrevious();
        }

        // Assert - 10 steps backward from 0: wraps 2 times, ends at index 0
        // 0 -> 4 -> 3 -> 2 -> 1 -> 0 -> 4 -> 3 -> 2 -> 1 -> 0
        Assert.Equal(0, searchService.ActiveSearchContext!.CurrentIndex);
    }

    [Fact]
    public void NavigateSearchPrevious_NoActiveSearch_NoOp()
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
        keyboardNav.NavigateSearchPrevious();

        // Assert - No exception thrown
        Assert.Null(searchService.ActiveSearchContext);
    }

    [Fact]
    public void MovePrevious_GetCurrentMatch_ReturnsCorrectMatch()
    {
        // Arrange
        var context = CreateSearchContextWithMatches("sensor", 3);

        // Act & Assert - Verify GetCurrentMatch returns correct topic at each position
        Assert.Equal("sensor/temperature", context.GetCurrentMatch()?.TopicPath);

        context.MovePrevious(); // Wrap to last
        Assert.Equal("sensor/pressure", context.GetCurrentMatch()?.TopicPath);

        context.MovePrevious();
        Assert.Equal("sensor/humidity", context.GetCurrentMatch()?.TopicPath);

        context.MovePrevious();
        Assert.Equal("sensor/temperature", context.GetCurrentMatch()?.TopicPath);
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
