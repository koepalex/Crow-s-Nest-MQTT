using CrowsNestMqtt.BusinessLogic.Commands;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class ParsedCommandTests
{
    [Fact]
    public void Constructor_ValidTypeAndArguments_CreatesInstance()
    {
        // Arrange
        var commandType = CommandType.Connect;
        var arguments = new List<string> { "localhost", "1883" };

        // Act
        var command = new ParsedCommand(commandType, arguments);

        // Assert
        Assert.Equal(commandType, command.Type);
        Assert.Equal(arguments, command.Arguments);
    }

    [Fact]
    public void Constructor_NullArguments_UsesEmptyList()
    {
        // Arrange
        var commandType = CommandType.Disconnect;

        // Act
        var command = new ParsedCommand(commandType, null!);

        // Assert
        Assert.Equal(commandType, command.Type);
        Assert.NotNull(command.Arguments);
        Assert.Empty(command.Arguments);
    }

    [Fact]
    public void Constructor_EmptyArguments_PreservesEmptyList()
    {
        // Arrange
        var commandType = CommandType.Clear;
        var arguments = new List<string>();

        // Act
        var command = new ParsedCommand(commandType, arguments);

        // Assert
        Assert.Equal(commandType, command.Type);
        Assert.Empty(command.Arguments);
    }

    [Fact]
    public void Constructor_MultipleArguments_PreservesAll()
    {
        // Arrange
        var commandType = CommandType.Export;
        var arguments = new List<string> { "json", "C:\\output.json", "topic/+" };

        // Act
        var command = new ParsedCommand(commandType, arguments);

        // Assert
        Assert.Equal(3, command.Arguments.Count);
        Assert.Equal("json", command.Arguments[0]);
        Assert.Equal("C:\\output.json", command.Arguments[1]);
        Assert.Equal("topic/+", command.Arguments[2]);
    }

    [Fact]
    public void Arguments_IsReadOnly()
    {
        // Arrange
        var commandType = CommandType.Filter;
        var arguments = new List<string> { "pattern" };
        var command = new ParsedCommand(commandType, arguments);

        // Act & Assert
        Assert.IsAssignableFrom<IReadOnlyList<string>>(command.Arguments);
    }

    [Theory]
    [InlineData(CommandType.Connect)]
    [InlineData(CommandType.Disconnect)]
    [InlineData(CommandType.Filter)]
    [InlineData(CommandType.Export)]
    [InlineData(CommandType.DeleteTopic)]
    [InlineData(CommandType.GotoResponse)]
    [InlineData(CommandType.Clear)]
    [InlineData(CommandType.Help)]
    public void Constructor_AllCommandTypes_WorksCorrectly(CommandType commandType)
    {
        // Arrange
        var arguments = new List<string> { "arg1" };

        // Act
        var command = new ParsedCommand(commandType, arguments);

        // Assert
        Assert.Equal(commandType, command.Type);
        Assert.Single(command.Arguments);
    }
}
