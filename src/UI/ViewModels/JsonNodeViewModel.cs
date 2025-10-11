using ReactiveUI;
using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Media; // For Brushes

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// Represents a node in the JSON TreeView.
/// </summary>
public class JsonNodeViewModel : ReactiveObject
{
    public string Name { get; }
    public string ValueDisplay { get; }
    public JsonValueKind ValueKind { get; }
    public string JsonPath { get; }
    public bool IsValueNode => Children.Count == 0; // Leaf nodes are value nodes

    // For TreeView binding
    public ObservableCollection<JsonNodeViewModel> Children { get; } = new();

    // Expansion state for automatic expansion feature
    private bool _isExpanded = true; // Default to expanded
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    // Depth tracking for automatic expansion (1-based: root children = 1)
    public int Depth { get; set; }

    // For Syntax Highlighting
    public IBrush ValueBrush { get; }

    public JsonNodeViewModel(string name, JsonElement element, string jsonPath)
    {
        Name = name;
        ValueKind = element.ValueKind;
        JsonPath = jsonPath;
        ValueDisplay = GetValueDisplay(element);
        ValueBrush = GetValueBrush(element.ValueKind);

        // Note: Children are populated recursively by JsonViewerViewModel.PopulateNodes
    }

    private string GetValueDisplay(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => "{...}", // Placeholder for objects
            JsonValueKind.Array => $"[...] ({element.GetArrayLength()})", // Placeholder for arrays showing count
            JsonValueKind.String => $"\"{element.GetString()}\"", // Add quotes for strings
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.ToString() // Fallback
        };
    }

    private IBrush GetValueBrush(JsonValueKind kind)
    {
        // Basic syntax highlighting colors
        return kind switch
        {
            JsonValueKind.String => Brushes.Green,
            JsonValueKind.Number => Brushes.Blue,
            JsonValueKind.True => Brushes.DarkOrange,
            JsonValueKind.False => Brushes.DarkOrange,
            JsonValueKind.Null => Brushes.Gray,
            _ => Brushes.Black // Default for keys, objects, arrays
        };
    }
}