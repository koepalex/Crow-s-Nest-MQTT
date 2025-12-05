namespace CrowsNestMQTT.BusinessLogic.Navigation;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

/// <summary>
/// Manages active topic search session state.
/// Includes search term, matching topics, and current navigation position.
/// Observable properties enable MVVM binding for UI updates.
/// Supports wrap-around navigation through search results.
/// </summary>
public sealed class SearchContext : INotifyPropertyChanged
{
    private int _currentIndex;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the original search term entered by the user (case-preserved).
    /// </summary>
    public string SearchTerm { get; }

    /// <summary>
    /// Gets the immutable list of topics matching the search term.
    /// </summary>
    public IReadOnlyList<TopicReference> Matches { get; }

    /// <summary>
    /// Gets or sets the current navigation position (0-based index).
    /// Returns -1 if no matches exist.
    /// </summary>
    public int CurrentIndex
    {
        get => _currentIndex;
        set
        {
            // Validate index bounds
            if (Matches.Count == 0)
            {
                _currentIndex = -1;
            }
            else if (value < 0 || value >= Matches.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"CurrentIndex must be between 0 and {Matches.Count - 1}, or -1 if no matches."
                );
            }
            else
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;
                    OnPropertyChanged();
                    // IsActive depends on CurrentIndex
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }
    }

    /// <summary>
    /// Gets the total number of matching topics.
    /// </summary>
    public int TotalMatches => Matches.Count;

    /// <summary>
    /// Gets a value indicating whether any topics matched the search.
    /// </summary>
    public bool HasMatches => Matches.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the search is actively navigable.
    /// True if there are matches and CurrentIndex is valid.
    /// </summary>
    public bool IsActive => HasMatches && CurrentIndex >= 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchContext"/> class.
    /// </summary>
    /// <param name="searchTerm">The search term (case-preserved)</param>
    /// <param name="matches">List of matching topics</param>
    /// <exception cref="ArgumentException">If searchTerm is null or whitespace</exception>
    /// <exception cref="ArgumentNullException">If matches is null</exception>
    public SearchContext(string searchTerm, IReadOnlyList<TopicReference> matches)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            throw new ArgumentException("Search term cannot be null or whitespace.", nameof(searchTerm));
        }

        ArgumentNullException.ThrowIfNull(matches);

        SearchTerm = searchTerm;
        Matches = matches;

        // Set initial index: 0 if matches exist, -1 if no matches
        _currentIndex = matches.Count > 0 ? 0 : -1;
    }

    /// <summary>
    /// Navigates to the next match in the search results.
    /// Wraps to the first match when at the end.
    /// No-op if no matches exist.
    /// </summary>
    public void MoveNext()
    {
        if (Matches.Count == 0)
        {
            return; // No-op
        }

        // Wrap-around: (current + 1) % count
        CurrentIndex = (CurrentIndex + 1) % Matches.Count;
    }

    /// <summary>
    /// Navigates to the previous match in the search results.
    /// Wraps to the last match when at the start.
    /// No-op if no matches exist.
    /// </summary>
    public void MovePrevious()
    {
        if (Matches.Count == 0)
        {
            return; // No-op
        }

        // Wrap-around: (current - 1 + count) % count
        CurrentIndex = (CurrentIndex - 1 + Matches.Count) % Matches.Count;
    }

    /// <summary>
    /// Gets the currently selected topic match.
    /// </summary>
    /// <returns>The TopicReference at CurrentIndex, or null if no matches</returns>
    public TopicReference? GetCurrentMatch()
    {
        if (CurrentIndex < 0 || CurrentIndex >= Matches.Count)
        {
            return null;
        }

        return Matches[CurrentIndex];
    }

    /// <summary>
    /// Resets the search context to an empty state (no matches).
    /// </summary>
    /// <returns>A new SearchContext with no matches</returns>
    public static SearchContext CreateEmpty(string searchTerm)
    {
        return new SearchContext(searchTerm, Array.Empty<TopicReference>());
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
