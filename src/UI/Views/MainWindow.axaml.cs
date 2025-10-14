using Avalonia.Controls;
using System.Diagnostics.CodeAnalysis;

namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// The main window of the application.
/// Hosts the MainView user control.
/// </summary>
[ExcludeFromCodeCoverage] // UI initialization code is not unit testable
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}