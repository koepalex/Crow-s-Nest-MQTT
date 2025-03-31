using MQTTnet;
using MQTTnet.Protocol;

namespace CrowsNestMqtt.BusinessLogic;

public class MqttEngine
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;

    public event EventHandler<MqttApplicationMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<Exception>? ConnectionError;

    public MqttEngine(string brokerHost, int brokerPort)
    {
        var factory = new MqttClientFactory();
        _options = factory.CreateClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithCleanSession(true)
            .Build();

        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += (e =>
        {
            MessageReceived?.Invoke(this, e);
            return Task.CompletedTask;
        });

        _client.DisconnectedAsync += (async e =>
        {
            if (e.Exception != null)
            {
                ConnectionError?.Invoke(this, e.Exception);
            }
            await ReconnectAsync();
        });

        _options = new MqttClientOptionsBuilder()
                    .WithTcpServer(brokerHost, brokerPort)
                    .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                    .Build();
    }

    public async Task ConnectAsync()
    {
        await _client.ConnectAsync(_options, CancellationToken.None);
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f => 
                    f.WithTopic("#")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build();
        await _client.SubscribeAsync(subscribeOptions);
    }

    public async Task DisconnectAsync()
    {
        await _client.DisconnectAsync();
    }

    private async Task ReconnectAsync()
    {
        while (!_client.IsConnected)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                await ConnectAsync();
            }
            catch(Exception ex)
            {
                ConnectionError?.Invoke(this, ex);
            }
        }
    }
}