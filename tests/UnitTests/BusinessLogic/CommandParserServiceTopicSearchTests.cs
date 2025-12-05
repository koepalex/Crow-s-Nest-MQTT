using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Services;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic
{
    public class CommandParserServiceTopicSearchTests
    {
        private static readonly SettingsData DefaultSettings = new("localhost", 1883);

        [Fact]
        public void ParseInput_WithTopicSearchTerm_ReturnsTopicSearchCommand()
        {
            // Arrange
            var service = new CommandParserService();

            // Act
            var result = service.ParseInput("/request", DefaultSettings);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.TopicSearch, result.ParsedCommand!.Type);
            Assert.Single(result.ParsedCommand.Arguments);
            Assert.Equal("request", result.ParsedCommand.Arguments[0]);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/   ")]
        public void ParseInput_WithEmptyTopicSearch_CreatesTopicSearchCommandWithoutArguments(string input)
        {
            // Arrange
            var service = new CommandParserService();

            // Act
            var result = service.ParseInput(input, DefaultSettings);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.ParsedCommand);
            Assert.Equal(CommandType.TopicSearch, result.ParsedCommand!.Type);
            Assert.Empty(result.ParsedCommand.Arguments);
        }
    }
}
