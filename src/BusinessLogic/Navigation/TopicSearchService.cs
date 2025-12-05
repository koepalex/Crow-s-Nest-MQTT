namespace CrowsNestMQTT.BusinessLogic.Navigation;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Provides topic search functionality with case-insensitive substring matching.
/// Implements FR-001 through FR-007 for topic search feature.
/// </summary>
public sealed class TopicSearchService : ITopicSearchService
{
    private readonly Func<IEnumerable<TopicReference>> _topicProvider;
    private SearchContext? _activeSearchContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicSearchService"/> class.
    /// </summary>
    /// <param name="topicProvider">Function that provides available topics for searching</param>
    /// <exception cref="ArgumentNullException">If topicProvider is null</exception>
    public TopicSearchService(Func<IEnumerable<TopicReference>> topicProvider)
    {
        ArgumentNullException.ThrowIfNull(topicProvider);
        _topicProvider = topicProvider;
    }

    /// <inheritdoc/>
    public SearchContext? ActiveSearchContext => _activeSearchContext;

    /// <inheritdoc/>
    public SearchContext ExecuteSearch(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            throw new ArgumentException("Search term cannot be null or whitespace.", nameof(searchTerm));
        }

        // Get available topics from provider
        var availableTopics = _topicProvider();

        // Perform case-insensitive substring search on topic paths
        var matches = availableTopics
            .Where(topic => topic.TopicPath.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Create search context with results
        var searchContext = new SearchContext(searchTerm, matches);

        // Update active search context
        _activeSearchContext = searchContext;

        return searchContext;
    }

    /// <inheritdoc/>
    public void ClearSearch()
    {
        _activeSearchContext = null;
    }
}
