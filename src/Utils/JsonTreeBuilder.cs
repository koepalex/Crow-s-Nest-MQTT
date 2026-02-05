using System.Text.Json;

namespace CrowsNestMqtt.Utils
{
    /// <summary>
    /// Utility class for constructing JsonTreeNode hierarchy from JSON with automatic expansion up to depth 5.
    /// Thread-safe and stateless - safe for concurrent use.
    /// </summary>
    public class JsonTreeBuilder
    {

        /// <summary>
        /// Builds a JsonTreeNode tree from a JsonDocument.
        /// </summary>
        /// <param name="document">The JSON document to parse (must not be null)</param>
        /// <returns>Root JsonTreeNode with depth = 1</returns>
        public JsonTreeNode BuildTree(JsonDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            return BuildTreeRecursive(document.RootElement, "root", 1, null);
        }

        /// <summary>
        /// Recursively builds the tree structure from a JsonElement.
        /// </summary>
        /// <param name="element">The JSON element to process</param>
        /// <param name="key">The key for this node</param>
        /// <param name="depth">Current depth (1-based)</param>
        /// <param name="parent">Parent node (null for root)</param>
        /// <returns>JsonTreeNode representing this element</returns>
        private static JsonTreeNode BuildTreeRecursive(JsonElement element, string key, int depth, JsonTreeNode? parent)
        {
            var node = new JsonTreeNode
            {
                Key = key,
                ValueKind = element.ValueKind,
                Depth = depth,
                Parent = parent
            };

            // Determine expandability and populate children based on value kind
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var properties = element.EnumerateObject().ToList();
                    node.IsExpandable = properties.Count > 0;
                    node.Value = properties.Count > 0 ? "{...}" : "{}";

                    if (node.IsExpandable)
                    {
                        foreach (var property in properties)
                        {
                            var childNode = BuildTreeRecursive(property.Value, property.Name, depth + 1, node);
                            node.Children.Add(childNode);
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    var arrayLength = element.GetArrayLength();
                    node.IsExpandable = arrayLength > 0;
                    node.Value = arrayLength > 0 ? $"[...] ({arrayLength})" : "[]";

                    if (node.IsExpandable)
                    {
                        int index = 0;
                        foreach (var item in element.EnumerateArray())
                        {
                            var childNode = BuildTreeRecursive(item, $"[{index}]", depth + 1, node);
                            node.Children.Add(childNode);
                            index++;
                        }
                    }
                    break;

                case JsonValueKind.String:
                    node.IsExpandable = false;
                    node.Value = $"\"{element.GetString()}\"";
                    break;

                case JsonValueKind.Number:
                    node.IsExpandable = false;
                    node.Value = element.GetRawText();
                    break;

                case JsonValueKind.True:
                    node.IsExpandable = false;
                    node.Value = "true";
                    break;

                case JsonValueKind.False:
                    node.IsExpandable = false;
                    node.Value = "false";
                    break;

                case JsonValueKind.Null:
                    node.IsExpandable = false;
                    node.Value = "null";
                    break;

                default:
                    node.IsExpandable = false;
                    node.Value = element.ToString();
                    break;
            }

            // Set IsExpanded based on depth and expandability
            // FR-008: IsExpanded = true when Depth <= MaxAutoExpandDepth (5) AND IsExpandable = true
            node.InitializeExpansionState();

            return node;
        }
    }
}
