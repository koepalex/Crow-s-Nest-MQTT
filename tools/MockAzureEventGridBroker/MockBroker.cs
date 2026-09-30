namespace CrowsNestMqtt.MockAzureEventGridBroker;

using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;

/// <summary>
/// In-process, single-use mock MQTT broker that emulates Azure Event Grid
/// namespace enhanced authentication (<c>OAUTH2-JWT</c>). Accepts CONNECT
/// packets whose AuthenticationMethod is <c>OAUTH2-JWT</c> and whose
/// AuthenticationData contains a well-formed JWT. Signature and audience are
/// NOT verified. Do not use in production or expose beyond localhost.
///
/// One instance binds exactly once; call <see cref="StartAsync"/>, then
/// <see cref="StopAsync"/> or dispose. Tests should create a fresh instance
/// per test to avoid MQTTnet server-lifecycle surprises.
/// </summary>
internal sealed class MockBroker : IAsyncDisposable
{
    internal const string ExpectedMethod = "OAUTH2-JWT";
    internal const string ListeningStdoutPrefix = "MOCK_EG_LISTENING_ON=";

    private readonly MockBrokerOptions _options;
    private readonly TextWriter _log;

    private MqttServer? _server;
    private int _boundPort;
    private bool _started;

    public MockBroker(MockBrokerOptions options, TextWriter? log = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? Console.Out;
    }

    /// <summary>
    /// Port the broker actually bound to. Zero before <see cref="StartAsync"/>
    /// completes successfully.
    /// </summary>
    public int BoundPort => _boundPort;

