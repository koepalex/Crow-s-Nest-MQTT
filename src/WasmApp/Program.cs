using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using CrowsNestMqtt.UI; // Use the App from the UI project

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        await BuildAvaloniaApp()
            .UseReactiveUI() // Ensure ReactiveUI is initialized
            .StartBrowserAppAsync("out"); // "out" is the standard div ID for Avalonia WASM apps
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>(); // Configure using the App class from CrowsNestMqtt.UI
}