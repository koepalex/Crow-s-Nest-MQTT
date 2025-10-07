using CrowsNestMqtt.BusinessLogic.Commands;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class CommandResultTests
{
    [Fact]
    public void SuccessCommand_ValidCommand_CreatesSuccessResult()
    {
        // Arrange
        var parsedCommand = new ParsedCommand(CommandType.Connect, new List<string> { "localhost" });

        // Act
        var result = CommandResult.SuccessCommand(parsedCommand);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Connect, result.ParsedCommand.Type);
        Assert.Null(result.SearchTerm);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SuccessCommand_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CommandResult.SuccessCommand(null!));
    }

    [Fact]
    public void SuccessSearch_ValidSearchTerm_CreatesSuccessResult()
    {
        // Arrange
        var searchTerm = "test search";

        // Act
        var result = CommandResult.SuccessSearch(searchTerm);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ParsedCommand);
        Assert.Equal(searchTerm, result.SearchTerm);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SuccessSearch_NullSearchTerm_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CommandResult.SuccessSearch(null!));
    }

    [Fact]
    public void Failure_ValidMessage_CreatesFailureResult()
    {
        // Arrange
        var errorMessage = "Invalid command syntax";

        // Act
        var result = CommandResult.Failure(errorMessage);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.ParsedCommand);
        Assert.Null(result.SearchTerm);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void Failure_NullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CommandResult.Failure(null!));
    }

    [Fact]
    public void SuccessCommand_PreservesCommandArguments()
    {
        // Arrange
        var arguments = new List<string> { "arg1", "arg2", "arg3" };
        var parsedCommand = new ParsedCommand(CommandType.Export, arguments);

        // Act
        var result = CommandResult.SuccessCommand(parsedCommand);

        // Assert
        Assert.Equal(3, result.ParsedCommand!.Arguments.Count);
        Assert.Equal("arg1", result.ParsedCommand.Arguments[0]);
        Assert.Equal("arg2", result.ParsedCommand.Arguments[1]);
        Assert.Equal("arg3", result.ParsedCommand.Arguments[2]);
    }
}
