namespace CrowsNestMqtt.MockBrokerTests;

using System.Net.Sockets;
using System.Text;
using CrowsNestMqtt.MockAzureEventGridBroker;
using MQTTnet;
using Xunit;

/// <summary>
/// End-to-end lifecycle tests for <see cref="MockBroker"/>. These cover the
/// non-TLS and TLS branches of <see cref="MockBroker.StartAsync"/>, the
/// ephemeral-port fallback path, and shutdown behavior. A real MQTTnet v5
/// client is used so we exercise the full server plumbing rather than mocking
/// the event args.
/// </summary>
public sealed class MockBrokerLifecycleTests
{
    private static readonly TimeSpan LogWaitTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StartAsync_NonTls_BindsAndAcceptsClientWithOAuth2Jwt()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false), log);

        var boundPort = await broker.StartAsync();
        Assert.Equal(port, boundPort);
        Assert.Equal(port, broker.BoundPort);

        var jwt = TestHelpers.BuildJwt();
        using var client = new MqttClientFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithTcpServer("127.0.0.1", port)
            .WithClientId("lifecycle-nontls")
            .WithEnhancedAuthentication("OAUTH2-JWT", Encoding.UTF8.GetBytes(jwt))
            .Build();

        var connectResult = await client.ConnectAsync(options, TestContext.Current.CancellationToken);
        Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);

        var accepted = await log.WaitForLineAsync(
            l => l.Contains("Accepted CONNECT (method=OAUTH2-JWT", StringComparison.Ordinal),
            LogWaitTimeout);
        Assert.True(accepted, "Broker log did not contain the expected acceptance line. Rendered log:\n" + log.RenderedLog);

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task StartAsync_Tls_BindsAndAcceptsClientOverTls()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: true), log);

        await broker.StartAsync();

        var jwt = TestHelpers.BuildJwt();
        using var client = new MqttClientFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithTcpServer("127.0.0.1", port)
            .WithTlsOptions(o =>
            {
                o.UseTls();
                o.WithAllowUntrustedCertificates(true);
                o.WithIgnoreCertificateChainErrors(true);
                o.WithCertificateValidationHandler(_ => true);
            })
            .WithClientId("lifecycle-tls")
            .WithEnhancedAuthentication("OAUTH2-JWT", Encoding.UTF8.GetBytes(jwt))
            .Build();

        var connectResult = await client.ConnectAsync(options, TestContext.Current.CancellationToken);
        Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);

        await client.DisconnectAsync();
    }

    [Fact]
    public async Task StartAsync_WritesListeningLineToLogSink()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false), log);

        await broker.StartAsync();

        Assert.Contains(log.Lines, l => l == $"{MockBroker.ListeningStdoutPrefix}127.0.0.1:{port}");
    }

    [Fact]
    public async Task StartAsync_EmitsStartupAndStartedLogLines()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false), log);

        await broker.StartAsync();

        Assert.Contains(log.Lines, l => l.Contains("Starting mock Azure Event Grid broker", StringComparison.Ordinal));
        Assert.Contains(log.Lines, l => l.Contains("Mock broker started", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_WithNullPort_AllocatesEphemeralPort()
    {
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", Port: null, UseTls: false), log);

        var bound = await broker.StartAsync();
        Assert.InRange(bound, 1, 65535);
        Assert.Equal(bound, broker.BoundPort);

        // Prove the ephemeral port is actually reachable.
        using var tcp = new TcpClient();
        await tcp.ConnectAsync("127.0.0.1", bound, TestContext.Current.CancellationToken);
        Assert.True(tcp.Connected);
    }

    [Fact]
    public async Task StartAsync_CalledTwice_Throws()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false), log);

        await broker.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => broker.StartAsync());
    }

    [Fact]
    public async Task Ctor_NullOptions_Throws()
    {
        await Task.Yield();
        Assert.Throws<ArgumentNullException>(() => new MockBroker(null!));
    }

    [Fact]
    public async Task StartAsync_WithCancelledToken_Throws()
    {
        var port = TestHelpers.GetFreeTcpPort();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => broker.StartAsync(cts.Token));
    }

    [Fact]
    public async Task StopAsync_BeforeStart_IsNoOp()
    {
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", 12345, UseTls: false));

        // Should not throw and should not log anything about stopping.
        await broker.StopAsync();
    }

    [Fact]
    public async Task StopAsync_Twice_IsIdempotent()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false), log);
        await broker.StartAsync();

        await broker.StopAsync();
        await broker.StopAsync(); // second call must be a no-op

        Assert.Contains(log.Lines, l => l.Contains("Mock broker stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisposeAsync_StopsRunningBroker()
    {
        var port = TestHelpers.GetFreeTcpPort();
        var log = new CapturingLogWriter();
        var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false), log);
        await broker.StartAsync();

        await broker.DisposeAsync();

        Assert.Contains(log.Lines, l => l.Contains("Mock broker stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DefaultLogSink_IsConsoleOut_WhenNoWriterProvided()
    {
        // We can't intercept Console.Out on a running xunit runner without
        // racing every other test, so we just assert that construction with no
        // log sink succeeds and StartAsync/StopAsync exercise the default path.
        var port = TestHelpers.GetFreeTcpPort();
        await using var broker = new MockBroker(new MockBrokerOptions("127.0.0.1", port, UseTls: false));

        await broker.StartAsync();
        Assert.Equal(port, broker.BoundPort);
        await broker.StopAsync();
    }
}
