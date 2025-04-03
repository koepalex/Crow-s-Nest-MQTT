using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrowsNestMqtt.UI.ViewModels; // Added for MainViewModel
using CrowsNestMqtt.UI.Views; // Assuming MainWindow is now in App namespace or adjust if needed
using CrowsNestMqtt.Businesslogic.Services;
using Avalonia.Controls;
using ReactiveUI; // Added for CommandParserService

namespace CrowsNestMqtt.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                // Set the DataContext for the MainWindow
                // Instantiate the service and inject it into the ViewModel
                DataContext = new MainViewModel(new CommandParserService())
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
