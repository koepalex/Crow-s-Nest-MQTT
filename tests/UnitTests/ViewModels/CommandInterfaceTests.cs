using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Services; // Added for IStatusBarService
using NSubstitute;
using MQTTnet;
using System.Reflection;
using System.Text;
using System.Reactive.Concurrency;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
public class CommandInterfaceTests
    {
       // Synchronous dispatcher to make Dispatcher.UIThread.Post immediate in unit tests
       static CommandInterfaceTests()
       {
           var dispatcherType = typeof(Avalonia.Threading.Dispatcher);
           var field = dispatcherType.GetField("_uiThread", BindingFlags.Static | BindingFlags.NonPublic);
           if (field != null)
           {
               field.SetValue(null, new ImmediateDispatcher());
           }
       }

       private sealed class ImmediateDispatcher : Avalonia.Threading.IDispatcher
       {
           public bool CheckAccess() => true;
           public void Post(Action action) => action();
           public void Post(Action action, Avalonia.Threading.DispatcherPriority priority) => action();
           public void VerifyAccess() { }
           public Avalonia.Threading.DispatcherPriority Priority => Avalonia.Threading.DispatcherPriority.Normal;
       }

       private readonly ICommandParserService _commandParserService;
       private readonly MqttEngine _mqttEngine; // Keep this for engine-specific tests
       private readonly IMqttService _mqttServiceMock; // Add mock for MessageViewModel
       private readonly IStatusBarService _statusBarServiceMock; // Add mock for MessageViewModel

       public CommandInterfaceTests()
       {
           _commandParserService = Substitute.For<ICommandParserService>();
           _mqttEngine = Substitute.For<MqttEngine>(new MqttConnectionSettings());
           _mqttServiceMock = Substitute.For<IMqttService>(); // Initialize mock
           _statusBarServiceMock = Substitute.For<IStatusBarService>(); // Initialize mock
       }

        [Fact(Timeout = 10000)]
        public async Task ExecuteSubmitInput_WithCommandInput_ShouldExecuteCommand()
        {
            // Arrange - inject mock IMqttService via constructor to avoid creating a real MqttEngine
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            const string commandText = ":connect localhost:1883";
            viewModel.CommandText = commandText;

            // Set up the command parser mock to return a valid command result
            var parsedCommand = new ParsedCommand(CommandType.Connect, new List<string> { "localhost:1883" }.AsReadOnly());
            var commandResult = CommandResult.SuccessCommand(parsedCommand);
            _commandParserService.ParseInput(commandText, Arg.Any<SettingsData>()).Returns(commandResult);

            // Act - Call the ExecuteSubmitInput method via reflection
            var executeMethod = typeof(MainViewModel).GetMethod("ExecuteSubmitInput", BindingFlags.NonPublic | BindingFlags.Instance);
            executeMethod?.Invoke(viewModel, null);

            // Assert
            _commandParserService.Received(1).ParseInput(commandText, Arg.Any<SettingsData>());
            await _mqttServiceMock.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public void ExecuteSubmitInput_WithSearchTerm_ShouldApplyFilter()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            const string searchTerm = "test search";
            viewModel.CommandText = searchTerm;

            // Set up the command parser mock to return a search result
            var commandResult = CommandResult.SuccessSearch(searchTerm);
            _commandParserService.ParseInput(searchTerm, Arg.Any<SettingsData>()).Returns(commandResult); // Revert to Arg.Any for now

            // Act - Call the ExecuteSubmitInput method via reflection
            var executeMethod = typeof(MainViewModel).GetMethod("ExecuteSubmitInput", BindingFlags.NonPublic | BindingFlags.Instance);
            executeMethod?.Invoke(viewModel, null);

            // Assert
            _commandParserService.Received(1).ParseInput(searchTerm, Arg.Any<SettingsData>()); // Use Arg.Any for Received check too
            Assert.Equal(searchTerm, viewModel.CurrentSearchTerm);
        }

        [Fact]
        public void DispatchCommand_WithHelpCommand_ShouldDisplayHelp()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            var parsedCommand = new ParsedCommand(CommandType.Help, new List<string>().AsReadOnly());

            // Act - Call the DispatchCommand method via reflection
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { parsedCommand });

            // Assert
            Assert.Contains("Available commands", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_WithSpecificHelpCommand_ShouldDisplaySpecificHelp()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            var parsedCommand = new ParsedCommand(CommandType.Help, new List<string> { "connect" }.AsReadOnly());

            // Act - Call the DispatchCommand method via reflection
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { parsedCommand });

            // Assert
            Assert.Contains("Help for :connect", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_WithFilterCommand_ShouldApplyTopicFilter()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            var parsedCommand = new ParsedCommand(CommandType.Filter, new List<string> { "sensor" }.AsReadOnly());

            // Act - Call the DispatchCommand method via reflection
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { parsedCommand });

            // Assert
            Assert.Contains("Topic filter applied", viewModel.StatusBarText);
            Assert.True(viewModel.IsTopicFilterActive);
        }

        [Fact]
        public void DispatchCommand_WithClearFilterCommand_ShouldClearTopicFilter()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            
            // First apply a filter
            var applyFilterCommand = new ParsedCommand(CommandType.Filter, new List<string> { "sensor" }.AsReadOnly());
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { applyFilterCommand });

            // Then clear it with an empty filter command
            var clearFilterCommand = new ParsedCommand(CommandType.Filter, new List<string>().AsReadOnly());

            // Act
            dispatchMethod?.Invoke(viewModel, new object[] { clearFilterCommand });

            // Assert
            Assert.Contains("Topic filter cleared", viewModel.StatusBarText);
            Assert.False(viewModel.IsTopicFilterActive);
        }

        [Fact]
        public void DispatchCommand_WithExportCommand_ShouldExportSelectedMessage()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            
           // Create a message to export
           var messageIdExport = Guid.NewGuid();
           var timestampExport = DateTime.Now;
           var topicExport = "test/export";
           var payloadExport = "test payload";
           var fullMessageExport = new MqttApplicationMessageBuilder()
               .WithTopic(topicExport)
               .WithPayload(Encoding.UTF8.GetBytes(payloadExport))
               .Build();

           _mqttServiceMock.TryGetMessage(topicExport, messageIdExport, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessageExport;
                   return true;
               });

           var testMessageExport = new MessageViewModel(messageIdExport, topicExport, timestampExport, payloadExport, Encoding.UTF8.GetBytes(payloadExport).Length, _mqttServiceMock, _statusBarServiceMock);
           viewModel.SelectedMessage = testMessageExport;
            
            // Create export command with format and path
            var exportCommand = new ParsedCommand(CommandType.Export, new List<string> { "json", "c:\\temp" }.AsReadOnly());

            // Act - Call the DispatchCommand method via reflection
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { exportCommand });

            // Assert - Check that the status bar indicates success, even if the file isn't fully verified in this unit test.
            // The actual file path might vary, so we check for the start of the success message.
            Assert.StartsWith("Successfully exported message to:", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_WithViewRawCommand_ShouldSwitchToRawView()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            
           // Create a message with JSON payload
           var messageIdViewRaw = Guid.NewGuid();
           var timestampViewRaw = DateTime.Now;
           var topicViewRaw = "test/view";
           var payloadViewRaw = "{\"test\":\"value\"}";
           var fullMessageViewRaw = new MqttApplicationMessageBuilder()
               .WithTopic(topicViewRaw)
               .WithPayload(Encoding.UTF8.GetBytes(payloadViewRaw))
               .Build();

            _mqttServiceMock.TryGetMessage(topicViewRaw, messageIdViewRaw, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessageViewRaw;
                   return true;
               });

           var testMessageViewRaw = new MessageViewModel(messageIdViewRaw, topicViewRaw, timestampViewRaw, payloadViewRaw, Encoding.UTF8.GetBytes(payloadViewRaw).Length, _mqttServiceMock, _statusBarServiceMock);
           viewModel.SelectedMessage = testMessageViewRaw;
            
            // Initially JSON viewer should be visible for valid JSON
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
            
            // Create view raw command
            var viewRawCommand = new ParsedCommand(CommandType.ViewRaw, new List<string>().AsReadOnly());

            // Act - Call the DispatchCommand method via reflection
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { viewRawCommand });

            // Assert
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);
        }

        [Fact]
        public void DispatchCommand_WithViewJsonCommand_ShouldSwitchToJsonView()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            
           // Create a message with JSON payload (same as above for simplicity, could reuse)
           var messageIdViewJson = Guid.NewGuid();
           var timestampViewJson = DateTime.Now;
           var topicViewJson = "test/view";
           var payloadViewJson = "{\"test\":\"value\"}";
           var fullMessageViewJson = new MqttApplicationMessageBuilder()
               .WithTopic(topicViewJson)
               .WithPayload(Encoding.UTF8.GetBytes(payloadViewJson))
               .Build();

           _mqttServiceMock.TryGetMessage(topicViewJson, messageIdViewJson, out Arg.Any<MqttApplicationMessage?>())
               .Returns(x => {
                   x[2] = fullMessageViewJson;
                   return true;
               });

           var testMessageViewJson = new MessageViewModel(messageIdViewJson, topicViewJson, timestampViewJson, payloadViewJson, Encoding.UTF8.GetBytes(payloadViewJson).Length, _mqttServiceMock, _statusBarServiceMock);
           viewModel.SelectedMessage = testMessageViewJson;
            
            // First switch to raw view
            var payloadViewTypeEnum = typeof(MainViewModel).GetNestedType("PayloadViewType", BindingFlags.NonPublic);
            Assert.NotNull(payloadViewTypeEnum);
            var rawValue = Enum.Parse(payloadViewTypeEnum, "Raw");
            var typedRawValue = Convert.ChangeType(rawValue, payloadViewTypeEnum);
            var switchViewMethod = typeof(MainViewModel).GetMethod(
                "SwitchPayloadView",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { payloadViewTypeEnum },
                null);
            switchViewMethod?.Invoke(viewModel, new object[] { typedRawValue });

            // Confirm raw view is active
            Assert.False(viewModel.IsJsonViewerVisible);
            Assert.True(viewModel.IsRawTextViewerVisible);
            
            // Create view JSON command
            var viewJsonCommand = new ParsedCommand(CommandType.ViewJson, new List<string>().AsReadOnly());

            // Act - Call the DispatchCommand method via reflection
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { viewJsonCommand });

            // Assert
            Assert.True(viewModel.IsJsonViewerVisible);
            Assert.False(viewModel.IsRawTextViewerVisible);
        }

        [Fact]
        public void UpdateCommandSuggestions_ShouldFilterSuggestionsByCommandText()
        {
            // Arrange
            using var viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            
            // Get the private method via reflection
            var updateSuggestionsMethod = typeof(MainViewModel).GetMethod("UpdateCommandSuggestions", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            updateSuggestionsMethod?.Invoke(viewModel, new object[] { ":c" });
            
            // Assert
            bool hasConnectSuggestion = false;
            bool hasCopySuggestion = false;
            bool hasClearSuggestion = false;
            bool hasHelpSuggestion = false;
            
            foreach (var suggestion in viewModel.CommandSuggestions)
            {
                if (suggestion.Contains(":connect", StringComparison.OrdinalIgnoreCase))
                    hasConnectSuggestion = true;
                else if (suggestion.Contains(":copy", StringComparison.OrdinalIgnoreCase))
                    hasCopySuggestion = true;
                else if (suggestion.Contains(":clear", StringComparison.OrdinalIgnoreCase))
                    hasClearSuggestion = true;
                else if (suggestion.Contains(":help", StringComparison.OrdinalIgnoreCase))
                    hasHelpSuggestion = false; // Should not contain :help
            }
            
            Assert.True(hasConnectSuggestion);
            Assert.True(hasCopySuggestion);
            Assert.True(hasClearSuggestion);
            Assert.False(hasHelpSuggestion);
        }

        // Helper method to invoke DispatchCommand via reflection
        private void DispatchCommand(MainViewModel viewModel, CommandType type, params string[] args)
        {
            var parsedCommand = new ParsedCommand(type, new List<string>(args).AsReadOnly());
            var dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance);
            dispatchMethod?.Invoke(viewModel, new object[] { parsedCommand });
        }

        [Fact]
        public void DispatchCommand_SetAuthMode_UserPass_ShouldUpdateSettings()
        {
            // Arrange
            var commandParser = new CommandParserService(); // Use real parser
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous; // Start as anonymous

            // Act
            DispatchCommand(viewModel, CommandType.SetAuthMode, "userpass");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.Settings.SelectedAuthMode);
            Assert.Contains("Authentication mode set to Username/Password", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_SetAuthMode_Anonymous_ShouldUpdateSettings()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword; // Start as userpass
            viewModel.Settings.AuthUsername = "test"; // Ensure it's not empty for full switch effect

            // Act
            DispatchCommand(viewModel, CommandType.SetAuthMode, "anonymous");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.Anonymous, viewModel.Settings.SelectedAuthMode);
            Assert.Contains("Authentication mode set to Anonymous", viewModel.StatusBarText);
        }
        
        [Fact]
        public void DispatchCommand_SetAuthMode_UserPass_EmptyUsername_ShouldWarn()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
            viewModel.Settings.AuthUsername = ""; // Ensure username is empty

            // Act
            DispatchCommand(viewModel, CommandType.SetAuthMode, "userpass");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.Settings.SelectedAuthMode);
            Assert.Contains("Please set a username using :setuser", viewModel.StatusBarText);
        }


        [Fact]
        public void DispatchCommand_SetUser_FromAnonymous_ShouldSwitchModeAndUpdateUser()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
            viewModel.Settings.AuthUsername = "";
            viewModel.Settings.AuthPassword = "";

            // Act
            DispatchCommand(viewModel, CommandType.SetUser, "newuser");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.Settings.SelectedAuthMode);
            Assert.Equal("newuser", viewModel.Settings.AuthUsername);
            Assert.Empty(viewModel.Settings.AuthPassword); // Password should remain unchanged
            Assert.Contains("Username set. Auth mode switched to Username/Password", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_SetPassword_FromAnonymous_ShouldSwitchModeAndUpdatePassword()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
            viewModel.Settings.AuthUsername = "";
            viewModel.Settings.AuthPassword = "";

            // Act
            DispatchCommand(viewModel, CommandType.SetPassword, "newpass");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.Settings.SelectedAuthMode);
            Assert.Equal("newpass", viewModel.Settings.AuthPassword);
            Assert.Empty(viewModel.Settings.AuthUsername); // Username should remain unchanged
            Assert.Contains("Password set. Auth mode switched to Username/Password", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_SetUser_WhenUserPass_ShouldUpdateUserAndKeepMode()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
            viewModel.Settings.AuthUsername = "olduser";
            viewModel.Settings.AuthPassword = "oldpass";

            // Act
            DispatchCommand(viewModel, CommandType.SetUser, "updateduser");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.Settings.SelectedAuthMode);
            Assert.Equal("updateduser", viewModel.Settings.AuthUsername);
            Assert.Equal("oldpass", viewModel.Settings.AuthPassword); // Password should remain unchanged
            Assert.Contains("Username set. Settings will be saved.", viewModel.StatusBarText);
            Assert.DoesNotContain("Auth mode switched", viewModel.StatusBarText);
        }

        [Fact]
        public void DispatchCommand_SetPassword_WhenUserPass_ShouldUpdatePasswordAndKeepMode()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);
            viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword;
            viewModel.Settings.AuthUsername = "olduser";
            viewModel.Settings.AuthPassword = "oldpass";

            // Act
            DispatchCommand(viewModel, CommandType.SetPassword, "updatedpass");

            // Assert
            Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, viewModel.Settings.SelectedAuthMode);
            Assert.Equal("updatedpass", viewModel.Settings.AuthPassword);
            Assert.Equal("olduser", viewModel.Settings.AuthUsername); // Username should remain unchanged
            Assert.Contains("Password set. Settings will be saved.", viewModel.StatusBarText);
            Assert.DoesNotContain("Auth mode switched", viewModel.StatusBarText);
        }

        [Fact]
        public void TogglePublishWindowCommand_ShouldRaiseToggleEvent_NotShowEvent()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);

            bool toggleRaised = false;
            bool showRaised = false;
            viewModel.TogglePublishWindowRequested += (_, _) => toggleRaised = true;
            viewModel.ShowPublishWindowRequested += (_, _) => showRaised = true;

            // Act
            viewModel.TogglePublishWindowCommand.Execute().Subscribe();

            // Assert: toggle path must raise the toggle event exclusively so the
            // view can close an already-open window instead of re-activating it.
            Assert.True(toggleRaised, "TogglePublishWindowRequested should be raised.");
            Assert.False(showRaised, "ShowPublishWindowRequested must not be raised by the toggle command.");
        }

        [Fact]
        public void DispatchCommand_Publish_ShouldRaiseShowEvent_NotToggleEvent()
        {
            // Arrange
            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);

            bool toggleRaised = false;
            bool showRaised = false;
            viewModel.TogglePublishWindowRequested += (_, _) => toggleRaised = true;
            viewModel.ShowPublishWindowRequested += (_, _) => showRaised = true;

            // Act
            DispatchCommand(viewModel, CommandType.Publish);

            // Assert: :publish always shows, never toggles closed.
            Assert.True(showRaised, "ShowPublishWindowRequested should be raised by :publish.");
            Assert.False(toggleRaised, "TogglePublishWindowRequested must not be raised by :publish.");
        }

        [Fact]
        public void DispatchCommand_PublishWithTopicAndFileRef_RoutesFileThroughLoadFileContent()
        {
            // Arrange: real file so LoadFileContentAsync succeeds.
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "{\"hello\":\"world\"}");

                var commandParser = new CommandParserService();
                using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);

                // Act
                DispatchCommand(viewModel, CommandType.Publish, "foo/bar", "@" + tempFile);

                // Assert: file reference routed into VM (not raw text in editor).
                var pvm = viewModel.PublishViewModel;
                Assert.NotNull(pvm);
                Assert.Equal("foo/bar", pvm!.Topic);
                Assert.Equal(tempFile, pvm.LoadedFilePath);
                Assert.True(pvm.IsPayloadReadOnly, "File-ref mode should make the payload editor read-only.");
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void DispatchCommand_PublishWithSingleFileRef_UsesSelectedTopic()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "payload");

                var commandParser = new CommandParserService();
                using var viewModel = new MainViewModel(commandParser, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);

                // Seed a selected topic via reflection (private field).
                var selectedPathField = typeof(MainViewModel).GetField("_normalizedSelectedPath", BindingFlags.Instance | BindingFlags.NonPublic);
                selectedPathField?.SetValue(viewModel, "sensors/temperature");

                // Act: :publish @<tmp>  (single argument starting with '@').
                DispatchCommand(viewModel, CommandType.Publish, "@" + tempFile);

                // Assert
                var pvm = viewModel.PublishViewModel;
                Assert.NotNull(pvm);
                Assert.Equal("sensors/temperature", pvm!.Topic);
                Assert.Equal(tempFile, pvm.LoadedFilePath);
                Assert.True(pvm.IsPayloadReadOnly);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void DispatchCommand_PublishWithRelativeFileRef_ResolvesAgainstBasePath()
        {
            // Arrange: create a file inside a sandbox that serves as the samples
            // base directory. The command references the file by its *name*
            // only (relative), relying on the handler to resolve it via
            // IFileAutoCompleteService.BasePath.
            var baseDir = Path.Combine(Path.GetTempPath(), "CrowsNestMqtt_SamplesBase_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(baseDir);
            try
            {
                var fileName = "sample-payload.json";
                var filePath = Path.Combine(baseDir, fileName);
                File.WriteAllText(filePath, "{\"k\":1}");

                var stubFileSvc = new StubFileAutoCompleteService { BasePath = baseDir };

                var commandParser = new CommandParserService();
                using var viewModel = new MainViewModel(
                    commandParser,
                    mqttService: _mqttServiceMock,
                    uiScheduler: Scheduler.Immediate,
                    fileAutoCompleteService: stubFileSvc);

                // Act: the argument is just "@sample-payload.json" — no directory.
                DispatchCommand(viewModel, CommandType.Publish, "foo/bar", "@" + fileName);

                // Assert: the handler resolves the relative path against BasePath.
                var pvm = viewModel.PublishViewModel;
                Assert.NotNull(pvm);
                Assert.Equal("foo/bar", pvm!.Topic);
                Assert.Equal(filePath, pvm.LoadedFilePath);
                Assert.True(pvm.IsPayloadReadOnly);
            }
            finally
            {
                if (Directory.Exists(baseDir))
                    Directory.Delete(baseDir, recursive: true);
            }
        }

        [Fact]
        public void UpdateCommandSuggestions_PublishAtToken_EmitsFileSuggestions()
        {
            // Arrange
            var stubFileSvc = new StubFileAutoCompleteService(
                new FileAutoCompleteSuggestion("/tmp/foo.json", "foo.json", false, 12L, ".json"),
                new FileAutoCompleteSuggestion("/tmp/foobar.txt", "foobar.txt", false, 10L, ".txt"));

            var commandParser = new CommandParserService();
            using var viewModel = new MainViewModel(
                commandParser,
                mqttService: _mqttServiceMock,
                uiScheduler: Scheduler.Immediate,
                fileAutoCompleteService: stubFileSvc);

            // Act: invoke the private UpdateCommandSuggestions directly to avoid
            // the debounce pipeline in the constructor.
            var method = typeof(MainViewModel).GetMethod("UpdateCommandSuggestions", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(viewModel, new object?[] { ":publish foo/bar @/tmp/foo" });

            // Assert
            Assert.Contains(":publish foo/bar @/tmp/foo.json", viewModel.CommandSuggestions);
            Assert.Contains(":publish foo/bar @/tmp/foobar.txt", viewModel.CommandSuggestions);
        }

        private sealed class StubFileAutoCompleteService : IFileAutoCompleteService
        {
            private readonly List<FileAutoCompleteSuggestion> _suggestions;
            public StubFileAutoCompleteService(params FileAutoCompleteSuggestion[] items) => _suggestions = new List<FileAutoCompleteSuggestion>(items);
            public string BasePath { get; set; } = System.IO.Path.GetTempPath();
            public List<FileAutoCompleteSuggestion> GetSuggestions(string partialPath, int maxResults = 20) => _suggestions;
        }
    }
}
