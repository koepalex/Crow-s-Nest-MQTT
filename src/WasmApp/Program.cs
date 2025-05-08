using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using CrowsNestMqtt.UI; // Use the App from the UI project
using Microsoft.Extensions.Logging;
using CrowsNestMqtt.Utils;

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug); // Example
        });
        AppLogger.InitializeWasmLogger(loggerFactory);

        await BuildAvaloniaApp()
            .UseReactiveUI() // Ensure ReactiveUI is initialized
            .StartBrowserAppAsync("out"); // "out" is the standard div ID for Avalonia WASM apps
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<CrowsNestMqtt.UI.App>(); // Configure using the App class from CrowsNestMqtt.UI
}