    /// <summary>
    /// Starts the MQTT listener on <see cref="MockBrokerOptions.Host"/> and, if
    /// specified, <see cref="MockBrokerOptions.Port"/> — otherwise an ephemeral
    /// port is allocated. Returns the port that was actually bound so callers
    /// can advertise it to clients.
    /// </summary>
    public async Task<int> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            throw new InvalidOperationException("MockBroker has already been started; create a new instance for a subsequent listener.");
        }
        _started = true;
        cancellationToken.ThrowIfCancellationRequested();

        var port = _options.Port ?? GetFreeTcpPort();
        _boundPort = port;

        Log(FormattableString.Invariant($"Starting mock Azure Event Grid broker on {_options.Host}:{port} (TLS={_options.UseTls})."));

        var optionsBuilder = new MqttServerOptionsBuilder()
            .WithPersistentSessions()
            .WithKeepAlive();

        if (_options.UseTls)
        {
            var pfxBytes = DevCertificateFactory.CreateSelfSignedPfxBytes(_options.Host);
            var cert = X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                password: null,
                keyStorageFlags: X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            optionsBuilder = optionsBuilder
                .WithEncryptedEndpoint()
                .WithEncryptedEndpointBoundIPAddress(IPAddress.Parse(_options.Host))
                .WithEncryptedEndpointPort(port)
                .WithoutDefaultEndpoint()
                .WithEncryptionCertificate(cert)
                .WithEncryptionSslProtocol(System.Security.Authentication.SslProtocols.None);
        }
        else
        {
            optionsBuilder = optionsBuilder
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(IPAddress.Parse(_options.Host))
                .WithDefaultEndpointPort(port);
        }

        var factory = new MqttServerFactory();
        _server = factory.CreateMqttServer(optionsBuilder.Build());
        _server.ValidatingConnectionAsync += HandleValidatingConnectionAsync;
        _server.InterceptingInboundPacketAsync += HandleInboundPacketAsync;
        _server.ClientConnectedAsync += HandleClientConnectedAsync;
        _server.ClientDisconnectedAsync += HandleClientDisconnectedAsync;

        await _server.StartAsync().ConfigureAwait(false);

        // Bare stdout line consumed by Aspire and other orchestrators — must
        // remain free of the timestamp/prefix that Log() adds.
        await _log.WriteLineAsync(FormattableString.Invariant($"{ListeningStdoutPrefix}{_options.Host}:{port}")).ConfigureAwait(false);
        Log("Mock broker started. Waiting for connections.");
        return port;
    }

    /// <summary>
    /// Stops the underlying MQTT server if it is running. Safe to call multiple
    /// times or before <see cref="StartAsync"/> — in either case the call is a
    /// no-op.
    /// </summary>
    public async Task StopAsync()
    {
        if (_server is null)
        {
            return;
        }

        Log("Stopping mock broker...");
        try
        {
            await _server.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _server.Dispose();
            _server = null;
        }
        Log("Mock broker stopped.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    internal Task HandleValidatingConnectionAsync(ValidatingConnectionEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (!string.Equals(args.AuthenticationMethod, ExpectedMethod, StringComparison.Ordinal))
        {
            args.ReasonCode = MqttConnectReasonCode.BadAuthenticationMethod;
            args.ReasonString = FormattableString.Invariant(
                $"Mock broker rejects CONNECT: expected AuthenticationMethod '{ExpectedMethod}', got '{args.AuthenticationMethod ?? "(null)"}'.");
            Log(args.ReasonString);
            return Task.CompletedTask;
        }

        if (args.AuthenticationData is null || args.AuthenticationData.Length == 0)
        {
            args.ReasonCode = MqttConnectReasonCode.NotAuthorized;
            args.ReasonString = "Mock broker rejects CONNECT: AuthenticationData is empty.";
            Log(args.ReasonString);
            return Task.CompletedTask;
        }

        var tokenString = Encoding.UTF8.GetString(args.AuthenticationData);
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokenString);
            var sub = jwt.Subject ?? "(missing sub)";
            var exp = jwt.ValidTo == default
                ? "(no exp)"
                : jwt.ValidTo.ToString("O", CultureInfo.InvariantCulture);
            Log(FormattableString.Invariant(
                $"Accepted CONNECT (method={ExpectedMethod}, client-id='{args.ClientId}', sub={sub}, exp={exp})."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            args.ReasonCode = MqttConnectReasonCode.NotAuthorized;
            args.ReasonString = FormattableString.Invariant(
                $"Mock broker rejects CONNECT: AuthenticationData is not a valid JWT ({ex.GetType().Name}: {ex.Message}).");
            Log(args.ReasonString);
        }

        return Task.CompletedTask;
    }

    internal Task HandleInboundPacketAsync(InterceptingPacketEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Packet is not MqttAuthPacket auth)
        {
            return Task.CompletedTask;
        }

        var tokenLen = auth.AuthenticationData?.Length ?? 0;
        var summary = FormattableString.Invariant(
            $"Received AUTH (method={auth.AuthenticationMethod}, reason={auth.ReasonCode}, data-bytes={tokenLen}, client-id='{args.ClientId}').");
        try
        {
            if (auth.AuthenticationData is { Length: > 0 })
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
                    Encoding.UTF8.GetString(auth.AuthenticationData));
                summary += FormattableString.Invariant(
                    $" JWT sub={jwt.Subject ?? "(missing)"}, exp={jwt.ValidTo.ToString("O", CultureInfo.InvariantCulture)}.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            summary += FormattableString.Invariant($" (JWT parse failed: {ex.Message})");
        }
        Log(summary);
        return Task.CompletedTask;
    }

    private Task HandleClientConnectedAsync(ClientConnectedEventArgs args)
    {
        Log(FormattableString.Invariant($"Client connected: id='{args.ClientId}' endpoint={args.RemoteEndPoint}."));
        return Task.CompletedTask;
    }

    private Task HandleClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        Log(FormattableString.Invariant($"Client disconnected: id='{args.ClientId}' type={args.DisconnectType}."));
        return Task.CompletedTask;
    }

    internal static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void Log(string message)
    {
        _log.WriteLine(FormattableString.Invariant(
            $"[{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}] [mock-eg-broker] {message}"));
    }
}
