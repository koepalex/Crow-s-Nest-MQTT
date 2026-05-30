namespace CrowsNestMqtt.BusinessLogic.Configuration;

/// <summary>
/// Defines a topic filter that should be automatically logged to SQLite.
/// </summary>
public record AutoLogTopicRule(string TopicFilter, bool IsEnabled = true);
