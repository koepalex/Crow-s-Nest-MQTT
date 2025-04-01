using ReactiveUI;

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// Base class for all ViewModels using ReactiveUI.
/// Provides basic implementation of INotifyPropertyChanged via ReactiveObject.
/// </summary>
public abstract class ReactiveViewModel : ReactiveObject
{
    // Base class can be extended with common properties or methods if needed later.
}
