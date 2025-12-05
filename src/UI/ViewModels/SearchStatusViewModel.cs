namespace CrowsNestMqtt.UI.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using CrowsNestMQTT.BusinessLogic.Navigation;

/// <summary>
/// Provides formatted search status information for UI display.
/// Implements FR-022, FR-023, FR-024, FR-025.
/// </summary>
public sealed class SearchStatusViewModel : ISearchStatusProvider, INotifyPropertyChanged
{
    private SearchContext? _currentContext;
    private string _statusText = string.Empty;
    private bool _isVisible;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    /// <inheritdoc/>
    public void UpdateFromContext(SearchContext? context)
    {
        _currentContext = context;

        if (context == null)
        {
            // FR-025: Clear indicators when search cancelled
            StatusText = string.Empty;
            IsVisible = false;
            return;
        }

        // FR-022: Display search term and match count
        if (!context.HasMatches)
        {
            // FR-022: Feedback when no matches
            StatusText = $"No topics matching '{context.SearchTerm}'";
            IsVisible = true;
            return;
        }

        // FR-023: Update position indicator during navigation
        if (context.CurrentIndex == 0)
        {
            // Initial position - show total matches
            StatusText = $"Search: '{context.SearchTerm}' ({context.TotalMatches} match{(context.TotalMatches == 1 ? "" : "es")})";
        }
        else
        {
            // Navigating - show current position (1-based)
            StatusText = $"Search: '{context.SearchTerm}' (match {context.CurrentIndex + 1} of {context.TotalMatches})";
        }

        IsVisible = true;
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
