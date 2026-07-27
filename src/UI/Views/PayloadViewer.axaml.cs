using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace CrowsNestMqtt.UI.Views;

public partial class PayloadViewer : UserControl
{
    public PayloadViewer()
    {
        InitializeComponent();
    }

    private void RawPayloadEditor_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var registryOptions = new RegistryOptions(ThemeName.VisualStudioDark);
        var installation = RawPayloadEditor.InstallTextMate(registryOptions);
        installation.SetGrammar(registryOptions.GetLanguageByExtension(".json").Id);
    }
}
