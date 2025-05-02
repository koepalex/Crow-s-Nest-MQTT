using Xunit;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Exporter;
using Xunit.Sdk; // Required for LINQ operations like SequenceEqual

namespace CrowsNestMQTT.UnitTests;

public class CommandParserServiceTests
{
    private readonly CommandParserService _parser;
    private readonly SettingsData _settings;

    public CommandParserServiceTests()
    {
        // Assuming default settings are sufficient for parsing logic tests
        _settings = new SettingsData(
            "broker.hivemq.com",
            1883,
            "fake-clientid",
            KeepAliveIntervalSeconds: 60,
            CleanSession: true,
            SessionExpiryIntervalSeconds: 60,
            ExportFormat: ExportTypes.json,
            ExportPath: Path.GetTempPath());
        _parser = new CommandParserService();
    }

    // --- Valid Simple Commands (No Arguments) ---
    [Theory]
    [InlineData(":disconnect", CommandType.Disconnect)]
    [InlineData(":clear", CommandType.Clear)]
    [InlineData(":help", CommandType.Help)]
    public void ParseCommand_ValidSimpleCommands_ReturnsSuccess(string input, CommandType expectedType)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedType, result.ParsedCommand?.Type);
        Assert.Equal(0, result.ParsedCommand?.Arguments?.Count); // Simple commands should have null Arguments list
        Assert.Null(result.ErrorMessage);
    }

    // --- Valid Commands With Required Arguments ---
    [Theory]
    // Connect
    [InlineData(":connect broker.hivemq.com:1883", CommandType.Connect, new[] { "broker.hivemq.com:1883" })]
    public void ParseCommand_ValidCommandsWithArgs_ReturnsSuccessAndCorrectArgs(string input, CommandType expectedType, string[] expectedArgs)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(expectedType, result.ParsedCommand?.Type);
        Assert.NotNull(result.ParsedCommand?.Arguments);
        Assert.Equal(expectedArgs, result.ParsedCommand.Arguments);
        Assert.Null(result.ErrorMessage);
    }

    // --- Argument Parsing (Quoted Arguments) ---
    [Theory]
    // Connect with quoted args (less common but possible)
    [InlineData(":connect brokername:1883", CommandType.Connect, new[] { "brokername:1883" })]
    // Export with quoted path
    [InlineData(":export json \"path/with space/file.json\"", CommandType.Export, new[] { "json", "path/with space/file.json" })]
    public void ParseCommand_QuotedArguments_ReturnsSuccessAndCorrectArgs(string input, CommandType expectedType, string[] expectedArgs)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.True(result.IsSuccess, $"Input: '{input}' failed. Error: {result.ErrorMessage}");
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(expectedType, result.ParsedCommand?.Type);
        Assert.NotNull(result.ParsedCommand?.Arguments);
        Assert.True(expectedArgs.SequenceEqual(result.ParsedCommand.Arguments), $"Expected: [{string.Join(", ", expectedArgs.Select(a => $"\"{a}\""))}], Actual: [{string.Join(", ", result.ParsedCommand.Arguments.Select(a => $"\"{a}\""))}]");
        Assert.Null(result.ErrorMessage);
    }

    // --- Argument Parsing (Malformed Quoted Arguments) ---
    [Theory]
    [InlineData(":pub \"unbalanced topic payload")] // Unbalanced quote at start
    [InlineData(":pub topic \"unbalanced payload")] // Unbalanced quote in second arg
    [InlineData(":pub \"topic\"payload")] // Quote not followed by space
    [InlineData(":pub topic\" payload")] // Quote not preceded by space
    public void ParseCommand_MalformedQuotedArguments_ReturnsFailure(string input)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ParsedCommand);
        Assert.NotNull(result.ErrorMessage);
        // Optionally assert specific error message if the parser provides detailed errors
        // Assert.Contains("quote", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }


    // --- Invalid Input (Unknown Command) ---
    [Theory]
    [InlineData(":foo")]
    [InlineData("notacommand")]
    public void ParseCommand_UnknownOrEmptyCommand_ReturnsFailure(string? input)
    {
        var result = _parser.ParseCommand(input!, _settings);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ParsedCommand);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("command", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- Invalid Input (Incorrect Argument Count) ---
    [Theory]
    // Connect
    [InlineData(":connect broker")] // Missing port
    // Disconnect
    [InlineData(":disconnect arg")] // Too many args
    // Clear
    [InlineData(":clear arg")] // Too many args
    // Help
    [InlineData(":help arg1 arg2")] // Too many args
    // Export
    [InlineData(":export format path extra")] // Too many args
    // Filter
    [InlineData(":filter term1 term2")] // Too many args
    // Search
    [InlineData(":search term1 term2")] // Too many args
    public void ParseCommand_IncorrectArgumentCount_ReturnsFailure(string input)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ParsedCommand);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Invalid arguments", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- Specific Command Logic: Export ---
    [Fact]
    public void ParseCommand_ExportDefault_ReturnsSuccessWithDefaults()
    {
        var result = _parser.ParseCommand(":export", _settings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Export, result.ParsedCommand?.Type);
        // Default path might be null or a specific default, assuming null if not provided
        // Default format is likely 'txt' or 'json', assuming 'txt' based on typical defaults
        Assert.NotNull(result.ParsedCommand?.Arguments);
        Assert.NotNull(result.ParsedCommand?.Arguments);
        Assert.Equal(2, result.ParsedCommand.Arguments.Count);
        Assert.NotNull(result.ParsedCommand.Arguments[1]); // Default path from settings
        Assert.Equal("json", result.ParsedCommand?.Arguments[0]); // Default format from settings
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(":export txt path/to/file", "txt", "path/to/file")] // Default format
    [InlineData(":export json path/to/file.json", "json", "path/to/file.json")]
    [InlineData(":export txt path/to/file.txt", "txt", "path/to/file.txt")]
    [InlineData(":export txt \"path with space/file.csv\"", "txt", "path with space/file.csv")]
    public void ParseCommand_ExportWithArgs_ReturnsSuccessAndCorrectArgs(string input, string expectedFormat, string expectedPath)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.True(result.IsSuccess, $"Input: '{input}' failed. Error: {result.ErrorMessage}");
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Export, result.ParsedCommand?.Type);
        Assert.NotNull(result.ParsedCommand?.Arguments);
        Assert.Equal(2, result.ParsedCommand.Arguments.Count);
        Assert.Equal(expectedFormat, result.ParsedCommand.Arguments[0]);
        Assert.Equal(expectedPath, result.ParsedCommand.Arguments[1]);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData(":export path/to/file csv")] // Invalid format
    [InlineData(":export path/to/file xml")] // Invalid format
    public void ParseCommand_ExportInvalidFormat_ReturnsFailure(string input)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.False(result.IsSuccess);
        Assert.Null(result.ParsedCommand);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Invalid format", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // --- Specific Command Logic: Filter ---
    [Theory]
    [InlineData(":filter", CommandType.Filter, null)] // Clear filter
    [InlineData(":filter term", CommandType.Filter, new[] { "term" })]
    [InlineData(":filter \"search term with spaces\"", CommandType.Filter, new[] { "search term with spaces" })]
    public void ParseCommand_FilterCommand_ReturnsSuccessAndCorrectArgs(string input, CommandType expectedType, string[]? expectedArgs)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.True(result.IsSuccess, $"Input: '{input}' failed. Error: {result.ErrorMessage}");
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(expectedType, result.ParsedCommand.Type);
        if (expectedArgs == null)
        {
            Assert.NotNull(result.ParsedCommand.Arguments); // Clearing filter results in null args
            Assert.Empty(result.ParsedCommand.Arguments); 
        }
        else
        {
            Assert.NotNull(result.ParsedCommand.Arguments);
            Assert.Equal(expectedArgs, result.ParsedCommand.Arguments);
        }
        Assert.Null(result.ErrorMessage);
    }

    // --- Specific Command Logic: Search ---
    [Theory]
    [InlineData(":search", CommandType.Search, null)] // Clear search
    [InlineData(":search term", CommandType.Search, new[] { "term" })]
    public void ParseCommand_SearchCommand_ReturnsSuccessAndCorrectArgs(string input, CommandType expectedType, string[]? expectedArgs)
    {
        var result = _parser.ParseCommand(input, _settings);

        Assert.True(result.IsSuccess, $"Input: '{input}' failed. Error: {result.ErrorMessage}");
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(expectedType, result.ParsedCommand.Type);
         if (expectedArgs == null)
        {
            if (result.ParsedCommand.Arguments.Count != 0)
            {
                Assert.Empty(result.ParsedCommand.Arguments[0]); // Clearing search results in null args
            }
        }
            else
            {
                Assert.NotNull(result.ParsedCommand.Arguments);
                Assert.Equal(expectedArgs, result.ParsedCommand.Arguments);
            }
        Assert.Null(result.ErrorMessage);
    }
}