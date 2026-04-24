using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CrowsNestMqtt.UI.ViewModels;
using ReactiveUI;

namespace CrowsNestMqtt.UI.Views;

/// <summary>
/// Non-modal floating window for publishing MQTT messages.
/// </summary>
[ExcludeFromCodeCoverage]
public partial class PublishWindow : Window
{
    /// <summary>Command used by the Escape key binding to close the window.</summary>
    public ICommand CloseCommand { get; }

    public PublishWindow()
    {
        CloseCommand = ReactiveCommand.Create(Close);
        InitializeComponent();

        // Register with handledEventsToo:true so shortcuts still close the
        // window even when a focused child control (e.g. AvaloniaEdit's
        // TextEditor) has already marked the KeyDown event as Handled.
        AddHandler(KeyDownEvent, OnKeyDownForcedHandler,
            RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnKeyDownForcedHandler(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        // Ctrl+Shift+M toggles the publish window globally. Main window can't
        // see this keystroke when PublishWindow has focus, so handle it here
        // too — "toggle" from inside a visible window means close.
        if (e.Key == Key.M && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            Close();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is PublishViewModel vm)
        {
            // Wire the LoadFileCommand to open a file picker then load content
            vm.LoadFileCommand.Subscribe(async _ =>
            {
                try
                {
                    var topLevel = GetTopLevel(this);
                    if (topLevel == null) return;

                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(
                        new FilePickerOpenOptions
                        {
                            Title = "Select payload file",
                            AllowMultiple = false,
                            FileTypeFilter =
                            [
                                new FilePickerFileType("All Files") { Patterns = ["*"] },
                                new FilePickerFileType("JSON") { Patterns = ["*.json"] },
                                new FilePickerFileType("XML") { Patterns = ["*.xml"] },
                                new FilePickerFileType("Text") { Patterns = ["*.txt"] }
                            ]
                        });

                    if (files.Count > 0)
                    {
                        var path = files[0].TryGetLocalPath();
                        if (path != null)
                        {
                            await vm.LoadFileContentAsync(path);
                        }
                        else
                        {
                            vm.StatusText = "Could not resolve local file path.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    vm.StatusText = $"Error loading file: {ex.Message}";
                }
            });
        }
    }
}
