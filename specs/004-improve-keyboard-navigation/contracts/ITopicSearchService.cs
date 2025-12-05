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
namespace CrowsNestMQTT.BusinessLogic.Navigation
{
    using System.Collections.Generic;

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

    /// <summary>
    /// Contract: Search result container
    /// </summary>
    public class SearchContext
    {
        /// <summary>Original search term (case-preserved)</summary>
        public string SearchTerm { get; }

        /// <summary>Immutable list of matching topics</summary>
        public IReadOnlyList<TopicReference> Matches { get; }

        /// <summary>Current navigation position (0-based, -1 if no matches)</summary>
        public int CurrentIndex { get; set; }

        /// <summary>Total number of matches</summary>
        public int TotalMatches => Matches.Count;

        /// <summary>Whether any topics matched</summary>
        public bool HasMatches => Matches.Count > 0;

        /// <summary>Whether search is actively navigable</summary>
        public bool IsActive => HasMatches && CurrentIndex >= 0;

        // Constructor omitted for brevity (parameter validation required)

        /// <summary>Navigate to next match (wrap-around at end)</summary>
        public void MoveNext();

        /// <summary>Navigate to previous match (wrap-around at start)</summary>
        public void MovePrevious();

        /// <summary>Get currently selected match, or null if no matches</summary>
        public TopicReference? GetCurrentMatch();
    }

    /// <summary>
    /// Contract: Lightweight topic reference
    /// </summary>
    public class TopicReference
    {
        /// <summary>Full MQTT topic path</summary>
        public string TopicPath { get; }

        /// <summary>Human-readable display name</summary>
        public string DisplayName { get; }

        /// <summary>Internal topic identifier</summary>
        public Guid TopicId { get; }

        // Constructor and equality members omitted for brevity
    }
}
