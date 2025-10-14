using CrowsNestMqtt.UI.ViewModels;
using FuzzySharp;
using Serilog;
using System.Collections.ObjectModel;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Manages the topic tree structure for MQTT topics.
/// </summary>
public class TopicTreeManager : ITopicTreeManager
{
    private bool _isTopicFilterActive;

    public ObservableCollection<NodeViewModel> TopicTreeNodes { get; } = new();

    public bool IsTopicFilterActive
    {
        get => _isTopicFilterActive;
        private set
        {
            if (_isTopicFilterActive != value)
            {
                _isTopicFilterActive = value;
            }
        }
    }

    public event EventHandler<TopicFilterChangedEventArgs>? FilterChanged;

    public void UpdateOrCreateNode(string topic, bool incrementCount = true)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return;

        var parts = topic.Split('/');
        ObservableCollection<NodeViewModel> currentLevel = TopicTreeNodes;
        NodeViewModel? parentNode = null;
        string currentPath = "";

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part)) continue;

            currentPath = (i == 0) ? part : $"{currentPath}/{part}";

            var existingNode = currentLevel.FirstOrDefault(n => n.Name == part);

            if (existingNode == null)
            {
                existingNode = new NodeViewModel(part, parentNode) { FullPath = currentPath };

                // Insert in sorted order
                int insertIndex = 0;
                while (insertIndex < currentLevel.Count &&
                       string.Compare(currentLevel[insertIndex].Name, existingNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertIndex++;
                }
                currentLevel.Insert(insertIndex, existingNode);
            }

            // Increment count only for final node if requested
            if (i == parts.Length - 1 && incrementCount)
            {
                existingNode.IncrementMessageCount();
            }

            currentLevel = existingNode.Children;
            parentNode = existingNode;
        }
    }

    public void UpdateOrCreateNodeWithCount(string topic, int incrementBy)
    {
        if (string.IsNullOrWhiteSpace(topic) || incrementBy <= 0)
            return;

        var parts = topic.Split('/');
        ObservableCollection<NodeViewModel> currentLevel = TopicTreeNodes;
        NodeViewModel? parentNode = null;
        string currentPath = "";

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part)) continue;

            currentPath = (i == 0) ? part : $"{currentPath}/{part}";

            var existingNode = currentLevel.FirstOrDefault(n => n.Name == part);

            if (existingNode == null)
            {
                existingNode = new NodeViewModel(part, parentNode) { FullPath = currentPath };

                int insertIndex = 0;
                while (insertIndex < currentLevel.Count &&
                       string.Compare(currentLevel[insertIndex].Name, existingNode.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    insertIndex++;
                }
                currentLevel.Insert(insertIndex, existingNode);
            }

            // Increment by specified amount for final node
            if (i == parts.Length - 1)
            {
                for (int j = 0; j < incrementBy; j++)
                {
                    existingNode.IncrementMessageCount();
                }
            }

            currentLevel = existingNode.Children;
            parentNode = existingNode;
        }
    }

    public int ApplyTopicFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            ClearTopicFilter();
            return TopicTreeNodes.Count;
        }

        Log.Information("Applying topic filter: '{Filter}'", filter);
        int matchCount = 0;
        SetNodeVisibilityRecursive(TopicTreeNodes, isVisible: false, clearFilter: false, ref matchCount, filter);

        IsTopicFilterActive = true;
        FilterChanged?.Invoke(this, new TopicFilterChangedEventArgs
        {
            Filter = filter,
            MatchCount = matchCount,
            IsActive = true
        });

        return matchCount;
    }

    public void ClearTopicFilter()
    {
        int dummyMatchCount = 0;
        SetNodeVisibilityRecursive(TopicTreeNodes, isVisible: true, clearFilter: true, ref dummyMatchCount);

        IsTopicFilterActive = false;
        FilterChanged?.Invoke(this, new TopicFilterChangedEventArgs
        {
            Filter = null,
            MatchCount = 0,
            IsActive = false
        });

        Log.Information("Topic filter cleared.");
    }

    public void ExpandAllNodes()
    {
        Log.Information("Expand all nodes command executed.");
        SetNodeExpandedRecursive(TopicTreeNodes, true);
    }

    public void CollapseAllNodes()
    {
        Log.Information("Collapse all nodes command executed.");
        SetNodeExpandedRecursive(TopicTreeNodes, false);
    }

    public NodeViewModel? FindTopicNode(string topicPath)
    {
        if (string.IsNullOrWhiteSpace(topicPath))
            return null;

        var normalizedPath = topicPath.Trim().TrimEnd('/');
        return FindTopicNodeRecursive(TopicTreeNodes, normalizedPath);
    }

    public void LoadInitialTopics(IEnumerable<string> topics)
    {
        foreach (var topic in topics)
        {
            UpdateOrCreateNode(topic, incrementCount: false);
        }
    }

    private NodeViewModel? FindTopicNodeRecursive(IEnumerable<NodeViewModel> nodes, string topicPath)
    {
        foreach (var node in nodes)
        {
            var normalizedNodePath = node.FullPath?.Trim().TrimEnd('/');
            if (string.Equals(normalizedNodePath, topicPath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (!string.IsNullOrEmpty(normalizedNodePath) &&
                topicPath.StartsWith(normalizedNodePath + "/", StringComparison.OrdinalIgnoreCase))
            {
                var childResult = FindTopicNodeRecursive(node.Children, topicPath);
                if (childResult != null)
                    return childResult;
            }
        }
        return null;
    }

    private bool SetNodeVisibilityRecursive(
        IEnumerable<NodeViewModel> nodes,
        bool isVisible,
        bool clearFilter,
        ref int matchCount,
        string? filter = null)
    {
        bool anyChildVisible = false;

        foreach (var node in nodes)
        {
            bool nodeMatches = false;

            if (!clearFilter && !string.IsNullOrEmpty(node.FullPath) && !string.IsNullOrEmpty(filter))
            {
                var segments = node.FullPath.Split('/');
                nodeMatches = segments.Any(segment =>
                    !string.IsNullOrEmpty(segment) &&
                    Fuzz.PartialRatio(segment.ToLowerInvariant(), filter.ToLowerInvariant()) > 80);
            }

            bool childVisible = SetNodeVisibilityRecursive(node.Children, isVisible, clearFilter, ref matchCount, filter);

            if (clearFilter)
            {
                node.IsVisible = true;
                anyChildVisible = true;
            }
            else
            {
                node.IsVisible = nodeMatches || childVisible;
                if (node.IsVisible)
                {
                    anyChildVisible = true;
                    if (nodeMatches)
                    {
                        matchCount++;
                    }
                }
            }
        }

        return anyChildVisible;
    }

    private void SetNodeExpandedRecursive(IEnumerable<NodeViewModel> nodes, bool isExpanded)
    {
        foreach (var node in nodes)
        {
            node.IsExpanded = isExpanded;
            if (node.Children.Any())
            {
                SetNodeExpandedRecursive(node.Children, isExpanded);
            }
        }
    }
}
