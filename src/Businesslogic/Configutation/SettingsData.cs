using CrowsNestMqtt.Businesslogic.Exporter; // Added for ExportTypes

namespace CrowsNestMqtt.Businesslogic.Configuration;

public  record SettingsData(
    string Hostname,
    int Port,
    string? ClientId,
    int KeepAliveIntervalSeconds,
    bool CleanSession,
    uint? SessionExpiryIntervalSeconds,
    ExportTypes? ExportFormat = null, // Changed type to ExportTypes?
    string? ExportPath = null
);