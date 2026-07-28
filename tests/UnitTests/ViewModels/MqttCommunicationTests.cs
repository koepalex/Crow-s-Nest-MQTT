using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using NSubstitute;
using MQTTnet;
using System;
using System.Buffers;
using System.IO;
using System.Reactive.Linq;
using Xunit;
using Avalonia.Threading;
using System.Reflection;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    public sealed class MqttCommunicationTests : IDisposable
    {
        private readonly ICommandParserService _commandParserService;
        private readonly IMqttService _mqttServiceMock; // Changed to interface substitute

        public MqttCommunicationTests()
        {
            _commandParserService = Substitute.For<ICommandParserService>();
            _mqttServiceMock = Substitute.For<IMqttService>(); // Substitute the interface

            // Isolate settings persistence to a temp file per test-class instance
            // so tests that mutate ViewModel.Settings don't leak their state
            // into subsequent tests via the shared %LOCALAPPDATA%\CrowsNestMqtt\settings.json.
            _originalSettingsFilePath = SettingsViewModel._settingsFilePath;
            _tempSettingsFilePath = Path.Combine(
                Path.GetTempPath(),
                $"CrowsNestMqtt-tests-{Guid.NewGuid():N}",
                "settings.json");
            SettingsViewModel._settingsFilePath = _tempSettingsFilePath;
        }

        private readonly string _originalSettingsFilePath;
        private readonly string _tempSettingsFilePath;

        public void Dispose()
        {
            SettingsViewModel._settingsFilePath = _originalSettingsFilePath;
            _mqttServiceMock.Dispose();
            try
            {
                var dir = Path.GetDirectoryName(_tempSettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // best-effort cleanup
            }
            GC.SuppressFinalize(this);
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
        public void ConnectAsync_WithAzureHostname_ButNonAzureAuthMode_ShouldRefuseWithClearMessage()
        {
            // Regression: when the hostname is clearly Azure Event Grid but the
            // auth mode is Anonymous / Userpass / Enhanced, the anonymous CONNECT
            // is guaranteed to fail with an opaque broker error. Refuse loudly
            // instead of reconnecting in a loop.
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            viewModel.Settings.Hostname = "myns.northeurope-1.ts.eventgrid.azure.net";
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;

            viewModel.ConnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            _mqttServiceMock.DidNotReceive().ConnectAsync();
            _mqttServiceMock.DidNotReceive().UpdateSettings(Arg.Any<MqttConnectionSettings>());
            Assert.Contains("looks like Azure Event Grid", viewModel.StatusBarText ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(":setauthmode azure", viewModel.StatusBarText ?? string.Empty, StringComparison.Ordinal);
            Assert.True(viewModel.HasConnectionError);
        }

        [Fact]
        public void ConnectAsync_WithAzureMode_ButNonEventGridHost_ShouldWarnButProceed()
        {
            // Localhost or the mock broker are legitimate Azure-mode targets
            // (integration testing) — warn softly but let the connect proceed.
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Azure;
            // SelectedAuthMode setter clobbers Port to 8883 etc., so set hostname AFTER.
            viewModel.Settings.Hostname = "localhost";
            viewModel.Settings.ClientId = "some-client-id"; // silence the empty-client-id warning path
            viewModel.Settings.SubscriptionTopic = "test/topic"; // silence the '#' subscription warning path (last-write-wins on StatusBarText)

            viewModel.ConnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            _mqttServiceMock.Received(1).ConnectAsync();
            Assert.Contains("isn't an Event Grid", viewModel.StatusBarText ?? string.Empty, StringComparison.Ordinal);
        }

        [Fact]
        public void ConnectAsync_WhileAlreadyConnected_ShouldStillTriggerConnect()
        {
            // Regression: re-running :connect (e.g. after changing settings in the
            // connection dialog) while a session is active must forward the request to
            // the MQTT service, which disconnects the existing session before connecting.
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);

            _mqttServiceMock.ConnectionStateChanged += Raise.EventWith(
                _mqttServiceMock,
                new MqttConnectionStateChangedEventArgs(true, null, ConnectionStatusState.Connected));

            Assert.Equal(ConnectionStatusState.Connected, viewModel.ConnectionStatus);

            viewModel.ConnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

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
        public void ConnectionInfoText_ShouldBeEmpty_WhenNotConnected()
        {
            _mqttServiceMock.GetBufferedTopics().Returns(Array.Empty<string>());
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);

            var args = new MqttConnectionStateChangedEventArgs(false, null, ConnectionStatusState.Disconnected, "Disconnected");
            _mqttServiceMock.ConnectionStateChanged += Raise.EventWith(_mqttServiceMock, args);

            Assert.Equal(string.Empty, viewModel.ConnectionInfoText);
        }

        [Fact]
        public void ConnectionInfoText_ShouldShowBrokerDetails_WhenConnected()
        {
            _mqttServiceMock.GetBufferedTopics().Returns(Array.Empty<string>());
            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, uiScheduler: System.Reactive.Concurrency.Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
            viewModel.Settings.Hostname = "broker.example.com";
            viewModel.Settings.Port = 8883;
            viewModel.Settings.UseTls = true;
            viewModel.Settings.SelectedTransport = TransportProtocol.WebSocket;

            var args = new MqttConnectionStateChangedEventArgs(true, null, ConnectionStatusState.Connected, "Connected");
            _mqttServiceMock.ConnectionStateChanged += Raise.EventWith(_mqttServiceMock, args);

            var info = viewModel.ConnectionInfoText;
            Assert.Contains("broker.example.com:8883", info, StringComparison.Ordinal);
            Assert.Contains("TLS: on", info, StringComparison.Ordinal);
            Assert.Contains("Transport: WebSocket", info, StringComparison.Ordinal);
            Assert.Contains("Auth: User/Password", info, StringComparison.Ordinal);
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

            using var viewModel = new MainViewModel(_commandParserService, _mqttServiceMock, null, null, null, new EnvironmentSettingsOverrides { Hostname = expectedHostname, Port = expectedPort, HasOverrides = true, IsAspireEnvironment = true });

            // Act
            viewModel.ConnectCommand.Execute(System.Reactive.Unit.Default).Subscribe();

            // Assert
            _mqttServiceMock.Received(1).UpdateSettings(Arg.Is<MqttConnectionSettings>(s =>
                s!.Hostname == expectedHostname && s.Port == expectedPort
            ));
            _mqttServiceMock.Received(1).ConnectAsync();
        }

        [Fact]
        public async Task ConnectOnLaunchAsync_WhenDialogDisabled_ConnectsWithoutShowingDialog()
        {
            // Arrange
            using var viewModel = new MainViewModel(
                _commandParserService,
                _mqttServiceMock,
                null,
                null,
                null,
                new EnvironmentSettingsOverrides
                {
                    Hostname = "aspirehost",
                    Port = 1883,
                    ShowConnectionDialogOnLaunch = false,
                    HasOverrides = true,
                    IsAspireEnvironment = true
                });

            var dialogShown = false;
            using var handler = viewModel.ShowConnectionDialogInteraction.RegisterHandler(interaction =>
            {
                dialogShown = true;
                interaction.SetOutput(true);
            });

            // Act
            await viewModel.ConnectOnLaunchAsync();

            // Assert
            Assert.False(dialogShown);
            Assert.True(viewModel.AutoConnectOnLaunch);
            _mqttServiceMock.Received(1).UpdateSettings(Arg.Is<MqttConnectionSettings>(s =>
                s!.Hostname == "aspirehost" && s.Port == 1883));
            await _mqttServiceMock.Received(1).ConnectAsync();
        }

        [Fact]
        public async Task ConnectOnLaunchAsync_WhenDialogEnabled_ShowsDialogBeforeConnecting()
        {
            // Arrange
            using var viewModel = new MainViewModel(
                _commandParserService,
                _mqttServiceMock,
                null,
                null,
                null,
                new EnvironmentSettingsOverrides
                {
                    Hostname = "aspirehost",
                    Port = 1883,
                    ShowConnectionDialogOnLaunch = true,
                    HasOverrides = true,
                    IsAspireEnvironment = true
                });

            var dialogShown = false;
            using var handler = viewModel.ShowConnectionDialogInteraction.RegisterHandler(interaction =>
            {
                dialogShown = true;
                interaction.SetOutput(false);
            });

            // Act
            await viewModel.ConnectOnLaunchAsync();

            // Assert
            Assert.True(dialogShown);
            await _mqttServiceMock.DidNotReceive().ConnectAsync();
        }
   }
}
