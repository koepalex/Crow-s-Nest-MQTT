using System;
using Xunit;
using MQTTnet;
using MQTTnet.Protocol;
using NSubstitute;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.UI.Services;

namespace UnitTests.ViewModels
{
    // Custom mock for IMqttService to handle out parameter
public class MockMqttService : IMqttService
    {
        public Func<string, Guid, (bool found, MqttApplicationMessage? message)>? TryGetMessageHandler;

#pragma warning disable CS0067
        public event EventHandler<IdentifiedMqttApplicationMessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public event EventHandler<string>? LogMessage;
#pragma warning restore CS0067

        static MockMqttService()
        {
            // Explicit dummy usage to suppress CS0067
            var dummy = new MockMqttService();
            EventHandler<IdentifiedMqttApplicationMessageReceivedEventArgs> handler1 = (_, __) => { };
            EventHandler<MqttConnectionStateChangedEventArgs> handler2 = (_, __) => { };
            EventHandler<string> handler3 = (_, __) => { };
            dummy.MessageReceived += handler1;
            dummy.MessageReceived -= handler1;
            dummy.ConnectionStateChanged += handler2;
            dummy.ConnectionStateChanged -= handler2;
            dummy.LogMessage += handler3;
            dummy.LogMessage -= handler3;
        }

        public MockMqttService()
        {
        }

        public bool TryGetMessage(string topic, Guid messageId, out MqttApplicationMessage? message)
        {
            if (TryGetMessageHandler != null)
            {
                var result = TryGetMessageHandler(topic, messageId);
                message = result.message;
                return result.found;
            }
            message = null;
            return false;
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishAsync(string topic, string payload, bool retain, MqttQualityOfServiceLevel qos, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ClearAllBuffers() { }
        public IEnumerable<string> GetBufferedTopics() => Array.Empty<string>();
        public void UpdateSettings(MqttConnectionSettings settings) { }
        public void Dispose() { }
    }

    public class MessageViewModelTests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            var id = Guid.NewGuid();
            var topic = "test/topic";
            var timestamp = DateTime.UtcNow;
            var preview = "payload";
            var size = 42;

            var mqttService = Substitute.For<IMqttService>();
            var statusBarService = Substitute.For<IStatusBarService>();

            var vm = new MessageViewModel(id, topic, timestamp, preview, size, mqttService, statusBarService);

            Assert.Equal(id, vm.MessageId);
            Assert.Equal(topic, vm.Topic);
            Assert.Equal(timestamp, vm.Timestamp);
            Assert.Equal(preview, vm.PayloadPreview);
            Assert.Equal(size, vm.Size);
            Assert.Contains(preview, vm.DisplayText);
        }

        [Fact]
        public void Constructor_ThrowsOnNullTopic()
        {
            var mqttService = Substitute.For<IMqttService>();
            var statusBarService = Substitute.For<IStatusBarService>();
            Assert.Throws<ArgumentNullException>(() =>
                new MessageViewModel(Guid.NewGuid(), null!, DateTime.UtcNow, "preview", 1, mqttService, statusBarService));
        }

        [Fact]
        public void Constructor_ThrowsOnNullMqttService()
        {
            var statusBarService = Substitute.For<IStatusBarService>();
            Assert.Throws<ArgumentNullException>(() =>
                new MessageViewModel(Guid.NewGuid(), "topic", DateTime.UtcNow, "preview", 1, null!, statusBarService));
        }

        [Fact]
        public void Constructor_ThrowsOnNullStatusBarService()
        {
            var mqttService = Substitute.For<IMqttService>();
            Assert.Throws<ArgumentNullException>(() =>
                new MessageViewModel(Guid.NewGuid(), "topic", DateTime.UtcNow, "preview", 1, mqttService, null!));
        }

        [Fact]
        public void GetFullMessage_ReturnsMessage_WhenFound()
        {
            var id = Guid.NewGuid();
            var topic = "topic";
            var timestamp = DateTime.UtcNow;
            var preview = "payload";
            var size = 10;

            var mqttService = new MockMqttService();
            var statusBarService = Substitute.For<IStatusBarService>();
            var expectedMessage = new MqttApplicationMessage();

            mqttService.TryGetMessageHandler = (t, g) => (true, expectedMessage);

            var vm = new MessageViewModel(id, topic, timestamp, preview, size, mqttService, statusBarService);

            var result = vm.GetFullMessage();

            Assert.Equal(expectedMessage, result);
        }

        [Fact]
        public void GetFullMessage_ReturnsNullAndShowsStatus_WhenNotFound()
        {
            var id = Guid.NewGuid();
            var topic = "topic";
            var timestamp = DateTime.UtcNow;
            var preview = "payload";
            var size = 10;

            var mqttService = new MockMqttService();
            var statusBarService = Substitute.For<IStatusBarService>();

            mqttService.TryGetMessageHandler = (t, g) => (false, null);

            var vm = new MessageViewModel(id, topic, timestamp, preview, size, mqttService, statusBarService);

            var result = vm.GetFullMessage();

            Assert.Null(result);
            statusBarService.Received(1).ShowStatus(Arg.Is<string>(msg => msg.Contains(topic)));
        }

        [Fact]
        public void Events_AreUsed_SuppressCS0067()
        {
            var mqttService = new MockMqttService();
            EventHandler<IdentifiedMqttApplicationMessageReceivedEventArgs> handler1 = (_, __) => { };
            EventHandler<MqttConnectionStateChangedEventArgs> handler2 = (_, __) => { };
            EventHandler<string> handler3 = (_, __) => { };
            mqttService.MessageReceived += handler1;
            mqttService.MessageReceived -= handler1;
            mqttService.ConnectionStateChanged += handler2;
            mqttService.ConnectionStateChanged -= handler2;
            mqttService.LogMessage += handler3;
            mqttService.LogMessage -= handler3;
        }
    }
}
