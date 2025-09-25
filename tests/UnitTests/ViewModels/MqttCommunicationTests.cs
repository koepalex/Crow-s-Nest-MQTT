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
        static MqttCommunicationTests()
        {
            // Use reflection to set Dispatcher.UIThread to a synchronous dispatcher for tests
            var dispatcherType = typeof(Dispatcher);
            var field = dispatcherType.GetField("_uiThread", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, new ImmediateDispatcher());
            }
        }

        private class ImmediateDispatcher : IDispatcher
        {
            public bool CheckAccess() => true;
            public void Post(Action action) => action();
            public void Post(Action action, DispatcherPriority priority) => action();
            public void VerifyAccess() { }
            public DispatcherPriority Priority => DispatcherPriority.Normal;
        }
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
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);

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
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);

            // Act
            viewModel.DisconnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            // Assert
            _mqttServiceMock.Received(1).DisconnectAsync();
        }

        [Fact]
        public void ConnectionStateChanged_ShouldUpdateConnectionState()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);

            // Act
            var connectionStateEventArgs = new MqttConnectionStateChangedEventArgs(true, null, ConnectionStatusState.Connected, "Connected");
            _mqttServiceMock.ConnectionStateChanged += Raise.EventWith(_mqttServiceMock, connectionStateEventArgs);

            // Assert
            Assert.Equal(ConnectionStatusState.Connected, viewModel.ConnectionStatus);
            Assert.True(viewModel.IsConnected);
        }

        [Fact]
        public void MessageReceived_ShouldHandleMessageAndUpdateTopicTree()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
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
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);
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
            var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock);

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

            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, expectedHostname, expectedPort);

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
