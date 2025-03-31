using ReactiveUI;
using System;

namespace  CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// To be replaced.
/// </summary>
public class ReactiveViewModel : ReactiveObject
{
    /// <summary>
    /// Generated instance of <see cref="ReactiveViewModel"/> .
    /// </summary>
    public ReactiveViewModel()
    {
        // We can listen to any property changes with "WhenAnyValue" and do whatever we want in "Subscribe".
        this.WhenAnyValue(o => o.Name)
            .Subscribe(o => this.RaisePropertyChanged(nameof(Greeting)));
    }

    private string? _Name; // This is our backing field for Name

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name
    {
        get
        {
            return _Name;
        }
        set
        {
            // We can use "RaiseAndSetIfChanged" to check if the value changed and automatically notify the UI
            this.RaiseAndSetIfChanged(ref _Name, value);
        }
    }

    /// <summary>
    /// Gets the Greeting.
    /// </summary>
    public string Greeting
    {
        get
        {
            if (string.IsNullOrEmpty(Name))
            {
                // If no Name is provided, use a default Greeting
                return "Hello World from Avalonia.Samples";
            }
            else
            {
                // else Greet the User.
                return $"Hello {Name}";
            }
        }
    }
}
