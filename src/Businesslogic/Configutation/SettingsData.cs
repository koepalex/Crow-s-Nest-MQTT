namespace CrowsNestMqtt.BusinessLogic.Configuration;

using CrowsNestMqtt.BusinessLogic.Exporter;

public  record SettingsData(
    string Hostname,
    int Port,
    string? ClientId,
    int KeepAliveIntervalSeconds,
    bool CleanSession,
    uint? SessionExpiryIntervalSeconds,
    // string? Username, // Removed
    // string? Password, // Removed
    AuthenticationMode? AuthMode = null, // Added AuthMode, default to null to be handled by constructor logic
    ExportTypes? ExportFormat = null, // Changed type to ExportTypes?
    string? ExportPath = null
)
{
    // Ensure AuthMode is never null, defaulting to Anonymous if not provided.
    public AuthenticationMode AuthMode { get; init; } = AuthMode ?? new AnonymousAuthenticationMode();
}