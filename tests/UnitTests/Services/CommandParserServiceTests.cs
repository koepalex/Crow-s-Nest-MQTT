using Xunit;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.BusinessLogic.Configuration; // Required for SettingsData

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
            Assert.Equal("Invalid arguments for :setauthmode. Expected: :setauthmode <anonymous|userpass>", result.ErrorMessage);
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
            Assert.Equal("Invalid arguments for :setauthmode. Expected: :setauthmode <anonymous|userpass>", result.ErrorMessage);
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
    }
}
