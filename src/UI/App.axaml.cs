using Avalonia;
using Avalonia.Markup.Xaml;

namespace CrowsNestMqtt.UI; // Changed namespace

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}