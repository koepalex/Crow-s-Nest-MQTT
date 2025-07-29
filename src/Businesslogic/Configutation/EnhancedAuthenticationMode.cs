namespace CrowsNestMqtt.BusinessLogic.Configuration;

/// <summary>
/// Represents enhanced authentication mode with method and data.
/// </summary>
public sealed record EnhancedAuthenticationMode(
    string? AuthenticationMethod,
    string? AuthenticationData
) : AuthenticationMode;
