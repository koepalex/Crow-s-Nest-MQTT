using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Options; // Added for MqttClientOptions
using MQTTnet.Protocol;
using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration; // Added for AuthenticationMode types
using System.IO; // Added for Path and File operations
using System.Collections.Generic; // Added for List

namespace CrowsNestMqtt.UnitTests
{
    public class MqttEngineTests
    {
        private MqttClientOptions? GetMqttClientOptions(MqttEngine engine)
        {
            var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
            return methodInfo?.Invoke(engine, null) as MqttClientOptions;
        }

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

        [Fact]
        public void BuildMqttOptions_WithUsernamePasswordAuth_ShouldSetCredentials()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                AuthMode = new CrowsNestMqtt.BusinessLogic.Configuration.UsernamePasswordAuthenticationMode("testuser", "testpass")
            };
            var engine = new MqttEngine(settings);

            // Act
            // Use reflection to access the private method BuildMqttOptions
            var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
            var options = methodInfo?.Invoke(engine, null) as MqttClientOptions;

            // Assert
            Assert.NotNull(options);
            Assert.Equal("testuser", options.Credentials?.GetUserName(options));
            // Note: MQTTnet.MqttClientOptions stores password as byte[]
            Assert.Equal("testpass", System.Text.Encoding.UTF8.GetString(options.Credentials?.GetPassword(options) ?? Array.Empty<byte>()));
        }

        [Fact]
        public void BuildMqttOptions_WithAnonymousAuth_ShouldNotSetCredentials()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                AuthMode = new CrowsNestMqtt.BusinessLogic.Configuration.AnonymousAuthenticationMode()
            };
            var engine = new MqttEngine(settings);

            // Act
            var methodInfo = typeof(MqttEngine).GetMethod("BuildMqttOptions", BindingFlags.NonPublic | BindingFlags.Instance);
            var options = methodInfo?.Invoke(engine, null) as MqttClientOptions;

            // Assert
            Assert.NotNull(options);
            Assert.Null(options.Credentials); // Or check if UserName/Password are null/empty if Credentials object is always created
        }

        [Fact]
        public void BuildMqttOptions_ConfiguresTlsWithoutClientCertificate_WhenPathIsMissing()
        {
            // Arrange
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientCertificatePath = null // Explicitly null
            };
            var engine = new MqttEngine(settings);

            // Act
            var options = GetMqttClientOptions(engine);

            // Assert
            Assert.NotNull(options);
            Assert.NotNull(options.TlsOptions);
            Assert.True(options.TlsOptions.UseTls);
            Assert.True(options.TlsOptions.ClientCertificates == null || !options.TlsOptions.ClientCertificates.Any());
        }

        [Fact]
        public void BuildMqttOptions_ConfiguresTlsWithClientCertificate_WhenPathIsValid()
        {
            // Arrange
            string dummyCertPath = Path.Combine(Path.GetTempPath(), $"dummy-{Guid.NewGuid()}.pfx");
            // Create an empty file; X509Certificate2 might fail to load it,
            // but MqttEngine should attempt and log. The key is that ClientCertificates is set or not.
            File.WriteAllText(dummyCertPath, "dummy content"); 

            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientCertificatePath = dummyCertPath
            };
            var engine = new MqttEngine(settings);
            var logMessages = new List<string>();
            engine.LogMessage += (s, e) => logMessages.Add(e);

            // Act
            var options = GetMqttClientOptions(engine);

            // Assert
            Assert.NotNull(options);
            Assert.NotNull(options.TlsOptions);
            Assert.True(options.TlsOptions.UseTls);

            // Depending on X509Certificate2's behavior with "dummy content",
            // ClientCertificates might be null (if loading failed and was caught)
            // or contain a certificate (if it somehow "loaded" the dummy file, less likely for a real cert).
            // The important part for this test is that the engine *tried* because the file exists.
            // The MqttEngine logs "Client certificate loaded successfully." or "Error loading client certificate..."
            
            bool loadAttempted = logMessages.Any(log => log.Contains("Attempting to load client certificate from") && log.Contains(dummyCertPath));
            Assert.True(loadAttempted, "MqttEngine should have attempted to load the certificate.");

            // If X509Certificate2 fails to load "dummy content" (which it should), 
            // the ClientCertificates collection should be null or empty due to the catch block in MqttEngine.
            if (logMessages.Any(log => log.Contains("Error loading client certificate")))
            {
                Assert.True(options.TlsOptions.ClientCertificates == null || !options.TlsOptions.ClientCertificates.Any(),
                    "ClientCertificates should be null or empty if loading failed.");
            }
            else if (logMessages.Any(log => log.Contains("Client certificate loaded successfully.")))
            {
                // This case is less likely with "dummy content" but included for completeness
                 Assert.NotNull(options.TlsOptions.ClientCertificates);
                 Assert.Single(options.TlsOptions.ClientCertificates);
            }


            // Cleanup
            if (File.Exists(dummyCertPath))
            {
                File.Delete(dummyCertPath);
            }
        }

        [Fact]
        public void BuildMqttOptions_ConfiguresTlsWithoutClientCertificate_WhenPathIsInvalid()
        {
            // Arrange
            string invalidCertPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.pfx");
            var settings = new MqttConnectionSettings
            {
                Hostname = "localhost",
                Port = 1883,
                ClientCertificatePath = invalidCertPath
            };
            var engine = new MqttEngine(settings);
            var logMessages = new List<string>();
            engine.LogMessage += (s, e) => logMessages.Add(e);

            // Act
            var options = GetMqttClientOptions(engine);

            // Assert
            Assert.NotNull(options);
            Assert.NotNull(options.TlsOptions);
            Assert.True(options.TlsOptions.UseTls);
            Assert.True(options.TlsOptions.ClientCertificates == null || !options.TlsOptions.ClientCertificates.Any());
            Assert.Contains(logMessages, log => log.Contains($"Client certificate file not found at: '{invalidCertPath}'"));
        }
    }
}