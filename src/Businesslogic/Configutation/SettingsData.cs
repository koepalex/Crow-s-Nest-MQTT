namespace CrowsNestMqtt.BusinessLogic.Configuration;

using System.Collections.Generic;
using CrowsNestMqtt.BusinessLogic.Exporter;
using Businesslogic.Configuration; // Added for TopicBufferLimit

public record SettingsData(
    string Hostname,
    int Port,
    string? ClientId,
    int KeepAliveIntervalSeconds,
    bool CleanSession,
    uint? SessionExpiryIntervalSeconds,
    ExportTypes? ExportFormat = null,
    string? ExportPath = null)
{
    public IList<TopicBufferLimit> TopicSpecificBufferLimits { get; init; } = new List<TopicBufferLimit>();
}