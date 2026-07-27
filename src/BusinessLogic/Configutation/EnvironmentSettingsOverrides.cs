namespace CrowsNestMqtt.BusinessLogic.Configuration;

using System;
using System.Collections.Generic;
using System.Text.Json;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.Utils;

/// <summary>
/// Reads application settings from environment variables.
/// When overrides are detected, they are applied on top of file-based settings
/// and persisted to settings.json so other tools can use them.
/// </summary>
public sealed record EnvironmentSettingsOverrides
{
    private const string Prefix = "CROWSNEST__";
    private const string AspireServicePrefix = "services__mqtt__";

    public string? Hostname { get; init; }
    public int? Port { get; init; }
    public string? ClientId { get; init; }
    public int? KeepAliveIntervalSeconds { get; init; }
    public bool? CleanSession { get; init; }
    public uint? SessionExpiryIntervalSeconds { get; init; }
    public AuthenticationMode? AuthMode { get; init; }
    public bool? UseTls { get; init; }
    public int? SubscriptionQoS { get; init; }
    public ExportTypes? ExportFormat { get; init; }
    public string? ExportPath { get; init; }
    public int? MaxTopicLimit { get; init; }
    public int? ParallelismDegree { get; init; }
    public int? TimeoutPeriodSeconds { get; init; }
    public long? DefaultTopicBufferSizeBytes { get; init; }
    public IList<TopicBufferLimit>? TopicSpecificBufferLimits { get; init; }
    public TransportProtocol? Transport { get; init; }
    public string? WebSocketPath { get; init; }
    public string? WebSocketProxyAddress { get; init; }
    public string? WebSocketProxyUsername { get; init; }
    public string? WebSocketProxyPassword { get; init; }
    public bool? ShowConnectionDialogOnLaunch { get; init; }

    /// <summary>
    /// Whether any environment variable overrides were detected.
    /// </summary>
    public bool HasOverrides { get; init; }

    /// <summary>
    /// Whether Aspire endpoint environment variables were detected (triggers auto-connect).
    /// </summary>
    public bool IsAspireEnvironment { get; init; }

