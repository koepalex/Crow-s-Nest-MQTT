using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CrowsNestMqtt.BusinessLogic.Configuration;
using FluentAvalonia.Styling;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CrowsNestMqtt.UI; // Changed namespace

[ExcludeFromCodeCoverage]
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void ApplyTheme(AppTheme theme)
    {
        var themeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        RequestedThemeVariant = themeVariant;

        // Force the Win32 window's DWM attributes (title bar, Mica backdrop) to match the new theme
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Window mainWindow)
        {
            var faTheme = Styles.OfType<FluentAvaloniaTheme>().FirstOrDefault();
            faTheme?.ForceWin32WindowToTheme(mainWindow, themeVariant);
        }
    }
}
