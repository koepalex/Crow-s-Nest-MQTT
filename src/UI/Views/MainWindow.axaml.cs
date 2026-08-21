using Avalonia.Controls;
using Avalonia.Media;

using FluentAvalonia.UI.Windowing;

using System.Diagnostics.CodeAnalysis;

namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// The main window of the application.
/// Hosts the MainView user control.
/// </summary>
[ExcludeFromCodeCoverage] // UI initialization code is not unit testable
public partial class MainWindow : FAAppWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        TransparencyLevelHint = [WindowTransparencyLevel.Mica, WindowTransparencyLevel.None];
        TitleBar.ExtendsContentIntoTitleBar = true;

        InitializeComponent();
        Background = SelectPlatformBackground(OperatingSystem.IsLinux(), Background);
    }

    internal static IBrush? SelectPlatformBackground(bool isLinux, IBrush? currentBackground) =>
        isLinux ? currentBackground : Brushes.Transparent;
}