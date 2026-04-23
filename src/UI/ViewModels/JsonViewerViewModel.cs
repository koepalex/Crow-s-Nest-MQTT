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
    /// <summary>
    /// Maximum depth at which nodes are auto-expanded (1-based).
    /// </summary>
    public const int AutoExpandMaxDepth = 5;

    /// <summary>
    /// Maximum total descendant node count for a subtree to be auto-expanded.
    /// Subtrees whose descendant count exceeds this budget remain collapsed on load,
    /// preventing the TreeView from materializing thousands of visuals synchronously
    /// when a large / deeply-nested JSON payload is displayed.
    /// The user can still click to expand these subtrees manually.
    /// </summary>
    public const int AutoExpandNodeBudget = 500;

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
                var arrayRootNode = new JsonNodeViewModel("$", jsonDoc.RootElement, "$") { Depth = 0 };
                RootNodes.Add(arrayRootNode);
                // Populate the children of this array node (depth 1)
                var arraySubtreeCount = PopulateNodes(jsonDoc.RootElement, arrayRootNode.Children, arrayRootNode.JsonPath, 1);
                // The array-root itself is at depth 0 (always within AutoExpandMaxDepth),
                // but still guard against auto-expanding huge arrays that would force the
                // TreeView to materialize thousands of items synchronously.
                arrayRootNode.IsExpanded = arraySubtreeCount <= AutoExpandNodeBudget;
            }
            else
            {
                // Original behavior for root objects or other types (start at depth 1)
                PopulateNodes(jsonDoc.RootElement, RootNodes, "$", 1); // Start path at root '$'
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

    /// <summary>
    /// Recursively materializes JsonNodeViewModels for the given element and returns
    /// the total count of descendant nodes produced (excluding the elements added
    /// into <paramref name="children"/> themselves? -- see remarks).
    /// </summary>
    /// <remarks>
    /// Returned value is the total number of JsonNodeViewModel instances appended
    /// to <paramref name="children"/> (including transitive grandchildren). It is
    /// used to decide whether a container node is cheap enough to auto-expand
    /// (see <see cref="AutoExpandNodeBudget"/>). The rule applied to each container
    /// child is: expand when <c>depth &lt;= AutoExpandMaxDepth</c> AND the container's
    /// own descendant count fits the budget. This keeps the familiar depth-based UX
    /// for small JSON payloads while preventing the TreeView from synchronously
    /// materializing thousands of TreeViewItems for large / deeply-nested payloads.
    /// </remarks>
    private int PopulateNodes(JsonElement element, ObservableCollection<JsonNodeViewModel> children, string currentPath, int depth)
    {
        int totalAdded = 0;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    string safeName = property.Name;
                    string childPath = $"{currentPath}.{safeName}";
                    var node = new JsonNodeViewModel(property.Name, property.Value, childPath) { Depth = depth };

                    children.Add(node);
                    totalAdded++;

                    bool isContainer = property.Value.ValueKind == JsonValueKind.Object
                                       || property.Value.ValueKind == JsonValueKind.Array;
                    if (isContainer)
                    {
                        int descendantCount = PopulateNodes(property.Value, node.Children, node.JsonPath, depth + 1);
                        totalAdded += descendantCount;

                        // FR-008: Auto-expand up to depth 5, but only if the subtree
                        // is small enough to render cheaply. Larger subtrees stay
                        // collapsed on load and the user can expand them manually.
                        node.IsExpanded = depth <= AutoExpandMaxDepth
                                          && descendantCount <= AutoExpandNodeBudget;
                    }
                    else
                    {
                        node.IsExpanded = false;
                    }
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string childPath = $"{currentPath}[{index}]";
                    var node = new JsonNodeViewModel($"[{index}]", item, childPath) { Depth = depth };

                    children.Add(node);
                    totalAdded++;

                    bool isContainer = item.ValueKind == JsonValueKind.Object
                                       || item.ValueKind == JsonValueKind.Array;
                    if (isContainer)
                    {
                        int descendantCount = PopulateNodes(item, node.Children, node.JsonPath, depth + 1);
                        totalAdded += descendantCount;

                        node.IsExpanded = depth <= AutoExpandMaxDepth
                                          && descendantCount <= AutoExpandNodeBudget;
                    }
                    else
                    {
                        node.IsExpanded = false;
                    }
                    index++;
                }
                break;

            // Value types (string, number, boolean, null) are handled within JsonNodeViewModel constructor
            default:
                 AppLogger.Warning("Unexpected JsonValueKind encountered directly in PopulateNodes: {ValueKind}", element.ValueKind);
                 break;
        }
        return totalAdded;
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