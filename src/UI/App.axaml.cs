using Avalonia;
using Avalonia.Markup.Xaml;

namespace CrowsNestMqtt.UI; // Changed namespace

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // OnFrameworkInitializationCompleted is removed as it contains desktop-specific logic.
    // Host projects (Desktop, WASM) will handle their specific initialization.
}