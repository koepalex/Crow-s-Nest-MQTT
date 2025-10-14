using CrowsNestMqtt.UI.ViewModels;
using System.Collections.ObjectModel;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Manages the topic tree structure for MQTT topics.
/// Handles node creation, filtering, expansion/collapse, and searching.
/// </summary>
public interface ITopicTreeManager
{
    /// <summary>
    /// Gets the root-level topic tree nodes.
    /// </summary>
    ObservableCollection<NodeViewModel> TopicTreeNodes { get; }

    /// <summary>
    /// Gets whether a topic filter is currently active.
    /// </summary>
    bool IsTopicFilterActive { get; }

    /// <summary>
    /// Updates or creates a node in the tree for the given topic.
    /// </summary>
    /// <param name="topic">Full MQTT topic path</param>
    /// <param name="incrementCount">Whether to increment the message count</param>
    void UpdateOrCreateNode(string topic, bool incrementCount = true);

    /// <summary>
    /// Updates or creates a node and increments count by a specific amount.
    /// Optimized for batch processing.
    /// </summary>
    /// <param name="topic">Full MQTT topic path</param>
    /// <param name="incrementBy">Amount to increment by</param>
    void UpdateOrCreateNodeWithCount(string topic, int incrementBy);

    /// <summary>
    /// Applies a fuzzy filter to the topic tree.
    /// </summary>
    /// <param name="filter">Filter string (null/empty to clear)</param>
    /// <returns>Number of matching nodes</returns>
    int ApplyTopicFilter(string? filter);

    /// <summary>
    /// Clears the current topic filter.
    /// </summary>
    void ClearTopicFilter();

    /// <summary>
    /// Expands all nodes in the tree.
    /// </summary>
    void ExpandAllNodes();

    /// <summary>
    /// Collapses all nodes in the tree.
    /// </summary>
    void CollapseAllNodes();

    /// <summary>
    /// Finds a node by its full topic path.
    /// </summary>
    /// <param name="topicPath">Full topic path to find</param>
    /// <returns>The node if found, null otherwise</returns>
    NodeViewModel? FindTopicNode(string topicPath);

    /// <summary>
    /// Loads initial topics from a collection (e.g., buffered messages).
    /// </summary>
    /// <param name="topics">Collection of topic paths</param>
    void LoadInitialTopics(IEnumerable<string> topics);

    /// <summary>
    /// Event raised when the filter state changes.
    /// </summary>
    event EventHandler<TopicFilterChangedEventArgs>? FilterChanged;
}

/// <summary>
/// Event args for topic filter changes.
/// </summary>
public class TopicFilterChangedEventArgs : EventArgs
{
    public string? Filter { get; init; }
    public int MatchCount { get; init; }
    public bool IsActive { get; init; }
}
