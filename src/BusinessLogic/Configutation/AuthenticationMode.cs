namespace CrowsNestMqtt.BusinessLogic.Configuration;

using System.Text.Json.Serialization;

/// <summary>
/// Base class for different MQTT authentication modes.
/// Enables polymorphic serialization/deserialization for settings.
/// </summary>
[JsonDerivedType(typeof(AnonymousAuthenticationMode), typeDiscriminator: "anonymous")]
[JsonDerivedType(typeof(UsernamePasswordAuthenticationMode), typeDiscriminator: "userpass")]
[JsonDerivedType(typeof(EnhancedAuthenticationMode), typeDiscriminator: "enhanced")]
public abstract record AuthenticationMode;

/// <summary>
/// Represents anonymous authentication (no username or password).
/// </summary>
public sealed record AnonymousAuthenticationMode : AuthenticationMode;

/// <summary>
/// Represents authentication using a username and password.
/// </summary>
/// <param name="Username">The MQTT username.</param>
/// <param name="Password">The MQTT password.</param>
public sealed record UsernamePasswordAuthenticationMode(string Username, string Password) : AuthenticationMode;
