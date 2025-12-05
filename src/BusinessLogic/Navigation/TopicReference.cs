namespace CrowsNestMQTT.BusinessLogic.Navigation;

using System;

/// <summary>
/// Lightweight reference to a topic in the MQTT topic tree.
/// Used for search results without duplicating full topic data.
/// Immutable value object with equality based on TopicId.
/// </summary>
public sealed class TopicReference : IEquatable<TopicReference>
{
    /// <summary>
    /// Gets the full MQTT topic path (e.g., "sensor/temperature/bedroom").
    /// </summary>
    public string TopicPath { get; }

    /// <summary>
    /// Gets the human-readable display name for the topic.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the internal identifier linking to the full Topic entity.
    /// </summary>
    public Guid TopicId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicReference"/> class.
    /// </summary>
    /// <param name="topicPath">Full MQTT topic path</param>
    /// <param name="displayName">Human-readable topic name</param>
    /// <param name="topicId">Internal topic identifier</param>
    /// <exception cref="ArgumentException">If topicPath or displayName is null or empty</exception>
    public TopicReference(string topicPath, string displayName, Guid topicId)
    {
        if (string.IsNullOrWhiteSpace(topicPath))
        {
            throw new ArgumentException("Topic path cannot be null or empty.", nameof(topicPath));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be null or empty.", nameof(displayName));
        }

        if (topicId == Guid.Empty)
        {
            throw new ArgumentException("Topic ID cannot be empty.", nameof(topicId));
        }

        TopicPath = topicPath;
        DisplayName = displayName;
        TopicId = topicId;
    }

    /// <summary>
    /// Determines whether this TopicReference is equal to another.
    /// Equality is based on TopicId only.
    /// </summary>
    public bool Equals(TopicReference? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return TopicId == other.TopicId;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as TopicReference);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return TopicId.GetHashCode();
    }

    /// <summary>
    /// Returns the topic path as the string representation.
    /// </summary>
    public override string ToString()
    {
        return TopicPath;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TopicReference? left, TopicReference? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TopicReference? left, TopicReference? right)
    {
        return !(left == right);
    }
}
