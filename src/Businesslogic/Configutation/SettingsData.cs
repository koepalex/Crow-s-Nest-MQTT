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
    string? AuthenticationMethod = null,
    string? AuthenticationData = null)
{
    public IList<TopicBufferLimit> TopicSpecificBufferLimits { get; init; } = new List<TopicBufferLimit>();
    // Ensure AuthMode is never null, defaulting to Anonymous if not provided.
    public AuthenticationMode AuthMode { get; init; } = AuthMode ?? new AnonymousAuthenticationMode();
}