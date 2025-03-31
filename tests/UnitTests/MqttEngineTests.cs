using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using CrowsNestMqtt.BusinessLogic;

namespace CrowsNestMqtt.Tests
{
    public class MqttEngineTests
    {
        [Fact]
        public async Task MqttEngine_Should_Receive_Published_Message()
        {
            // Arrange
            string brokerHost = "localhost";
            int brokerPort = 11883;
            var engine = new MqttEngine(brokerHost, brokerPort);

            MqttApplicationMessageReceivedEventArgs? receivedArgs = null;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            engine.MessageReceived += (sender, args) =>
            {
                receivedArgs = args;
                messageReceivedEvent.Set();
            };

            await engine.ConnectAsync();

            // Act: Publish a test message using a publisher client.
            var factory = new MqttClientFactory();
            var publisher = factory.CreateMqttClient();
            var publisherOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort, System.Net.Sockets.AddressFamily.InterNetwork)
                .WithCleanSession(true)
                .Build();

            await publisher.ConnectAsync(publisherOptions, CancellationToken.None);

            var payload = "Test Message";
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisher.PublishAsync(message, CancellationToken.None);

            // Wait for the message to be received
            if (!messageReceivedEvent.Wait(TimeSpan.FromSeconds(10)))
            {
                Assert.True(false, "Timeout waiting for message.");
            }

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal("test/topic", receivedArgs.ApplicationMessage.Topic);
            Assert.Equal(payload, receivedArgs.ApplicationMessage.ConvertPayloadToString());

            // Cleanup
            await publisher.DisconnectAsync();
            await engine.DisconnectAsync();
        }
    }
}