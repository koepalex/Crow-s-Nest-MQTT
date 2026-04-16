using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
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