    /// <summary>
    /// Reads settings from environment variables.
    /// Returns an instance with HasOverrides=false if no relevant env vars are found.
    /// </summary>
    public static EnvironmentSettingsOverrides Load()
    {
        string? hostname = null;
        int? port = null;
        bool isAspire = false;
        TransportProtocol? transport = null;
        bool? useTlsFromUri = null;
        string? webSocketPath = null;

        // Check Aspire endpoint env vars (lower priority than explicit CROWSNEST__ vars)
        // Aspire sets services__mqtt__<endpoint-name>__0 for each referenced endpoint
        var aspireEndpoint = FindAspireEndpointVar();

        if (!string.IsNullOrEmpty(aspireEndpoint))
        {
            isAspire = true;
            var parsed = ParseMqttUri(aspireEndpoint);
            hostname = parsed.hostname;
            port = parsed.port;
            transport = parsed.transport;
            useTlsFromUri = parsed.useTls;
            webSocketPath = parsed.webSocketPath;
        }

        // Read individual CROWSNEST__ env vars (these override Aspire values)
        var envHostname = ReadString("HOSTNAME");
        var envPort = ReadInt("PORT");
        var clientId = ReadStringAllowEmpty("CLIENT_ID");
        var keepAlive = ReadInt("KEEP_ALIVE_SECONDS");
        var cleanSession = ReadBool("CLEAN_SESSION");
        var sessionExpiry = ReadUInt("SESSION_EXPIRY_SECONDS");
        var useTls = ReadBool("USE_TLS");
        var subscriptionQoS = ReadInt("SUBSCRIPTION_QOS");
        var exportFormat = ReadExportFormat("EXPORT_FORMAT");
        var exportPath = ReadString("EXPORT_PATH");
        var maxTopicLimit = ReadInt("MAX_TOPIC_LIMIT");
        var parallelismDegree = ReadInt("PARALLELISM_DEGREE");
        var timeoutSeconds = ReadInt("TIMEOUT_SECONDS");
        var defaultBufferSize = ReadLong("DEFAULT_BUFFER_SIZE_BYTES");
        var topicLimits = ReadTopicBufferLimits("TOPIC_BUFFER_LIMITS");
        var authMode = ReadAuthMode();
        var envTransport = ReadTransport("TRANSPORT");
        var envWebSocketPath = ReadString("WEBSOCKET_PATH");
        var webSocketProxyAddress = ReadString("WEBSOCKET_PROXY_ADDRESS");
        var webSocketProxyUsername = ReadString("WEBSOCKET_PROXY_USERNAME");
        var webSocketProxyPassword = ReadString("WEBSOCKET_PROXY_PASSWORD");
        var showConnectionDialogOnLaunch = ReadBool("SHOW_CONNECTION_DIALOG_ON_LAUNCH");

        // Explicit CROWSNEST__ hostname/port override Aspire-derived values
        if (envHostname != null) hostname = envHostname;
        if (envPort.HasValue) port = envPort;
        if (envTransport.HasValue) transport = envTransport;
        if (envWebSocketPath != null) webSocketPath = envWebSocketPath;

        // Explicit USE_TLS overrides URI-derived value
        var finalUseTls = useTls ?? useTlsFromUri;

        bool hasAny = isAspire
            || envHostname != null || envPort.HasValue
            || clientId != null || keepAlive.HasValue
            || cleanSession.HasValue || sessionExpiry.HasValue
            || finalUseTls.HasValue || subscriptionQoS.HasValue
            || exportFormat.HasValue || exportPath != null
            || maxTopicLimit.HasValue || parallelismDegree.HasValue
            || timeoutSeconds.HasValue || defaultBufferSize.HasValue
            || topicLimits != null || authMode != null
            || transport.HasValue || webSocketPath != null
            || webSocketProxyAddress != null || webSocketProxyUsername != null
            || webSocketProxyPassword != null
            || showConnectionDialogOnLaunch.HasValue;

        if (hasAny)
        {
            AppLogger.Information("Environment variable overrides detected. Settings will be updated.");
        }

        return new EnvironmentSettingsOverrides
        {
            Hostname = hostname,
            Port = port,
            ClientId = clientId,
            KeepAliveIntervalSeconds = keepAlive,
            CleanSession = cleanSession,
            SessionExpiryIntervalSeconds = sessionExpiry,
            AuthMode = authMode,
            UseTls = finalUseTls,
            SubscriptionQoS = subscriptionQoS,
            ExportFormat = exportFormat,
            ExportPath = exportPath,
            MaxTopicLimit = maxTopicLimit,
            ParallelismDegree = parallelismDegree,
            TimeoutPeriodSeconds = timeoutSeconds,
            DefaultTopicBufferSizeBytes = defaultBufferSize,
            TopicSpecificBufferLimits = topicLimits,
            Transport = transport,
            WebSocketPath = webSocketPath,
            WebSocketProxyAddress = webSocketProxyAddress,
            WebSocketProxyUsername = webSocketProxyUsername,
            WebSocketProxyPassword = webSocketProxyPassword,
            ShowConnectionDialogOnLaunch = showConnectionDialogOnLaunch,
            HasOverrides = hasAny,
            IsAspireEnvironment = isAspire
        };
    }

    internal static (string? hostname, int? port, TransportProtocol? transport, bool? useTls, string? webSocketPath) ParseMqttUri(string connectionString)
    {
        try
        {
            var uri = new Uri(connectionString);
            if (!string.IsNullOrEmpty(uri.Host) && uri.Port > 0)
            {
                var scheme = uri.Scheme.ToLowerInvariant();
                TransportProtocol? transport = scheme switch
                {
                    "ws" or "wss" => TransportProtocol.WebSocket,
                    "mqtt" or "mqtts" or "tcp" => TransportProtocol.Tcp,
                    _ => null
                };
                bool? useTls = scheme switch
                {
                    "wss" or "mqtts" => true,
                    "ws" or "mqtt" or "tcp" => false,
                    _ => null
                };
                // Extract WebSocket path (use null if root or empty)
                string? wsPath = transport == TransportProtocol.WebSocket && uri.AbsolutePath != "/"
                    ? uri.AbsolutePath
                    : null;

                return (uri.Host, uri.Port, transport, useTls, wsPath);
            }
        }
        catch (UriFormatException ex)
        {
            AppLogger.Error(ex, "Invalid URI format for MQTT connection string: {ConnectionString}", connectionString);
        }

        return (null, null, null, null, null);
    }

