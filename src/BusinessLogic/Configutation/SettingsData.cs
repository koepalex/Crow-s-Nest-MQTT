namespace CrowsNestMqtt.BusinessLogic.Configuration;

using System.Collections.Generic;
using CrowsNestMqtt.BusinessLogic.Exporter;

public record SettingsData(
    string Hostname,
    int Port,
    string? ClientId = null,
    int KeepAliveIntervalSeconds = 60,
    bool CleanSession = true,
    uint? SessionExpiryIntervalSeconds = 300,
    AuthenticationMode? AuthMode = null,
    ExportTypes? ExportFormat = null,
    string? ExportPath = null,
    bool UseTls = false,
    int MaxTopicLimit = 500,
    int ParallelismDegree = 4,
    int TimeoutPeriodSeconds = 5)
{
    public IList<TopicBufferLimit> TopicSpecificBufferLimits { get; init; } = new List<TopicBufferLimit>();
    /// <summary>
    /// Default buffer size in bytes for topics that don't match any specific rules.
    /// If null, uses the system default (1 MB). This only applies when no "#" wildcard rule is configured.
    /// </summary>
    public long? DefaultTopicBufferSizeBytes { get; init; }
    // Ensure AuthMode is never null, defaulting to Anonymous if not provided.
    public AuthenticationMode AuthMode { get; init; } = AuthMode ?? new AnonymousAuthenticationMode();
}
