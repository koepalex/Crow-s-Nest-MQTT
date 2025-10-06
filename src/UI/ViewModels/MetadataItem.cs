using ReactiveUI;
using CrowsNestMqtt.BusinessLogic.Models; // Added for ResponseStatus

namespace CrowsNestMqtt.UI.ViewModels;

// Enhanced record for DataGrid items with optional icon support
public class MetadataItem : ReactiveObject
{
    private ResponseIconViewModel? _iconViewModel;

    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Optional icon view model for response-topic metadata items
    /// </summary>
    public ResponseIconViewModel? IconViewModel
    {
        get => _iconViewModel;
        set => this.RaiseAndSetIfChanged(ref _iconViewModel, value);
    }

    /// <summary>
    /// Indicates if this metadata item should show an icon
    /// </summary>
    public bool HasIcon => IconViewModel != null && IconViewModel.IsVisible;

    public MetadataItem(string key, string value, ResponseIconViewModel? iconViewModel = null)
    {
        Key = key;
        Value = value;
        IconViewModel = iconViewModel;
    }

    public override bool Equals(object? obj)
    {
        if (obj is MetadataItem other)
        {
            return Key == other.Key && Value == other.Value;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Value);
    }

    public override string ToString()
    {
        return $"MetadataItem {{ Key = {Key}, Value = {Value} }}";
    }
}

/// <summary>
/// View model for response status icons in metadata items
/// </summary>
public class ResponseIconViewModel : ReactiveObject
{
    private ResponseStatus _status;
    private string _iconPath = string.Empty;
    private string _toolTip = string.Empty;
    private bool _isClickable;
    private bool _isVisible = true;

    public string RequestMessageId { get; init; } = string.Empty;

    public ResponseStatus Status
    {
        get => _status;
        set
        {
            this.RaiseAndSetIfChanged(ref _status, value);
            this.RaisePropertyChanged(nameof(IsResponseReceived));
        }
    }

    /// <summary>
    /// Computed property for XAML binding to determine if response has been received
    /// </summary>
    public bool IsResponseReceived => Status == ResponseStatus.Received;

    public string IconPath
    {
        get => _iconPath;
        set => this.RaiseAndSetIfChanged(ref _iconPath, value);
    }

    public string ToolTip
    {
        get => _toolTip;
        set => this.RaiseAndSetIfChanged(ref _toolTip, value);
    }

    public bool IsClickable
    {
        get => _isClickable;
        set => this.RaiseAndSetIfChanged(ref _isClickable, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public string? NavigationCommand { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
