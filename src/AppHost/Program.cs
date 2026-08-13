using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Microsoft.IdentityModel.Tokens;

var builder = DistributedApplication.CreateBuilder(args);

// Add EMQX MQTT v5 broker as a Docker container with dynamic ports
var mqttBroker = builder.AddContainer("mqtt", "emqx/emqx", "latest")
    .WithEndpoint(targetPort: 1883, name: "mqtt", scheme: "mqtt")
    .WithEndpoint(targetPort: 8083, name: "ws", scheme: "ws")
    .WithEndpoint(targetPort: 8883, name: "mqtts", scheme: "tcp")
    .WithHttpEndpoint(targetPort: 18083, name: "dashboard")
    .WithEnvironment("EMQX_AUTHORIZATION__NO_MATCH", "allow")
    .WithEnvironment("EMQX_AUTHORIZATION__SOURCES", "[]") // Disable file-based ACL to allow subscribe to #
    .WithEnvironment("EMQX_LISTENERS__TCP__DEFAULT__BIND", "0.0.0.0:1883")
    .WithEnvironment("EMQX_LISTENERS__WS__DEFAULT__BIND", "0.0.0.0:8083")
    .WithEnvironment("EMQX_LISTENERS__SSL__DEFAULT__BIND", "0.0.0.0:8883")
    .WithEnvironment("EMQX_MQTT__MAX_PACKET_SIZE", "10MB")
    .WithHttpHealthCheck("/status", endpointName: "dashboard");

// Get the MQTT endpoints for passing to the client applications
var mqttEndpoint = mqttBroker.GetEndpoint("mqtt");
var wsEndpoint = mqttBroker.GetEndpoint("ws");
var mqttsEndpoint = mqttBroker.GetEndpoint("mqtts");

// Forward proxy used to exercise WebSocket proxy configuration locally.
var webSocketProxy = builder.AddContainer("websocket-proxy", "ubuntu/squid", "latest")
    .WithBindMount("./squid/crowsnest.conf", "/etc/squid/conf.d/crowsnest.conf", isReadOnly: true)
    .WithEndpoint(targetPort: 3128, name: "http", scheme: "http");
var webSocketProxyEndpoint = webSocketProxy.GetEndpoint("http");

// Build configuration the AppHost itself was compiled with, so that
// 'dotnet run --configuration Release' picks up Release outputs. Aspire's
// Environment.EnvironmentName is "Development" by default and doesn't reflect
// the actual build config, so infer it from where the AppHost assembly was
// loaded from.
var buildConfiguration =
    Path.GetFileName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
        is "Release" or "release" ? "Release"
        : Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!)
            is "Release" or "release" ? "Release"
        : "Debug";

// -------------------------------------------------------------------------
// Azure Event Grid emulation (local testing only, no real Azure resources).
// -------------------------------------------------------------------------
// The mock broker accepts CONNECT packets whose AuthenticationMethod is
// "OAUTH2-JWT" and whose AuthenticationData is a well-formed JWT. Signatures
// are not verified, so any locally-signed JWT works. We synthesize a fresh JWT
// per AppHost run and pass it to the client via CROWSNEST__AZURE_TOKEN_OVERRIDE,
// which short-circuits DefaultAzureCredential.
const int mockEgPort = 51883; // Fixed so the client can reference it via env var
var mockBrokerDllPath = Path.GetFullPath(Path.Combine(
    builder.AppHostDirectory,
    "..", "..", "tools", "MockAzureEventGridBroker", "bin",
    buildConfiguration,
    "net10.0",
    "CrowsNestMqtt.MockAzureEventGridBroker.dll"));
var mockBrokerWorkingDir = Path.GetDirectoryName(mockBrokerDllPath)!;

