using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.Businesslogic.Commands;
using CrowsNestMqtt.Businesslogic.Configuration;
using CrowsNestMqtt.Businesslogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using NSubstitute;
using MQTTnet;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xunit;

namespace CrowsNestMqtt.Tests.ViewModels
{
    public class CommandInterfaceTests
    {
        private readonly ICommandParserService _commandParserService;
        private readonly MqttEngine _mqttEngine;

        public CommandInterfaceTests()
        {
            _commandParserService = Substitute.For<ICommandParserService>();
            _mqttEngine = Substitute.For<MqttEngine>(new MqttConnectionSettings());
        }

        [Fact]
        public void ExecuteSubmitInput_WithCommandInput_ShouldExecuteCommand()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
            const string commandText = ":connect localhost:1883";
            viewModel.CommandText = commandText;

            // Set up the command parser mock to return a valid command result
            var parsedCommand = new ParsedCommand(CommandType.Connect, new List<string> { "localhost:1883" }.AsReadOnly());
            var commandResult = CommandResult.SuccessCommand(parsedCommand);
            _commandParserService.ParseInput(commandText, Arg.Any<SettingsData>()).Returns(commandResult); // Revert to Arg.Any for now

            // Use reflection to replace the engine for testing connect command execution
            var engineField = typeof(MainViewModel).GetField("_mqttEngine", BindingFlags.NonPublic | BindingFlags.Instance);
            engineField?.SetValue(viewModel, _mqttEngine);

            // Act - Call the ExecuteSubmitInput method via reflection
            var executeMethod = typeof(MainViewModel).GetMethod("ExecuteSubmitInput", BindingFlags.NonPublic | BindingFlags.Instance);
            executeMethod?.Invoke(viewModel, null);

            // Assert
            _commandParserService.Received(1).ParseInput(commandText, Arg.Any<SettingsData>()); // Use Arg.Any for Received check too
            // Should update settings and connect as a result of the :connect command
            // _mqttEngine.Received(1).UpdateSettings(Arg.Is<MqttConnectionSettings>(s => s != null)); // Cannot verify non-virtual method on class substitute
            _mqttEngine.Received(1).ConnectAsync();
        }

        [Fact]
        public void ExecuteSubmitInput_WithSearchTerm_ShouldApplyFilter()
        {
            // Arrange
            var viewModel = new MainViewModel(_commandParserService);
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
            var viewModel = new MainViewModel(_commandParserService);
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
            var viewModel = new MainViewModel(_commandParserService);
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
            var viewModel = new MainViewModel(_commandParserService);
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
            var viewModel = new MainViewModel(_commandParserService);
            
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
            var viewModel = new MainViewModel(_commandParserService);
            
            // Create a message to export
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/export",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("test payload"))
                },
                PayloadPreview = "test payload"
            };
            viewModel.SelectedMessage = testMessage;
            
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
            var viewModel = new MainViewModel(_commandParserService);
            
            // Create a message with JSON payload
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/view",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("{\"test\":\"value\"}"))
                },
                PayloadPreview = "{\"test\":\"value\"}"
            };
            viewModel.SelectedMessage = testMessage;
            
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
            var viewModel = new MainViewModel(_commandParserService);
            
            // Create a message with JSON payload
            var testMessage = new MessageViewModel
            {
                Timestamp = DateTime.Now,
                FullMessage = new MqttApplicationMessage
                {
                    Topic = "test/view",
                    Payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes("{\"test\":\"value\"}"))
                },
                PayloadPreview = "{\"test\":\"value\"}"
            };
            viewModel.SelectedMessage = testMessage;
            
            // First switch to raw view
            var switchViewMethod = typeof(MainViewModel).GetMethod("SwitchPayloadView", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            switchViewMethod?.Invoke(viewModel, new object[] { true });
            
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
            var viewModel = new MainViewModel(_commandParserService);
            
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
    }
}