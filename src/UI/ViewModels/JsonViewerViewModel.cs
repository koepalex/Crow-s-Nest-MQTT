using ReactiveUI;
using System.Collections.ObjectModel;
using System.Text.Json; // For JsonDocument, JsonElement
using CrowsNestMqtt.Utils; // For AppLogger

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for the JSON Hierarchical Data Viewer.
/// Handles parsing JSON, building the tree structure, and managing tracked JSON paths.
/// </summary>
public class JsonViewerViewModel : ReactiveObject
{
    private string _jsonParseError = string.Empty;
    public string JsonParseError
    {
        get => _jsonParseError;
        private set => this.RaiseAndSetIfChanged(ref _jsonParseError, value);
    }

    private bool _hasParseError = true;
    public bool HasParseError
    {
        get => _hasParseError;
        private set => this.RaiseAndSetIfChanged(ref _hasParseError, value);
    }

    public ObservableCollection<JsonNodeViewModel> RootNodes { get; } = new();

// Property to potentially bind TreeView.SelectedItem if needed
private JsonNodeViewModel? _selectedNode;
public JsonNodeViewModel? SelectedNode
{
    get => _selectedNode;
    set => this.RaiseAndSetIfChanged(ref _selectedNode, value);
}


    /// <summary>
    /// Parses the input JSON string and populates the RootNodes collection.
    /// </summary>
    /// <param name="jsonString">The JSON string to parse.</param>
    public void LoadJson(string jsonString)
    {
        RootNodes.Clear();
        // TrackedPaths.Clear(); // Decide if tracked paths should clear when new JSON is loaded
        JsonParseError = string.Empty;
        HasParseError = false;

        if (string.IsNullOrWhiteSpace(jsonString))
        {
            // Don't treat empty string as an error, just clear the view
            // JsonParseError = "Input JSON string is empty.";
            // HasParseError = true;
            return;
        }

        try
        {
            // Use JsonDocument for efficient, read-only parsing
            using var jsonDoc = JsonDocument.Parse(jsonString);
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Create a single root node for the array itself
                var arrayRootNode = new JsonNodeViewModel("$", jsonDoc.RootElement, "$");
                RootNodes.Add(arrayRootNode);
                // Populate the children of this array node
                PopulateNodes(jsonDoc.RootElement, arrayRootNode.Children, arrayRootNode.JsonPath);
            }
            else
            {
                // Original behavior for root objects or other types
                PopulateNodes(jsonDoc.RootElement, RootNodes, "$"); // Start path at root '$'
            }
        }
        catch (JsonException ex)
        {
            AppLogger.Warning(ex, "Failed to parse JSON string.");
            JsonParseError = $"JSON Parsing Error: {ex.Message} (Line: {ex.LineNumber}, Pos: {ex.BytePositionInLine})";
            HasParseError = true;
        }
        catch (Exception ex) // Catch other potential errors
        {
            AppLogger.Error(ex, "An unexpected error occurred while loading JSON.");
            JsonParseError = $"An unexpected error occurred: {ex.Message}";
            HasParseError = true;
        }
    }

    private void PopulateNodes(JsonElement element, ObservableCollection<JsonNodeViewModel> children, string currentPath)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    // Use property name escaping if needed for complex keys
                    string safeName = property.Name; // Basic for now
                    string childPath = $"{currentPath}.{safeName}"; // Basic path construction
                    var node = new JsonNodeViewModel(property.Name, property.Value, childPath);
                    children.Add(node);
                    // Recursively populate children if it's an object or array
                    if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                    {
                        PopulateNodes(property.Value, node.Children, node.JsonPath);
                    }
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string childPath = $"{currentPath}[{index}]";
                    var node = new JsonNodeViewModel($"[{index}]", item, childPath);
                    children.Add(node);
                    // Recursively populate children if it's an object or array
                    if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                    {
                        PopulateNodes(item, node.Children, node.JsonPath);
                    }
                    index++;
                }
                break;

            // Value types (string, number, boolean, null) are handled within JsonNodeViewModel constructor
            default:
                 AppLogger.Warning("Unexpected JsonValueKind encountered directly in PopulateNodes: {ValueKind}", element.ValueKind);
                 break;
        }
    }

    // Helper to get string representation consistent with JsonNodeViewModel display
    private string GetValueString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => "{...}",
            JsonValueKind.Array => $"[...] ({element.GetArrayLength()})",
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.ToString()
        };
    }

    // Removed TrackedPaths_CollectionChanged and UpdateHasTrackedPaths methods
}