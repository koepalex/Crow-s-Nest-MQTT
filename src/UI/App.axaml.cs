using Avalonia;
using Avalonia.Markup.Xaml;
using System.Diagnostics.CodeAnalysis;

namespace CrowsNestMqtt.UI; // Changed namespace

[ExcludeFromCodeCoverage]
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
