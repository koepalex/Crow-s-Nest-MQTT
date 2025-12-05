/// <summary>
/// Contract: Search Status Provider
/// Provides formatted search status information for UI display.
///
/// Functional Requirements Addressed:
/// - FR-022: Display search term and match count
/// - FR-023: Update position indicator during navigation
/// - FR-024: Consistent status bar location
/// - FR-025: Clear indicators when search cancelled/replaced
/// </summary>
namespace CrowsNestMQTT.UI.ViewModels
{
    using CrowsNestMQTT.BusinessLogic.Navigation;

    public interface ISearchStatusProvider
    {
        /// <summary>
        /// Gets formatted status text for display in UI.
        /// </summary>
        /// <returns>
        /// - Empty string if no active search
        /// - "No topics matching '[term]'" if search found no matches
        /// - "Search: '[term]' (X matches)" if matches exist but not navigating
        /// - "Search: '[term]' (match Y of X)" when navigating through results
        /// </returns>
        /// <remarks>
        /// Format examples:
        /// - No search: ""
        /// - No matches: "No topics matching 'xyz'"
        /// - With matches (initial): "Search: 'sensor' (3 matches)"
        /// - Navigating: "Search: 'sensor' (match 2 of 3)"
        /// </remarks>
        string StatusText { get; }

        /// <summary>
        /// Gets whether status should be visible in UI.
        /// </summary>
        /// <returns>True if there's an active search context, false otherwise</returns>
        bool IsVisible { get; }

        /// <summary>
        /// Updates status based on current search context.
        /// </summary>
        /// <param name="context">Active search context, or null to clear status</param>
        /// <remarks>
        /// - Raises PropertyChanged for StatusText and IsVisible
        /// - Called automatically when search context changes
        /// - Called when navigation position changes (n/N keys)
        /// </remarks>
        void UpdateFromContext(SearchContext? context);
    }
}