var mockEgBroker = builder.AddExecutable(
        "mock-eg-broker",
        "dotnet",
        mockBrokerWorkingDir,
        mockBrokerDllPath)
    .WithEnvironment("MOCK_EG_HOST", "127.0.0.1")
    .WithEnvironment("MOCK_EG_PORT", mockEgPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
    .WithEnvironment("MOCK_EG_USE_TLS", "true")
    // Expose the listener as an Aspire endpoint so the dashboard shows the URL
    // and downstream resources can reference it via .GetEndpoint("mqtts").
    .WithEndpoint(port: mockEgPort, targetPort: mockEgPort, name: "mqtts", scheme: "tcp", isProxied: false);

var mockEgEndpoint = mockEgBroker.GetEndpoint("mqtts");

var devJwt = CreateDevJwt();

// -------------------------------------------------------------------------
// Client launch strategy: project resource vs. plain executable.
// -------------------------------------------------------------------------
// Visual Studio starts Aspire *project* resources through its own IDE
// "run session" protocol instead of launching them itself. That protocol
// currently fails for non-web (WinExe/Avalonia) projects, and it also cannot
// start the same project more than once - this AppHost starts
// CrowsNestMqtt.App five times. The symptom in the dashboard is:
//   [sys] run session could not be started: IDE returned a response indicating failure
// See https://github.com/microsoft/aspire/issues/13852 and
//     https://github.com/microsoft/aspire/issues/11837.
// Starting the already-built client executable directly bypasses the IDE
// protocol and behaves exactly like the (working) `dotnet run` CLI flow. Use
// Debug > Attach to Process to debug a client instance in that mode.
// Force a mode with CROWSNEST_ASPIRE_CLIENT_LAUNCH=project|executable.
var clientRuntimeIdentifier = OperatingSystem.IsLinux() ? "linux-x64" : null;
var clientExecutablePath = Path.GetFullPath(Path.Combine(
    builder.AppHostDirectory,
    "..", "MainApp", "bin", buildConfiguration, "net10.0",
    clientRuntimeIdentifier ?? string.Empty,
    OperatingSystem.IsWindows() ? "CrowsNestMqtt.App.exe" : "CrowsNestMqtt.App"));

var launchModeOverride = Environment.GetEnvironmentVariable("CROWSNEST_ASPIRE_CLIENT_LAUNCH");
var launchClientsAsExecutable = launchModeOverride switch
{
    not null when launchModeOverride.Equals("executable", StringComparison.OrdinalIgnoreCase) => true,
    not null when launchModeOverride.Equals("project", StringComparison.OrdinalIgnoreCase) => false,
    // Linux native LibVLC assets are emitted only for the linux-x64 target.
    _ when OperatingSystem.IsLinux() => true,
    // Visual Studio sets VSAPPIDNAME/VSAPPIDDIR for processes it launches.
    _ => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSAPPIDNAME"))
         || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSAPPIDDIR"))
};

if (launchClientsAsExecutable && !File.Exists(clientExecutablePath))
{
    Console.WriteLine(
        $"[apphost] Client executable not found at '{clientExecutablePath}'; falling back to project resources.");
    launchClientsAsExecutable = false;
}

if (launchClientsAsExecutable)
{
    AddClientInstances(name => builder.AddExecutable(
        name,
        clientExecutablePath,
        Path.GetDirectoryName(clientExecutablePath)!));
}
else
{
    AddClientInstances(name => builder.AddProject<Projects.CrowsNestMqtt_App>(name));
}

// Add delayed test data sender that publishes sample data after broker is ready
var toolsDir = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "tools"));
builder.AddExecutable("test-data-sender", "pwsh", toolsDir, "-File", "SendTestDataDelayed.ps1")
    .WithReference(mqttEndpoint)
    .WaitFor(mqttBroker)
    .WithEnvironment("MQTT_HOST", mqttEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("MQTT_PORT", mqttEndpoint.Property(EndpointProperty.Port))
    .WithEnvironment("MQTT_USE_TLS", "false");

builder.Build().Run();

// Creates every CrowsNestMqtt client instance using the supplied factory, which
// produces either a project resource or an executable resource depending on the
// selected launch strategy. Both resource kinds support environment variables,
// endpoint references and wait dependencies, so the configuration is identical.
void AddClientInstances<T>(Func<string, IResourceBuilder<T>> addClient)
    where T : IResource, IResourceWithEnvironment, IResourceWithWaitSupport
{
    // Shared settings for all anonymous CrowsNestMqtt instances
    static IResourceBuilder<TResource> WithSharedEnvironment<TResource>(IResourceBuilder<TResource> rb)
        where TResource : IResource, IResourceWithEnvironment => rb
        .WithEnvironment("CROWSNEST__AUTH_MODE", "anonymous")
        .WithEnvironment("CROWSNEST__CLIENT_ID", "")
        .WithEnvironment("CROWSNEST__KEEP_ALIVE_SECONDS", "0")
        .WithEnvironment("CROWSNEST__CLEAN_SESSION", "true")
        .WithEnvironment("CROWSNEST__SESSION_EXPIRY_SECONDS", "0")
        .WithEnvironment("CROWSNEST__SUBSCRIPTION_QOS", "1")
        .WithEnvironment("CROWSNEST__SHOW_CONNECTION_DIALOG_ON_LAUNCH", "false")
        .WithEnvironment("CROWSNEST__TOPIC_BUFFER_LIMITS", """[{"TopicFilter":"#","MaxSizeBytes":11048576}]""");

    // Auto-connects to the MQTT broker via TCP
    WithSharedEnvironment(addClient("crows-nest-mqtt")
        .WithReference(mqttEndpoint)
        .WaitFor(mqttBroker)
        .WithEnvironment("CROWSNEST__HOSTNAME", mqttEndpoint.Property(EndpointProperty.Host))
        .WithEnvironment("CROWSNEST__PORT", mqttEndpoint.Property(EndpointProperty.Port))
        .WithEnvironment("CROWSNEST__USE_TLS", "false"));

    // Connects via WebSocket
    WithSharedEnvironment(addClient("crows-nest-mqtt-ws")
        .WithReference(wsEndpoint)
        .WaitFor(mqttBroker)
        .WithEnvironment("CROWSNEST__HOSTNAME", wsEndpoint.Property(EndpointProperty.Host))
        .WithEnvironment("CROWSNEST__PORT", wsEndpoint.Property(EndpointProperty.Port))
        .WithEnvironment("CROWSNEST__USE_TLS", "false")
        .WithEnvironment("CROWSNEST__TRANSPORT", "WebSocket")
        .WithEnvironment("CROWSNEST__WEBSOCKET_PATH", "/mqtt"));

    // Reaches the broker through the Squid forward proxy. The broker hostname is
    // its container resource name because Squid resolves the destination inside
    // the Aspire container network.
    WithSharedEnvironment(addClient("crows-nest-mqtt-ws-proxy")
        .WithReference(wsEndpoint)
        .WithReference(webSocketProxyEndpoint)
        .WaitFor(mqttBroker)
        .WaitFor(webSocketProxy)
        .WithEnvironment("CROWSNEST__HOSTNAME", "mqtt")
        .WithEnvironment("CROWSNEST__PORT", "8083")
        .WithEnvironment("CROWSNEST__USE_TLS", "false")
        .WithEnvironment("CROWSNEST__TRANSPORT", "WebSocket")
        .WithEnvironment("CROWSNEST__WEBSOCKET_PATH", "/mqtt")
        .WithEnvironment(
            "CROWSNEST__WEBSOCKET_PROXY_ADDRESS",
            ReferenceExpression.Create(
                $"http://{webSocketProxyEndpoint.Property(EndpointProperty.Host)}:{webSocketProxyEndpoint.Property(EndpointProperty.Port)}")));

    // Connects via TLS
    WithSharedEnvironment(addClient("crows-nest-mqtt-tls")
        .WithReference(mqttsEndpoint)
        .WaitFor(mqttBroker)
        .WithEnvironment("CROWSNEST__HOSTNAME", mqttsEndpoint.Property(EndpointProperty.Host))
        .WithEnvironment("CROWSNEST__PORT", mqttsEndpoint.Property(EndpointProperty.Port))
        .WithEnvironment("CROWSNEST__USE_TLS", "true"));

    // Connects to the Azure Event Grid mock. Note: WithSharedEnvironment is
    // intentionally NOT used here — it would overwrite CROWSNEST__AUTH_MODE=azure
    // with "anonymous". The buffer/session defaults are set individually instead.
    addClient("crows-nest-mqtt-azure")
        .WithReference(mockEgEndpoint)
        .WaitFor(mockEgBroker)
        .WithEnvironment("CROWSNEST__HOSTNAME", mockEgEndpoint.Property(EndpointProperty.Host))
        .WithEnvironment("CROWSNEST__PORT", mockEgEndpoint.Property(EndpointProperty.Port))
        .WithEnvironment("CROWSNEST__USE_TLS", "true")
        .WithEnvironment("CROWSNEST__AUTH_MODE", "azure")
        .WithEnvironment("CROWSNEST__AUTH_SCOPE", "https://eventgrid.azure.net/.default")
        .WithEnvironment("CROWSNEST__AZURE_TOKEN_OVERRIDE", devJwt)
        .WithEnvironment("CROWSNEST__CLIENT_ID", "")
        .WithEnvironment("CROWSNEST__KEEP_ALIVE_SECONDS", "0")
        .WithEnvironment("CROWSNEST__CLEAN_SESSION", "true")
        .WithEnvironment("CROWSNEST__SESSION_EXPIRY_SECONDS", "0")
        .WithEnvironment("CROWSNEST__SUBSCRIPTION_QOS", "1")
        .WithEnvironment("CROWSNEST__SHOW_CONNECTION_DIALOG_ON_LAUNCH", "false")
        .WithEnvironment("CROWSNEST__TOPIC_BUFFER_LIMITS", """[{"TopicFilter":"#","MaxSizeBytes":11048576}]""");
}

// -------------------------------------------------------------------------
// Development JWT synthesis for the Azure Event Grid emulation instance.
// The mock broker does not verify the signature; any well-formed JWT works.
// -------------------------------------------------------------------------
static string CreateDevJwt()
{
    var keyBytes = new byte[32];
    RandomNumberGenerator.Fill(keyBytes);
    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(keyBytes),
        SecurityAlgorithms.HmacSha256Signature);

    var handler = new JwtSecurityTokenHandler();
    var token = handler.CreateJwtSecurityToken(
        issuer: "https://sts.local",
        audience: "https://eventgrid.azure.net",
        subject: new ClaimsIdentity(new[]
        {
            new Claim("sub", "dev-user"),
            new Claim("scp", "EventGrid.Publish EventGrid.Receive"),
        }),
        notBefore: DateTime.UtcNow.AddMinutes(-5),
        expires: DateTime.UtcNow.AddHours(1),
        issuedAt: DateTime.UtcNow,
        signingCredentials: credentials);

    return handler.WriteToken(token);
}
