namespace CrowsNestMQTT.BusinessLogic.Navigation;

using System;

/// <summary>
/// Contract: Topic Search Service
/// Provides topic search functionality with case-insensitive substring matching.
///
/// Functional Requirements Addressed:
/// - FR-001: Search mechanism triggered by /[term] command
/// - FR-002: Case-insensitive partial matching
/// - FR-003: Substring matching within topic names
/// - FR-004: Auto-select first matching topic
/// - FR-007: Feedback when no matches found
/// </summary>
public interface ITopicSearchService
{
    /// <summary>
    /// Searches for topics matching the given search term (case-insensitive substring match).
    /// </summary>
    /// <param name="searchTerm">The term to search for (non-null, non-whitespace)</param>
    /// <returns>Search context containing matches and navigation state</returns>
    /// <exception cref="ArgumentException">If searchTerm is null or whitespace</exception>
    /// <remarks>
    /// - Returns empty SearchContext if no matches found
    /// - Matching is case-insensitive using StringComparison.OrdinalIgnoreCase
    /// - Searches full topic path (e.g., "temp" matches "sensor/temperature/bedroom")
    /// - First match is auto-selected (CurrentIndex = 0) if matches exist
    /// </remarks>
    SearchContext ExecuteSearch(string searchTerm);

    /// <summary>
    /// Clears the active search context.
    /// </summary>
    /// <remarks>
    /// - Resets active search to null
    /// - Clears status bar search indicator
    /// - Does not change current topic selection
    /// </remarks>
    void ClearSearch();

    /// <summary>
    /// Gets the currently active search context, or null if no search is active.
    /// </summary>
    SearchContext? ActiveSearchContext { get; }
}
