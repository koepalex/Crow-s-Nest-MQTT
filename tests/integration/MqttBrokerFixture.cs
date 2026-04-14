using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MQTTnet.Server;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests;

/// <summary>
/// Test fixture that starts an in-memory MQTT broker on a random port for integration tests.
/// Shared across test classes via <see cref="IClassFixture{TFixture}"/>.
/// </summary>
public class MqttBrokerFixture : IAsyncLifetime
{
    private MqttServer? _mqttServer;
    public int Port { get; private set; }
    public string Hostname => "localhost";
    public bool IsRunning { get; private set; }
    public string? StartupError { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            var mqttFactory = new MqttServerFactory();

            Port = GetRandomPort();
            Trace.WriteLine($"[MqttBrokerFixture] Starting embedded MQTT broker on port {Port}...");

            var options = new MqttServerOptionsBuilder()
                .WithKeepAlive()
                .WithTcpKeepAliveRetryCount(3)
                .WithPersistentSessions()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(Port)
                .Build();
            _mqttServer = mqttFactory.CreateMqttServer(options);

            await _mqttServer.StartAsync().ConfigureAwait(false);
            await VerifyBrokerListeningAsync().ConfigureAwait(false);

            IsRunning = true;
            Trace.WriteLine($"[MqttBrokerFixture] Broker started successfully on port {Port}.");
        }
        catch (Exception ex)
        {
            StartupError = ex.ToString();
            IsRunning = false;
            Trace.WriteLine($"[MqttBrokerFixture] FAILED to start broker on port {Port}: {ex}");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Trace.WriteLine($"[MqttBrokerFixture] Disposing broker on port {Port}...");
        if (_mqttServer != null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await _mqttServer.StopAsync().WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Trace.WriteLine("[MqttBrokerFixture] StopAsync timed out after 5s, forcing dispose.");
            }
            _mqttServer.Dispose();
            _mqttServer = null;
        }
        IsRunning = false;
    }

    private async Task VerifyBrokerListeningAsync()
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(IPAddress.Loopback, Port, cts.Token).ConfigureAwait(false);
    }

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
