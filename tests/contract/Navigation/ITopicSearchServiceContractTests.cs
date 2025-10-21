/// <summary>
/// Contract tests for ITopicSearchService
/// These tests MUST FAIL until implementation is complete (TDD approach)
///
/// Validates:
/// - FR-001: Search mechanism with /[term] command
/// - FR-002: Case-insensitive partial matching
/// - FR-003: Substring matching within topic names
/// - FR-004: Auto-select first matching topic
/// - FR-007: Feedback when no matches found
/// </summary>
namespace CrowsNestMQTT.Tests.Contract.Navigation;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

public class ITopicSearchServiceContractTests
{
    [Fact]
    public void ExecuteSearch_WithValidSearchTerm_ReturnsSearchContext()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ExecuteSearch("sensor");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("sensor", result.SearchTerm);
    }

    [Fact]
    public void ExecuteSearch_WithMatchingTopics_ReturnsMatchesInContext()
    {
        // Arrange
        var service = CreateServiceWithTopics(new[]
        {
            "sensor/temperature",
            "sensor/humidity",
            "device/status"
        });

        // Act
        var result = service.ExecuteSearch("sensor");

        // Assert
        Assert.NotNull(result.Matches);
        Assert.Equal(2, result.TotalMatches);
        Assert.True(result.HasMatches);
    }

    [Fact]
    public void ExecuteSearch_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange - FR-002: Case-insensitive matching
        var service = CreateServiceWithTopics(new[]
        {
            "SENSOR/Temperature",
            "device/SENSOR/status"
        });

        // Act - search with lowercase
        var resultLower = service.ExecuteSearch("sensor");

        // Assert
        Assert.Equal(2, resultLower.TotalMatches);

        // Act - search with uppercase
        var resultUpper = service.ExecuteSearch("SENSOR");
        Assert.Equal(2, resultUpper.TotalMatches);

        // Act - search with mixed case
        var resultMixed = service.ExecuteSearch("SeNsOr");
        Assert.Equal(2, resultMixed.TotalMatches);
    }

    [Fact]
    public void ExecuteSearch_SubstringMatching_MatchesPartialTopicNames()
    {
        // Arrange - FR-003: Substring matching
        var service = CreateServiceWithTopics(new[]
        {
            "sensor/temperature/bedroom",
            "device/thermostat",
            "logs/error"
        });

        // Act
        var result = service.ExecuteSearch("temp");

        // Assert - Should match both "temperature" and "thermostat"
        Assert.Equal(2, result.TotalMatches);
    }

    [Fact]
    public void ExecuteSearch_FirstMatchAutoSelected_SetsCurrentIndexToZero()
    {
        // Arrange - FR-004: Auto-select first match
        var service = CreateServiceWithTopics(new[]
        {
            "sensor/temperature",
            "sensor/humidity"
        });

        // Act
        var result = service.ExecuteSearch("sensor");

        // Assert
        Assert.Equal(0, result.CurrentIndex);
        Assert.True(result.IsActive);
    }

    [Fact]
    public void ExecuteSearch_NoMatches_ReturnsEmptySearchContext()
    {
        // Arrange - FR-007: No matches feedback
        var service = CreateServiceWithTopics(new[]
        {
            "sensor/temperature",
            "device/status"
        });

        // Act
        var result = service.ExecuteSearch("nonexistent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("nonexistent", result.SearchTerm);
        Assert.Equal(0, result.TotalMatches);
        Assert.False(result.HasMatches);
        Assert.Equal(-1, result.CurrentIndex);
        Assert.False(result.IsActive);
    }

    [Fact]
    public void ExecuteSearch_NullSearchTerm_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ExecuteSearch(null!));
    }

    [Fact]
    public void ExecuteSearch_EmptySearchTerm_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ExecuteSearch(""));
    }

    [Fact]
    public void ExecuteSearch_WhitespaceSearchTerm_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.ExecuteSearch("   "));
    }

    [Fact]
    public void ExecuteSearch_SetsActiveSearchContext()
    {
        // Arrange
        var service = CreateServiceWithTopics(new[] { "sensor/temperature" });
        Assert.Null(service.ActiveSearchContext);

        // Act
        var result = service.ExecuteSearch("sensor");

        // Assert
        Assert.NotNull(service.ActiveSearchContext);
        Assert.Same(result, service.ActiveSearchContext);
    }

    [Fact]
    public void ExecuteSearch_ReplacesExistingActiveSearchContext()
    {
        // Arrange
        var service = CreateServiceWithTopics(new[]
        {
            "sensor/temperature",
            "device/status"
        });

        var firstSearch = service.ExecuteSearch("sensor");
        Assert.NotNull(service.ActiveSearchContext);

        // Act - perform new search
        var secondSearch = service.ExecuteSearch("device");

        // Assert
        Assert.NotNull(service.ActiveSearchContext);
        Assert.Same(secondSearch, service.ActiveSearchContext);
        Assert.NotSame(firstSearch, service.ActiveSearchContext);
        Assert.Equal("device", service.ActiveSearchContext.SearchTerm);
    }

    [Fact]
    public void ClearSearch_SetsActiveSearchContextToNull()
    {
        // Arrange
        var service = CreateServiceWithTopics(new[] { "sensor/temperature" });
        service.ExecuteSearch("sensor");
        Assert.NotNull(service.ActiveSearchContext);

        // Act
        service.ClearSearch();

        // Assert
        Assert.Null(service.ActiveSearchContext);
    }

    [Fact]
    public void ClearSearch_WhenNoActiveSearch_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        Assert.Null(service.ActiveSearchContext);

        // Act & Assert - should not throw
        service.ClearSearch();
        Assert.Null(service.ActiveSearchContext);
    }

    [Fact]
    public void SearchContext_PreservesSearchTermCasing()
    {
        // Arrange
        var service = CreateServiceWithTopics(new[] { "SENSOR/temperature" });

        // Act
        var result = service.ExecuteSearch("SeNsOr");

        // Assert - original casing should be preserved
        Assert.Equal("SeNsOr", result.SearchTerm);
    }

    [Fact]
    public void SearchContext_MatchesProperty_IsImmutable()
    {
        // Arrange
        var service = CreateServiceWithTopics(new[]
        {
            "sensor/temperature",
            "sensor/humidity"
        });

        // Act
        var result = service.ExecuteSearch("sensor");
        var matchesCount = result.Matches.Count;

        // Assert - Matches should be read-only list
        Assert.IsAssignableFrom<IReadOnlyList<TopicReference>>(result.Matches);

        // Attempting to modify should not be possible
        // (compile-time check - IReadOnlyList doesn't expose Add/Remove)
        Assert.Equal(2, matchesCount);
    }

    // Helper methods - these will need actual implementation
    // For now, they will cause tests to fail
    private ITopicSearchService CreateService()
    {
        // This will fail until TopicSearchService is implemented
        throw new NotImplementedException(
            "TopicSearchService not implemented yet. " +
            "This is expected - tests should fail before implementation (TDD)."
        );
    }

    private ITopicSearchService CreateServiceWithTopics(string[] topicPaths)
    {
        // This will fail until TopicSearchService is implemented
        throw new NotImplementedException(
            "TopicSearchService not implemented yet. " +
            "This is expected - tests should fail before implementation (TDD)."
        );
    }
}
