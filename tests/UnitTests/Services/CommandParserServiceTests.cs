using Xunit;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.BusinessLogic.Configuration; // Required for SettingsData
using CrowsNestMqtt.BusinessLogic.Exporter;

namespace CrowsNestMqtt.UnitTests.Services
{
    public class CommandParserServiceTests
    {
        private readonly CommandParserService _parser = new CommandParserService();
        private readonly SettingsData _defaultSettings = new SettingsData("testhost", 1883); // Default settings for parsing

        // Tests for :setauthmode
        [Theory]
        [InlineData("anonymous")]
        [InlineData("userpass")]
        public void ParseCommand_SetAuthMode_ValidArgs_ShouldSucceed(string mode)
        {
            // Arrange
            var input = $":setauthmode {mode}";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetAuthMode, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal(mode, result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_SetAuthMode_InvalidArg_ShouldFail()
        {
            // Arrange
            var input = ":setauthmode foobar";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setauthmode. Expected: :setauthmode <anonymous|userpass|enhanced>", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_SetAuthMode_NoArgs_ShouldFail()
        {
            // Arrange
            var input = ":setauthmode";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setauthmode. Expected: :setauthmode <anonymous|userpass|enhanced>", result.ErrorMessage);
        }

        // Tests for :setuser
        [Fact]
        public void ParseCommand_SetUser_ValidArg_ShouldSucceed()
        {
            // Arrange
            var input = ":setuser testuser";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetUser, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("testuser", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_SetUser_NoArgs_ShouldFail()
        {
            // Arrange
            var input = ":setuser";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setuser. Expected: :setuser <username>", result.ErrorMessage);
        }
        
        [Fact]
        public void ParseCommand_SetUser_MultipleArgs_ShouldFail()
        {
            // Arrange
            var input = ":setuser user1 user2"; // CommandParserService currently takes only the first part if not quoted

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);
            
            // Assert
            // Based on current SplitArguments, "user1 user2" without quotes becomes two arguments.
            // The parser for :setuser only expects one.
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setuser. Expected: :setuser <username>", result.ErrorMessage);
        }


        // Tests for :setpass
        [Fact]
        public void ParseCommand_SetPass_ValidArg_ShouldSucceed()
        {
            // Arrange
            var input = ":setpass testpassword";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetPassword, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("testpassword", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_SetPass_NoArgs_ShouldFail()
        {
            // Arrange
            var input = ":setpass";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setpass. Expected: :setpass <password>", result.ErrorMessage);
        }
        
        [Fact]
        public void ParseCommand_SetPass_MultipleArgs_ShouldFail()
        {
            // Arrange
            var input = ":setpass pass1 pass2";

            // Act
            var result = _parser.ParseCommand(input, _defaultSettings);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setpass. Expected: :setpass <password>", result.ErrorMessage);
        }

        // Tests for :setauthmethod
        [Fact]
        public void ParseCommand_SetAuthMethod_ValidArg_ShouldSucceed()
        {
            var input = ":setauthmethod SCRAM-SHA-1";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetAuthMethod, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("SCRAM-SHA-1", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_SetAuthMethod_NoArgs_ShouldFail()
        {
            var input = ":setauthmethod";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setauthmethod. Expected: :setauthmethod <method>", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_SetAuthMethod_MultipleArgs_ShouldFail()
        {
            var input = ":setauthmethod SCRAM-SHA-1 extra";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setauthmethod. Expected: :setauthmethod <method>", result.ErrorMessage);
        }

        // Tests for :setauthdata
        [Fact]
        public void ParseCommand_SetAuthData_ValidArg_ShouldSucceed()
        {
            var input = ":setauthdata someData";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetAuthData, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("someData", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_SetAuthData_NoArgs_ShouldFail()
        {
            var input = ":setauthdata";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setauthdata. Expected: :setauthdata <data>", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_SetAuthData_MultipleArgs_ShouldFail()
        {
            var input = ":setauthdata foo bar";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setauthdata. Expected: :setauthdata <data>", result.ErrorMessage);
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public void ParseCommand_SetUseTls_ValidArgs_ShouldSucceed(string value)
        {
            var input = $":setusetls {value}";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetUseTls, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal(value, result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_SetUseTls_InvalidArg_ShouldFail()
        {
            var input = ":setusetls maybe";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid argument for :setusetls. Expected: :setusetls <true|false>", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_SetUseTls_NoArgs_ShouldFail()
        {
            var input = ":setusetls";
            var result = _parser.ParseCommand(input, _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("Invalid arguments for :setusetls. Expected: :setusetls <true|false>", result.ErrorMessage);
        }

        // Tests for ParseInput method
        [Fact]
        public void ParseInput_EmptyString_ShouldReturnSuccessSearch()
        {
            var result = _parser.ParseInput("", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal(string.Empty, result.SearchTerm);
        }

        [Fact]
        public void ParseInput_WhitespaceOnly_ShouldReturnSuccessSearch()
        {
            var result = _parser.ParseInput("   ", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal(string.Empty, result.SearchTerm);
        }

        [Fact]
        public void ParseInput_Null_ShouldReturnSuccessSearch()
        {
            var result = _parser.ParseInput(null!, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal(string.Empty, result.SearchTerm);
        }

        [Fact]
        public void ParseInput_SearchTerm_ShouldReturnSuccessSearch()
        {
            var input = "test search term";
            var result = _parser.ParseInput(input, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal(input, result.SearchTerm);
        }

        [Fact]
        public void ParseInput_SearchTermWithWhitespace_ShouldReturnTrimmedSearchTerm()
        {
            var input = "  test search term  ";
            var result = _parser.ParseInput(input, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal("test search term", result.SearchTerm);
        }

        [Fact]
        public void ParseInput_CommandStartingWithColon_ShouldParseCommand()
        {
            var input = ":help";
            var result = _parser.ParseInput(input, _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Help, result.ParsedCommand.Type);
        }

        // Tests for ParseCommand method - additional commands
        [Fact]
        public void ParseCommand_EmptyString_ShouldReturnSuccessSearch()
        {
            var result = _parser.ParseCommand("", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal(string.Empty, result.SearchTerm);
        }

        [Fact]
        public void ParseCommand_WhitespaceOnly_ShouldReturnSuccessSearch()
        {
            var result = _parser.ParseCommand("   ", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.Null(result.ParsedCommand);
            Assert.Equal(string.Empty, result.SearchTerm);
        }

        [Fact]
        public void ParseCommand_EmptyCommand_ShouldFail()
        {
            var result = _parser.ParseCommand(":", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Empty command.", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_UnknownCommand_ShouldFail()
        {
            var result = _parser.ParseCommand(":unknown", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Unknown command: 'unknown'", result.ErrorMessage);
        }

        // Connect command tests
        [Fact]
        public void ParseCommand_Connect_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":connect", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Connect, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Connect_ValidServerPort_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":connect localhost:1883", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Connect, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("localhost:1883", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_Connect_ValidServerPortUsername_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":connect localhost:1883 testuser", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Connect, result.ParsedCommand.Type);
            Assert.Equal(2, result.ParsedCommand.Arguments.Count);
            Assert.Equal("localhost:1883", result.ParsedCommand.Arguments[0]);
            Assert.Equal("testuser", result.ParsedCommand.Arguments[1]);
        }

        [Fact]
        public void ParseCommand_Connect_ValidServerPortUsernamePassword_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":connect localhost:1883 testuser testpass", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Connect, result.ParsedCommand.Type);
            Assert.Equal(3, result.ParsedCommand.Arguments.Count);
            Assert.Equal("localhost:1883", result.ParsedCommand.Arguments[0]);
            Assert.Equal("testuser", result.ParsedCommand.Arguments[1]);
            Assert.Equal("testpass", result.ParsedCommand.Arguments[2]);
        }

        [Fact]
        public void ParseCommand_Connect_InvalidServerPortFormat_ShouldFail()
        {
            var result = _parser.ParseCommand(":connect invalidformat", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :connect. If one argument is provided, it must be in 'server:port' format.", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_Connect_InvalidPortRange_ShouldFail()
        {
            var result = _parser.ParseCommand(":connect localhost:99999", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid server:port format in :connect command. Port must be between 1 and 65535.", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_Connect_TooManyArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":connect localhost:1883 user pass extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :connect. Too many arguments. Expected: :connect [<server:port>] [<username>] [<password>]", result.ErrorMessage);
        }

        // Disconnect command tests
        [Fact]
        public void ParseCommand_Disconnect_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":disconnect", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Disconnect, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Disconnect_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":disconnect extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :disconnect. Expected: :disconnect", result.ErrorMessage);
        }

        // Export command tests
        [Fact]
        public void ParseCommand_Export_ValidJsonFormat_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":export json /path/to/file.json", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Export, result.ParsedCommand.Type);
            Assert.Equal(2, result.ParsedCommand.Arguments.Count);
            Assert.Equal("json", result.ParsedCommand.Arguments[0]);
            Assert.Equal("/path/to/file.json", result.ParsedCommand.Arguments[1]);
        }

        [Fact]
        public void ParseCommand_Export_ValidTxtFormat_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":export txt /path/to/file.txt", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Export, result.ParsedCommand.Type);
            Assert.Equal(2, result.ParsedCommand.Arguments.Count);
            Assert.Equal("txt", result.ParsedCommand.Arguments[0]);
            Assert.Equal("/path/to/file.txt", result.ParsedCommand.Arguments[1]);
        }

        [Fact]
        public void ParseCommand_Export_InvalidFormat_ShouldFail()
        {
            var result = _parser.ParseCommand(":export xml /path/to/file.xml", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid format for :export. Expected 'json' or 'txt'.", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_Export_NoArgsWithValidSettings_ShouldSucceed()
        {
            var settingsWithExport = new SettingsData("testhost", 1883)
            {
                ExportFormat = ExportTypes.json,
                ExportPath = "/path/to/export.json"
            };
            var result = _parser.ParseCommand(":export", settingsWithExport);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Export, result.ParsedCommand.Type);
            Assert.Equal(2, result.ParsedCommand.Arguments.Count);
            Assert.Equal("json", result.ParsedCommand.Arguments[0]);
            Assert.Equal("/path/to/export.json", result.ParsedCommand.Arguments[1]);
        }

        [Fact]
        public void ParseCommand_Export_NoArgsWithInvalidSettings_ShouldFail()
        {
            var result = _parser.ParseCommand(":export", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :export. Expected: :export <format:{json|txt}> <filepath>", result.ErrorMessage);
        }

        // Filter command tests
        [Fact]
        public void ParseCommand_Filter_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":filter", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Filter, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Filter_WithPattern_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":filter test.*pattern", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Filter, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("test.*pattern", result.ParsedCommand.Arguments[0]);
        }

        // Clear command tests
        [Fact]
        public void ParseCommand_Clear_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":clear", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Clear, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Clear_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":clear extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :clear_messages. Expected: :clear_messages", result.ErrorMessage);
        }

        // Help command tests
        [Fact]
        public void ParseCommand_Help_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":help", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Help, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Help_WithCommand_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":help connect", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Help, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("connect", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_Help_TooManyArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":help connect extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :help. Expected: :help [command_name]", result.ErrorMessage);
        }

        // Pause command tests
        [Fact]
        public void ParseCommand_Pause_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":pause", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Pause, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Pause_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":pause extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :pause. Expected: :pause", result.ErrorMessage);
        }

        // Resume command tests
        [Fact]
        public void ParseCommand_Resume_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":resume", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Resume, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Resume_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":resume extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :resume. Expected: :resume", result.ErrorMessage);
        }

        // Copy command tests
        [Fact]
        public void ParseCommand_Copy_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":copy", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Copy, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Copy_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":copy extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :copy. Expected: :copy", result.ErrorMessage);
        }

        // Search command tests
        [Fact]
        public void ParseCommand_Search_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":search", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Search, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Search_WithTerm_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":search test", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Search, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("test", result.ParsedCommand.Arguments[0]);
        }

        // Expand command tests
        [Fact]
        public void ParseCommand_Expand_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":expand", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Expand, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Expand_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":expand extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :expand. Expected: :expand", result.ErrorMessage);
        }

        // Collapse command tests
        [Fact]
        public void ParseCommand_Collapse_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":collapse", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Collapse, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Collapse_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":collapse extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :collapse. Expected: :collapse", result.ErrorMessage);
        }

        // View command tests
        [Theory]
        [InlineData("raw", CommandType.ViewRaw)]
        [InlineData("json", CommandType.ViewJson)]
        [InlineData("image", CommandType.ViewImage)]
        [InlineData("video", CommandType.ViewVideo)]
        public void ParseCommand_View_ValidTypes_ShouldSucceed(string viewType, CommandType expectedCommandType)
        {
            var result = _parser.ParseCommand($":view {viewType}", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(expectedCommandType, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal(viewType, result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_View_InvalidType_ShouldFail()
        {
            var result = _parser.ParseCommand(":view invalid", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :view. Expected: :view <raw|json|image|video|hex>", result.ErrorMessage);
        }

        [Fact]
        public void ParseCommand_View_NoArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":view", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :view. Expected: :view <raw|json|image|video|hex>", result.ErrorMessage);
        }

        // Settings command tests
        [Fact]
        public void ParseCommand_Settings_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":settings", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Settings, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_Settings_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":settings extra", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :settings. Expected: :settings", result.ErrorMessage);
        }

        // GotoResponse command tests
        [Fact]
        public void ParseCommand_GotoResponse_NoArgs_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":gotoresponse", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.GotoResponse, result.ParsedCommand.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }

        [Fact]
        public void ParseCommand_GotoResponse_WithArgs_ShouldFail()
        {
            var result = _parser.ParseCommand(":gotoresponse message-id-123", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid arguments for :gotoresponse. Expected: :gotoresponse", result.ErrorMessage);
        }

        // Tests for argument parsing with quotes
        [Fact]
        public void ParseCommand_WithQuotedArguments_ShouldParseCorrectly()
        {
            var result = _parser.ParseCommand(":setuser \"user with spaces\"", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetUser, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("user with spaces", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_WithPartialQuotes_ShouldParseCorrectly()
        {
            var result = _parser.ParseCommand(":setpass \"password", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.SetPassword, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("password", result.ParsedCommand.Arguments[0]);
        }

        // Edge case tests
        [Fact]
        public void ParseCommand_CaseInsensitive_ShouldWork()
        {
            var result = _parser.ParseCommand(":HELP", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Help, result.ParsedCommand.Type);
        }

        [Fact]
        public void ParseCommand_ConnectWithIPAddress_ShouldSucceed()
        {
            var result = _parser.ParseCommand(":connect 192.168.1.100:1883", _defaultSettings);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.Connect, result.ParsedCommand.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("192.168.1.100:1883", result.ParsedCommand.Arguments[0]);
        }

        [Fact]
        public void ParseCommand_ConnectWithZeroPort_ShouldFail()
        {
            var result = _parser.ParseCommand(":connect localhost:0", _defaultSettings);
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid server:port format in :connect command. Port must be between 1 and 65535.", result.ErrorMessage);
        }
    }
}