    private static AuthenticationMode? ReadAuthMode()
    {
        var mode = ReadString("AUTH_MODE");
        if (mode == null) return null;

        return mode.ToLowerInvariant() switch
        {
            "anonymous" => new AnonymousAuthenticationMode(),
            "userpass" or "usernamepassword" => new UsernamePasswordAuthenticationMode(
                ReadString("AUTH_USERNAME") ?? string.Empty,
                ReadString("AUTH_PASSWORD") ?? string.Empty),
            "enhanced" => new EnhancedAuthenticationMode(
                ReadString("AUTH_METHOD"),
                ReadString("AUTH_DATA")),
            "azure" => new AzureAuthenticationMode(ReadString("AUTH_SCOPE")),
            _ => null
        };
    }

    private static string? ReadString(string name)
    {
        var value = Environment.GetEnvironmentVariable(Prefix + name);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string? ReadStringAllowEmpty(string name)
    {
        var value = Environment.GetEnvironmentVariable(Prefix + name);
        // Returns null only if the env var is not set at all
        return value;
    }

    private static int? ReadInt(string name)
    {
        var value = ReadString(name);
        if (value != null && int.TryParse(value, out var result))
            return result;
        return null;
    }

    private static uint? ReadUInt(string name)
    {
        var value = ReadString(name);
        if (value != null && uint.TryParse(value, out var result))
            return result;
        return null;
    }

    private static long? ReadLong(string name)
    {
        var value = ReadString(name);
        if (value != null && long.TryParse(value, out var result))
            return result;
        return null;
    }

    private static bool? ReadBool(string name)
    {
        var value = ReadString(name);
        if (value != null && bool.TryParse(value, out var result))
            return result;
        return null;
    }

    private static ExportTypes? ReadExportFormat(string name)
    {
        var value = ReadString(name);
        if (value != null && Enum.TryParse<ExportTypes>(value, ignoreCase: true, out var result))
            return result;
        return null;
    }

    private static IList<TopicBufferLimit>? ReadTopicBufferLimits(string name)
    {
        var value = ReadString(name);
        if (value == null) return null;

        try
        {
            var limits = JsonSerializer.Deserialize<List<TopicBufferLimit>>(value);
            return limits;
        }
        catch (JsonException ex)
        {
            AppLogger.Error(ex, "Failed to parse {EnvVar} environment variable as JSON", Prefix + name);
            return null;
        }
    }

    private static TransportProtocol? ReadTransport(string name)
    {
        var value = ReadString(name);
        if (value != null && Enum.TryParse<TransportProtocol>(value, ignoreCase: true, out var result))
            return result;
        return null;
    }

    /// <summary>
    /// Finds the first Aspire service endpoint env var matching services__mqtt__*__0.
    /// Aspire sets these based on the endpoint name (e.g., services__mqtt__mqtt__0, services__mqtt__ws__0).
    /// Prefers "mqtt" endpoint, then "default", then any other.
    /// </summary>
    private static string? FindAspireEndpointVar()
    {
        // Check well-known names first for deterministic priority
        var mqtt = Environment.GetEnvironmentVariable(AspireServicePrefix + "mqtt__0");
        if (!string.IsNullOrEmpty(mqtt)) return mqtt;

        var defaultVar = Environment.GetEnvironmentVariable(AspireServicePrefix + "default__0");
        if (!string.IsNullOrEmpty(defaultVar)) return defaultVar;

        // Fall back to scanning for any services__mqtt__*__0 pattern
        var envVars = Environment.GetEnvironmentVariables();
        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            var key = entry.Key?.ToString();
            if (key != null
                && key.StartsWith(AspireServicePrefix, StringComparison.OrdinalIgnoreCase)
                && key.EndsWith("__0", StringComparison.Ordinal))
            {
                return entry.Value?.ToString();
            }
        }
        return null;
    }
}
