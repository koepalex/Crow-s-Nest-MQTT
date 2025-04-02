using System.Collections.ObjectModel;
using ReactiveUI; // Use ReactiveUI

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// Represents a node in the MQTT topic hierarchy TreeView.
/// </summary>
public class NodeViewModel : ReactiveObject
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private int _messageCount;
    public int MessageCount
    {
        get => _messageCount;
        set => this.RaiseAndSetIfChanged(ref _messageCount, value);
    }

    private bool _isExpanded; // Default is false
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    // Children collection for the tree structure
    public ObservableCollection<NodeViewModel> Children { get; } = new();

    // --- Structural Properties ---

    /// <summary>
    /// Gets or sets the parent node in the hierarchy. Null for root nodes.
    /// </summary>
    public NodeViewModel? Parent { get; set; } // Simple property, no change notification needed usually

    /// <summary>
    /// Gets or sets the full MQTT topic path represented by this node.
    /// </summary>
    public string FullPath { get; set; } = string.Empty; // Simple property

    // Constructor
    public NodeViewModel(string name)
    {
        _name = name; // Set initial name directly
    }

    // Parameterless constructor for XAML previewer/serializer if needed
    public NodeViewModel() : this("Default Node") { }
}