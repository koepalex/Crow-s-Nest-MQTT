using System.Net;
using System.Net.Sockets;
using System.Text;
using CrowsNestMqtt.Utils;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MQTTnet.Packets;

namespace CrowsNestMqtt.Tests.TestData;

/// <summary>
/// Utilities for MQTT testing including embedded broker management and test client helpers.
/// </summary>
public class MqttTestUtilities : IAsyncDisposable
{
    private MqttServer? _mqttServer;
    private readonly List<IMqttClient> _testClients = new();
    private readonly MqttClientFactory _mqttClientFactory = new();
    private readonly MqttServerFactory _mqttServerFactory = new();

    public string Hostname { get; private set; } = "localhost";
    public int Port { get; private set; }
    public bool IsServerRunning => _mqttServer?.IsStarted ?? false;

    /// <summary>
    /// Starts an embedded MQTT broker on a random available port.
    /// </summary>
    public async Task<int> StartEmbeddedBrokerAsync()
    {
        if (_mqttServer != null)
        {
            await _mqttServer.StopAsync();
            _mqttServer.Dispose();
        }

        Port = GetAvailablePort();
        var options = new MqttServerOptionsBuilder()
            .WithKeepAlive()
            .WithTcpKeepAliveRetryCount(3)
            .WithPersistentSessions(false) // Use in-memory sessions for testing
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(Port)
            .Build();

        _mqttServer = _mqttServerFactory.CreateMqttServer(options);
        await _mqttServer.StartAsync();

        return Port;
    }

    /// <summary>
    /// Stops the embedded MQTT broker.
    /// </summary>
    public async Task StopEmbeddedBrokerAsync()
    {
        if (_mqttServer != null)
        {
            await _mqttServer.StopAsync();
            _mqttServer.Dispose();
            _mqttServer = null;
        }
    }

    /// <summary>
    /// Creates a test MQTT client connected to the test broker.
    /// </summary>
    public async Task<IMqttClient> CreateConnectedTestClientAsync(string clientId = "TestClient")
    {
        if (!IsServerRunning)
        {
            throw new InvalidOperationException("MQTT broker must be started before creating clients");
        }

        var client = _mqttClientFactory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(Hostname, Port)
            .WithClientId(clientId + "-" + Guid.NewGuid().ToString("N")[..8])
            .WithCleanSession()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .Build();

        await client.ConnectAsync(options);
        _testClients.Add(client);
        return client;
    }

    /// <summary>
    /// Publishes test messages with correlation data to the test broker.
    /// </summary>
    public async Task PublishTestMessagesAsync(IMqttClient client, IEnumerable<BufferedMqttMessage> messages)
    {
        foreach (var bufferedMessage in messages)
        {
            var msg = bufferedMessage.Message;
            var mqttMessage = new MqttApplicationMessageBuilder()
                .WithTopic(msg.Topic)
                .WithPayload(msg.Payload.FirstSpan.ToArray())
                .WithQualityOfServiceLevel(msg.QualityOfServiceLevel)
                .WithRetainFlag(msg.Retain)
                .WithCorrelationData(msg.CorrelationData)
                .WithResponseTopic(msg.ResponseTopic)
                .WithContentType(msg.ContentType)
                .WithMessageExpiryInterval(msg.MessageExpiryInterval)
                .WithPayloadFormatIndicator(msg.PayloadFormatIndicator);

            if (msg.UserProperties != null)
            {
                foreach (var prop in msg.UserProperties)
                {
                    mqttMessage.WithUserProperty(prop.Name, prop.ValueBuffer);
                }
            }

            await client.PublishAsync(mqttMessage.Build());

            // Small delay between messages to ensure proper ordering in tests
            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Creates a test subscriber that collects messages for verification.
    /// </summary>
    public async Task<TestMessageCollector> CreateMessageCollectorAsync(string topicPattern = "#")
    {
        var client = await CreateConnectedTestClientAsync("Collector");
        var collector = new TestMessageCollector(client);
        await collector.SubscribeAsync(topicPattern);
        return collector;
    }

    /// <summary>
    /// Waits for a condition to be met within the specified timeout.
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, int checkIntervalMs = 100)
    {
        var endTime = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTime)
        {
            if (condition())
                return true;
            await Task.Delay(checkIntervalMs);
        }
        return false;
    }

    private static int GetAvailablePort()
    {
        using var tcpListener = new TcpListener(IPAddress.Loopback, 0);
        tcpListener.Start();
        int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        // Disconnect all test clients
        foreach (var client in _testClients)
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync();
            }
            client.Dispose();
        }
        _testClients.Clear();

        // Stop and dispose server
        await StopEmbeddedBrokerAsync();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Helper class to collect MQTT messages during testing.
/// </summary>
public class TestMessageCollector : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly List<MqttApplicationMessage> _receivedMessages = new();
    private readonly object _lock = new();

    public TestMessageCollector(IMqttClient client)
    {
        _client = client;
        _client.ApplicationMessageReceivedAsync += OnMessageReceived;
    }

    public IReadOnlyList<MqttApplicationMessage> ReceivedMessages
    {
        get
        {
            lock (_lock)
            {
                return _receivedMessages.ToList().AsReadOnly();
            }
        }
    }

    public int MessageCount
    {
        get
        {
            lock (_lock)
            {
                return _receivedMessages.Count;
            }
        }
    }

    public async Task SubscribeAsync(string topicPattern, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
    {
        await _client.SubscribeAsync(topicPattern, qos);
    }

    public void ClearMessages()
    {
        lock (_lock)
        {
            _receivedMessages.Clear();
        }
    }

    /// <summary>
    /// Waits for a specific number of messages to be received.
    /// </summary>
    public async Task<bool> WaitForMessagesAsync(int expectedCount, TimeSpan timeout)
    {
        return await MqttTestUtilities.WaitForConditionAsync(() => MessageCount >= expectedCount, timeout);
    }

    /// <summary>
    /// Gets messages that have correlation data.
    /// </summary>
    public IEnumerable<MqttApplicationMessage> GetMessagesWithCorrelationData()
    {
        lock (_lock)
        {
            return _receivedMessages.Where(m => m.CorrelationData != null && m.CorrelationData.Length > 0).ToList();
        }
    }

    /// <summary>
    /// Gets messages by topic pattern.
    /// </summary>
    public IEnumerable<MqttApplicationMessage> GetMessagesByTopic(string topicPattern)
    {
        lock (_lock)
        {
            if (topicPattern.Contains('#') || topicPattern.Contains('+'))
            {
                // Simple wildcard matching - for more complex scenarios, use a proper MQTT topic matcher
                var pattern = topicPattern.Replace("+", "[^/]+").Replace("#", ".*");
                var regex = new System.Text.RegularExpressions.Regex($"^{pattern}$");
                return _receivedMessages.Where(m => regex.IsMatch(m.Topic)).ToList();
            }
            else
            {
                return _receivedMessages.Where(m => m.Topic == topicPattern).ToList();
            }
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        lock (_lock)
        {
            _receivedMessages.Add(e.ApplicationMessage);
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            _client.ApplicationMessageReceivedAsync -= OnMessageReceived;
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
        }
        GC.SuppressFinalize(this);
    }
}