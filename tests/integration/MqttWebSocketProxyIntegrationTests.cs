using System.Text;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using MQTTnet.Protocol;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests;

public sealed class MqttWebSocketProxyIntegrationTests
    : IClassFixture<WebSocketProxyBrokerFixture>
{
    private readonly WebSocketProxyBrokerFixture _fixture;

    public MqttWebSocketProxyIntegrationTests(WebSocketProxyBrokerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MqttEngine_ShouldPublishAndReceiveThroughConfiguredWebSocketProxy()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var topic = $"proxy/integration/{Guid.NewGuid():N}";
        const string payload = "through-the-proxy";
        var connected = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var engine = new MqttEngine(new MqttConnectionSettings
        {
            Hostname = WebSocketProxyBrokerFixture.Hostname,
            Port = _fixture.WebSocketPort,
            ClientId = $"proxy-test-{Guid.NewGuid():N}",
            Transport = TransportProtocol.WebSocket,
            WebSocketPath = "/mqtt",
            WebSocketProxyAddress = $"http://{WebSocketProxyBrokerFixture.Hostname}:{_fixture.ProxyPort}",
            SubscriptionTopic = topic
        });

        engine.ConnectionStateChanged += (_, args) =>
        {
            if (args.IsConnected)
            {
                connected.TrySetResult();
            }
        };
        engine.MessagesBatchReceived += (_, batch) =>
        {
            var message = batch.FirstOrDefault(item => item.Topic == topic);
            if (message != null)
            {
                received.TrySetResult(Encoding.UTF8.GetString(message.ApplicationMessage.Payload));
            }
        };

        await engine.ConnectAsync(cts.Token);
        await connected.Task.WaitAsync(cts.Token);
        await Task.Delay(500, cts.Token);

        await engine.PublishAsync(
            topic,
            payload,
            qos: MqttQualityOfServiceLevel.AtLeastOnce,
            cancellationToken: cts.Token);

        Assert.Equal(payload, await received.Task.WaitAsync(cts.Token));
        Assert.True(_fixture.ProxyConnectionCount > 0);
        Assert.Contains(
            _fixture.WebSocketPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _fixture.LastProxyRequestLine,
            StringComparison.Ordinal);

        await engine.DisconnectAsync(CancellationToken.None);
    }
}
