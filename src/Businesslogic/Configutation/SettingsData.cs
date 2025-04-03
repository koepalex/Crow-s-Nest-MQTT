namespace CrowsNestMqtt.Businesslogic.Configuration;

public  record SettingsData(
    string Hostname,
    int Port,
    string? ClientId,
    int KeepAliveIntervalSeconds,
    bool CleanSession,
    uint? SessionExpiryIntervalSeconds
);