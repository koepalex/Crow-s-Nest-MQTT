using Avalonia.Controls;
using Avalonia.ReactiveUI; // If using ReactiveUI bindings in code-behind
using CrowsNestMqtt.UI.ViewModels; // Namespace for MainViewModel

namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// Code-behind for the MainView.axaml user control.
/// </summary>
public partial class MainView : UserControl // Consider ReactiveUserControl<MainViewModel> if using ReactiveUI features heavily
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainView"/> class.
    /// </summary>
    public MainView()
    {
        InitializeComponent();

        // DataContext is typically set by the framework when this view is used,
        // often in App.axaml.cs or MainWindow.xaml/cs.
        // If needed, you could assign a design-time context here, but it's
        // usually handled in the XAML <Design.DataContext>

        // Example of accessing ViewModel if needed (ensure DataContext is set)
        // this.WhenActivated(disposables => {
        //     if (ViewModel is MainViewModel vm)
        //     {
        //         // Start timer when view is activated
        //         vm.StartTimer();
        //         // Stop timer when view is deactivated
        //         Disposable.Create(vm.StopTimer).DisposeWith(disposables);
        //     }
        // });
    }

    // InitializeComponent is automatically generated from the XAML.
    // No need to define it manually unless customizing the build process.
}
