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
    /// <summary>
    /// Maximum number of characters to display for string / fallback values in
    /// the TreeView. Long strings (e.g. embedded base64 blobs or multi-KB text
    /// inside JSON payloads) are truncated with an ellipsis suffix to keep the
    /// TreeView responsive; the original value is still available via the
    /// underlying raw payload views.
    /// </summary>
    public const int MaxValueDisplayLength = 500;

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

    private static string GetValueDisplay(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => "{...}", // Placeholder for objects
            JsonValueKind.Array => $"[...] ({element.GetArrayLength()})", // Placeholder for arrays showing count
            JsonValueKind.String => FormatString(element.GetString()),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => Truncate(element.ToString()) // Fallback
        };
    }

    private static string FormatString(string? value)
    {
        if (value == null) return "\"\"";
        if (value.Length <= MaxValueDisplayLength)
        {
            return $"\"{value}\"";
        }
        // Truncate + ellipsis marker + original length hint. Keep quoting to
        // match the non-truncated rendering convention.
        return $"\"{value.Substring(0, MaxValueDisplayLength)}…\" ({value.Length} chars)";
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= MaxValueDisplayLength)
        {
            return value;
        }
        return value.Substring(0, MaxValueDisplayLength) + "…";
    }

    private static IBrush GetValueBrush(JsonValueKind kind)
    {
        // Basic syntax highlighting colors
        return kind switch
        {
            JsonValueKind.String => Brushes.Green,
            JsonValueKind.Number => Brushes.Blue,
            JsonValueKind.True => Brushes.DarkOrange,
            JsonValueKind.False => Brushes.DarkOrange,
            JsonValueKind.Null => Brushes.Gray,
            _ => Brushes.White // Default for keys, objects, arrays (visible on dark background)
        };
    }
}