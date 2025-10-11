using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using NSubstitute;
using MQTTnet;
using System.Buffers;
using System.Reactive.Linq;
using Xunit;
using Avalonia.Threading;
using System.Reflection;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public class MqttCommunicationTests : IDisposable
    {
        private readonly ICommandParserService _commandParserService;
        private readonly IMqttService _mqttServiceMock; // Changed to interface substitute

        public MqttCommunicationTests()
        {
            _commandParserService = Substitute.For<ICommandParserService>();
            _mqttServiceMock = Substitute.For<IMqttService>(); // Substitute the interface
        }

        public void Dispose()
        {
            // No direct cleanup needed here as each test creates its own ViewModel
            // and the finally block in the Aspire test handles its disposal.
            // If a shared ViewModel were used, it would be disposed here.
        }

        [Fact]
        public void ConnectAsync_ShouldUpdateSettingsAndConnect()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);

            // Act
            viewModel.ConnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            // Assert
            _mqttServiceMock.Received(1).UpdateSettings(Arg.Any<MqttConnectionSettings>());
            _mqttServiceMock.Received(1).ConnectAsync();
        }

        [Fact]
        public void DisconnectAsync_ShouldDisconnect()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);

            // Act
            viewModel.DisconnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            // Assert
            _mqttServiceMock.Received(1).DisconnectAsync();
        }

        [Fact]
        public void ConnectionStateChanged_ShouldUpdateConnectionState()
        {
            // Arrange
            _mqttServiceMock.GetBufferedTopics().Returns(Array.Empty<string>());
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);

            // Act
            var connectionStateEventArgs = new MqttConnectionStateChangedEventArgs(true, null, ConnectionStatusState.Disconnected, "Disconnected");
            _mqttServiceMock.ConnectionStateChanged += Raise.EventWith(_mqttServiceMock, connectionStateEventArgs);

            // Assert
            Assert.Equal(ConnectionStatusState.Disconnected, viewModel.ConnectionStatus);
            Assert.False(viewModel.IsConnected);
        }

        [Fact]
        public void MessageReceived_ShouldHandleMessageAndUpdateTopicTree()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            var payload = System.Text.Encoding.UTF8.GetBytes("test message");
            var message = new MqttApplicationMessageBuilder().WithTopic("test/topic").WithPayload(payload).Build();
            var identifiedArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(Guid.NewGuid(), message, "client1");

            // Act
            _mqttServiceMock.MessagesBatchReceived += Raise.Event<EventHandler<IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs>>>(_mqttServiceMock, new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { identifiedArgs });

            // Assert
            Assert.Single(viewModel.TopicTreeNodes);
            Assert.Equal("test", viewModel.TopicTreeNodes[0].Name);
            Assert.Single(viewModel.TopicTreeNodes[0].Children);
            Assert.Equal("topic", viewModel.TopicTreeNodes[0].Children[0].Name);
        }

        [Fact]
        public void MessageReceived_WhenPaused_ShouldNotUpdateUI()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            viewModel.IsPaused = true; // Set paused state directly

            var payload = System.Text.Encoding.UTF8.GetBytes("test message");
            var message = new MqttApplicationMessageBuilder().WithTopic("test/topic/paused").WithPayload(payload).Build();
            var identifiedArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(Guid.NewGuid(), message, "client1");

            // Act
            _mqttServiceMock.MessagesBatchReceived += Raise.Event<EventHandler<IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs>>>(_mqttServiceMock, new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { identifiedArgs });

            // Assert
            Assert.Empty(viewModel.TopicTreeNodes);
        }

        [Fact]
        public void Dispose_ShouldCleanUpResources()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);

            // Act
            viewModel.Dispose();

            // Assert
            _mqttServiceMock.Received(1).Dispose();
        }

        [Fact]
        public void ConnectCommand_WhenAspireConfigurationProvided_UsesAspireSettingsForConnection()
        {
            // Arrange
            const string expectedHostname = "testhost";
            const int expectedPort = 1883;

            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, expectedHostname, expectedPort);

            // Act
            viewModel.ConnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            // Assert
            _mqttServiceMock.Received(1).UpdateSettings(Arg.Is<MqttConnectionSettings>(s =>
                s.Hostname == expectedHostname && s.Port == expectedPort
            ));
            _mqttServiceMock.Received(1).ConnectAsync();
        }
   }
}
