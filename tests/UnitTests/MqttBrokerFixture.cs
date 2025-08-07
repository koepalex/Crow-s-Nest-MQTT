using System;
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

        public async Task InitializeAsync()
        {
            var mqttFactory = new MqttServerFactory();

            Port = GetMqttServerPort();
            var options = new MqttServerOptionsBuilder()
                .WithKeepAlive()
                .WithTcpKeepAliveRetryCount(3)
                .WithPersistentSessions()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(Port)
                .Build();
            _mqttServer = mqttFactory.CreateMqttServer(options);

            await _mqttServer.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_mqttServer != null)
            {
                await _mqttServer.StopAsync();
                _mqttServer.Dispose();
                _mqttServer = null;
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
