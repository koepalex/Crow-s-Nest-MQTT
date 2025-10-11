using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CrowsNestMqtt.Utils
{
    /// <summary>
    /// Represents a single node in the JSON tree visualization.
    /// Used for JSON viewer with automatic expansion up to depth 5.
    /// </summary>
    public class JsonTreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded;

        /// <summary>
        /// Property name (for objects) or index (for arrays) or "root" for top-level.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The JSON value (string, number, bool, null, or complex type indicator).
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// Type discriminator (Object, Array, String, Number, True, False, Null).
        /// </summary>
        public JsonValueKind ValueKind { get; set; }

        /// <summary>
        /// Nesting level (1-based: root = 1, root's children = 2, etc.).
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Expansion state for UI binding.
        /// Auto-set to true for depth less than or equal to 5 and expandable nodes.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether node has children (Object/Array with items).
        /// </summary>
        public bool IsExpandable { get; set; }

        /// <summary>
        /// Child nodes (for Object/Array types).
        /// </summary>
        public ObservableCollection<JsonTreeNode> Children { get; set; } = new();

        /// <summary>
        /// Reference to parent node (null for root).
        /// </summary>
        public JsonTreeNode? Parent { get; set; }

        /// <summary>
        /// Constructor that sets IsExpanded based on depth and expandability.
        /// FR-008: IsExpanded = true when Depth is 5 or less AND IsExpandable = true
        /// </summary>
        public JsonTreeNode()
        {
            // IsExpanded will be set after Depth and IsExpandable are assigned
        }

        /// <summary>
        /// Initialize expansion state after properties are set.
        /// Call this after Depth and IsExpandable are assigned.
        /// </summary>
        public void InitializeExpansionState()
        {
            // FR-008: IsExpanded = true when Depth <= 5 AND IsExpandable = true
            _isExpanded = Depth <= 5 && IsExpandable;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
