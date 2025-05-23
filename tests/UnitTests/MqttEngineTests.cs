using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;
using CrowsNestMqtt.BusinessLogic;

namespace CrowsNestMqtt.UnitTests
{
    public class MqttEngineTests
    {
        [Fact]
        [Trait("Category", "LocalOnly")] // Add this trait to mark the test as local-only
        public async Task MqttEngine_Should_Receive_Published_Message()
        {
            // Arrange
            string brokerHost = "localhost"; // Assuming a local broker for testing
            int brokerPort = 1883; // Default MQTT port
            var connectionSettings = new MqttConnectionSettings
            {
                Hostname = brokerHost,
                Port = brokerPort,
                // Add other necessary default settings for the test if needed
                ClientId = $"test-client-{Guid.NewGuid()}",
                CleanSession = true
            };
            var engine = new MqttEngine(connectionSettings);

           IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null; // Changed type here
           var messageReceivedEvent = new ManualResetEventSlim(false);

            engine.MessageReceived += (sender, args) =>
            {
                if (args.ApplicationMessage.Topic == "test/topic") 
                {
                    receivedArgs = args;
                    messageReceivedEvent.Set();
                }
            };

            await engine.ConnectAsync();

            // Act: Publish a test message using a publisher client.
            var factory = new MqttClientFactory();
            var publisher = factory.CreateMqttClient();
            var publisherOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort) // Removed AddressFamily, let MQTTnet handle defaults
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
                Assert.Fail("Timeout waiting for message.");
            }

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal("test/topic", receivedArgs.ApplicationMessage.Topic);
            Assert.Equal(payload, receivedArgs.ApplicationMessage.ConvertPayloadToString());

            // Cleanup
            await publisher.DisconnectAsync();
            await engine.DisconnectAsync();
        }
[Fact]
        [Trait("Category", "LocalOnly")]
        public async Task MqttEngine_Should_Handle_Empty_Payload_Message()
        {
            // Arrange
            string brokerHost = "localhost"; // Assuming a local broker for testing
            int brokerPort = 1883; // Default MQTT port
            var connectionSettings = new MqttConnectionSettings
            {
                Hostname = brokerHost,
                Port = brokerPort,
                ClientId = $"test-client-{Guid.NewGuid()}",
                CleanSession = true
            };
            var engine = new MqttEngine(connectionSettings);

            IdentifiedMqttApplicationMessageReceivedEventArgs? receivedArgs = null;
            var messageReceivedEvent = new ManualResetEventSlim(false);

            engine.MessageReceived += (sender, args) =>
            {
                if (args.ApplicationMessage.Topic == "test/empty_payload_topic")
                {
                    receivedArgs = args;
                    messageReceivedEvent.Set();
                }
            };

            await engine.ConnectAsync();

            // Act: Publish a test message with an empty payload.
            var factory = new MqttClientFactory();
            var publisher = factory.CreateMqttClient();
            var publisherOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .WithCleanSession(true)
                .Build();

            await publisher.ConnectAsync(publisherOptions, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("test/empty_payload_topic")
                .WithPayload(Array.Empty<byte>()) // Empty payload
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisher.PublishAsync(message, CancellationToken.None);

            // Wait for the message to be received
            if (!messageReceivedEvent.Wait(TimeSpan.FromSeconds(10)))
            {
                Assert.Fail("Timeout waiting for message with empty payload.");
            }

            // Assert
            Assert.NotNull(receivedArgs);
            Assert.Equal("test/empty_payload_topic", receivedArgs.ApplicationMessage.Topic);
            // Payload is ReadOnlySequence<byte>, a struct, so it cannot be null.
            // We check IsEmpty or Length instead.
            Assert.True(receivedArgs.ApplicationMessage.Payload.IsEmpty, "Payload should be empty.");
            Assert.Null(receivedArgs.ApplicationMessage.ConvertPayloadToString());

            // Cleanup
            await publisher.DisconnectAsync();
            await engine.DisconnectAsync();
        }
    }
}