using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Server;
using Xunit;

namespace CrowsNestMqtt.UnitTests
{
    /// <summary>
    /// Test fixture that starts an in-memory MQTT broker on a random port for integration tests.
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

                Port = GetMqttServerPort();
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

                // Health-check: verify the broker is actually listening
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
                    Trace.WriteLine($"[MqttBrokerFixture] StopAsync timed out after 5s, forcing dispose.");
                }
                _mqttServer.Dispose();
                _mqttServer = null;
            }
            IsRunning = false;
            Trace.WriteLine($"[MqttBrokerFixture] Broker disposed.");
        }

        private async Task VerifyBrokerListeningAsync()
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, Port, cts.Token).ConfigureAwait(false);
                Trace.WriteLine($"[MqttBrokerFixture] Health-check passed: TCP connect to port {Port} succeeded.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[MqttBrokerFixture] Health-check FAILED: TCP connect to port {Port} failed: {ex.Message}");
                throw new InvalidOperationException(
                    $"Embedded MQTT broker started but is not accepting connections on port {Port}.", ex);
            }
        }

        private static int GetMqttServerPort()
        {
            using var tcpListen = new TcpListener(IPAddress.Loopback, 0);
            tcpListen.Start();
            int port = ((IPEndPoint)tcpListen.LocalEndpoint).Port;
            tcpListen.Stop();
            return port;
        }
    }
}
