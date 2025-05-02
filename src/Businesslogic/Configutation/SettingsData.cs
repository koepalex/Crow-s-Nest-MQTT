namespace CrowsNestMqtt.BusinessLogic.Configuration;

using CrowsNestMqtt.BusinessLogic.Exporter;

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