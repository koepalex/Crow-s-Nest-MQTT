namespace CrowsNestMqtt.UI.Services;

using System;
using Avalonia.Input;
using CrowsNestMQTT.BusinessLogic.Navigation;

/// <summary>
/// Coordinates global keyboard event handling for search and message navigation.
/// Implements FR-008, FR-009, FR-013, FR-014, FR-020, FR-021.
/// </summary>
public sealed class KeyboardNavigationService : IKeyboardNavigationService
{
    private readonly ITopicSearchService _searchService;
    private readonly MessageNavigationState _messageNavigation;
    private readonly Func<bool> _shouldSuppressShortcuts;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardNavigationService"/> class.
    /// </summary>
    /// <param name="searchService">Topic search service</param>
    /// <param name="messageNavigation">Message navigation state</param>
    /// <param name="shouldSuppressShortcuts">Function to determine if shortcuts should be suppressed</param>
    public KeyboardNavigationService(
        ITopicSearchService searchService,
        MessageNavigationState messageNavigation,
        Func<bool> shouldSuppressShortcuts)
    {
        ArgumentNullException.ThrowIfNull(searchService);
        ArgumentNullException.ThrowIfNull(messageNavigation);
        ArgumentNullException.ThrowIfNull(shouldSuppressShortcuts);

        _searchService = searchService;
        _messageNavigation = messageNavigation;
        _shouldSuppressShortcuts = shouldSuppressShortcuts;
    }

    /// <inheritdoc/>
    public SearchContext? ActiveSearchContext => _searchService.ActiveSearchContext;

    /// <inheritdoc/>
    public MessageNavigationState MessageNavigation => _messageNavigation;

    /// <inheritdoc/>
    public bool HandleKeyPress(Key key, KeyModifiers modifiers)
    {
        // Suppress shortcuts when text input is focused (e.g., command palette)
        if (ShouldSuppressShortcuts())
        {
            return false;
        }

        // Handle navigation shortcuts
        return (key, modifiers) switch
        {
            (Key.N, KeyModifiers.Shift) => HandleShortcut(NavigateSearchPrevious),
            (Key.N, KeyModifiers.None) => HandleShortcut(NavigateSearchNext),
            (Key.J, KeyModifiers.None) => HandleShortcut(NavigateMessageDown),
            (Key.K, KeyModifiers.None) => HandleShortcut(NavigateMessageUp),
            _ => false // Not a navigation key
        };
    }

    /// <inheritdoc/>
    public bool ShouldSuppressShortcuts()
    {
        return _shouldSuppressShortcuts();
    }

    /// <inheritdoc/>
    public void NavigateSearchNext()
    {
        var context = _searchService.ActiveSearchContext;
        if (context == null || !context.HasMatches)
        {
            return; // No-op
        }

        context.MoveNext();
        // Note: UI binding will update automatically via INotifyPropertyChanged
    }

    /// <inheritdoc/>
    public void NavigateSearchPrevious()
    {
        var context = _searchService.ActiveSearchContext;
        if (context == null || !context.HasMatches)
        {
            return; // No-op
        }

        context.MovePrevious();
        // Note: UI binding will update automatically via INotifyPropertyChanged
    }

    /// <inheritdoc/>
    public void NavigateMessageDown()
    {
        if (!_messageNavigation.HasMessages)
        {
            return; // No-op
        }

        _messageNavigation.MoveDown();
        // Note: UI binding will update automatically via INotifyPropertyChanged
    }

    /// <inheritdoc/>
    public void NavigateMessageUp()
    {
        if (!_messageNavigation.HasMessages)
        {
            return; // No-op
        }

        _messageNavigation.MoveUp();
        // Note: UI binding will update automatically via INotifyPropertyChanged
    }

    /// <summary>
    /// Executes a shortcut action and returns true to mark the event as handled.
    /// </summary>
    private static bool HandleShortcut(Action action)
    {
        action();
        return true;
    }
}
