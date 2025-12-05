namespace CrowsNestMqtt.UI.Services;

using Avalonia.Input;
using CrowsNestMQTT.BusinessLogic.Navigation;

/// <summary>
/// Contract: Keyboard Navigation Service
/// Coordinates global keyboard event handling for search and message navigation.
///
/// Functional Requirements Addressed:
/// - FR-008: Navigate next search result with 'n'
/// - FR-009: Navigate previous search result with 'N'
/// - FR-013: Navigate down messages with 'j'
/// - FR-014: Navigate up messages with 'k'
/// - FR-020: Global shortcuts except when command palette active
/// - FR-021: Suppress shortcuts during text input
/// </summary>
public interface IKeyboardNavigationService
{
    /// <summary>
    /// Handles keyboard events for navigation shortcuts.
    /// </summary>
    /// <param name="key">The key pressed</param>
    /// <param name="modifiers">Modifier keys (Shift, Ctrl, Alt)</param>
    /// <returns>True if event was handled (should not propagate), false otherwise</returns>
    /// <remarks>
    /// Shortcuts:
    /// - 'n': Next search match
    /// - 'N' (Shift+n): Previous search match
    /// - 'j': Next message
    /// - 'k': Previous message
    ///
    /// Suppression:
    /// - All shortcuts suppressed when command palette TextBox has focus
    /// - Events marked as handled to prevent text input
    /// </remarks>
    bool HandleKeyPress(Key key, KeyModifiers modifiers);

    /// <summary>
    /// Determines if shortcuts should be suppressed based on current focus.
    /// </summary>
    /// <returns>True if shortcuts should be suppressed, false if they should execute</returns>
    /// <remarks>
    /// Checks:
    /// - Is command palette text input focused?
    /// - Other text input controls (if any)
    /// </remarks>
    bool ShouldSuppressShortcuts();

    /// <summary>
    /// Navigate to next search match (wraps to first at end).
    /// </summary>
    /// <remarks>
    /// - No-op if no active search context
    /// - Updates topic selection and message history view
    /// - Updates status bar position indicator
    /// </remarks>
    void NavigateSearchNext();

    /// <summary>
    /// Navigate to previous search match (wraps to last at start).
    /// </summary>
    /// <remarks>
    /// - No-op if no active search context
    /// - Updates topic selection and message history view
    /// - Updates status bar position indicator
    /// </remarks>
    void NavigateSearchPrevious();

    /// <summary>
    /// Navigate to next message in history (wraps to first at end).
    /// </summary>
    /// <remarks>
    /// - No-op if no messages in current topic
    /// - Updates message selection in view
    /// - Scrolls to selected message
    /// </remarks>
    void NavigateMessageDown();

    /// <summary>
    /// Navigate to previous message in history (wraps to first at start).
    /// </summary>
    /// <remarks>
    /// - No-op if no messages in current topic
    /// - Updates message selection in view
    /// - Scrolls to selected message
    /// </remarks>
    void NavigateMessageUp();

    /// <summary>
    /// Gets the active search context (if any).
    /// </summary>
    SearchContext? ActiveSearchContext { get; }

    /// <summary>
    /// Gets the message navigation state.
    /// </summary>
    MessageNavigationState MessageNavigation { get; }
}
