using Xunit;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.BusinessLogic.Services;

namespace UnitTests.BusinessLogic;

public class PublishCommandParserTests
{
    private readonly CommandParserService _parser = new();
    private readonly SettingsData _defaultSettings = new(
        "localhost",
        1883,
        ExportFormat: ExportTypes.json,
        ExportPath: ".");

    [Fact]
    public void ParseInput_PublishNoArgs_ReturnsPublishCommand()
    {
        var result = _parser.ParseInput(":publish", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Empty(result.ParsedCommand.Arguments);
    }

    [Fact]
    public void ParseInput_PublishWithTopic_ReturnsPublishCommandWithTopic()
    {
        var result = _parser.ParseInput(":publish my/topic", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Single(result.ParsedCommand.Arguments);
        Assert.Equal("my/topic", result.ParsedCommand.Arguments[0]);
    }

    [Fact]
    public void ParseInput_PublishWithTopicAndText_ReturnsPublishCommandWithArgs()
    {
        var result = _parser.ParseInput(":publish my/topic \"hello world\"", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Equal(2, result.ParsedCommand.Arguments.Count);
        Assert.Equal("my/topic", result.ParsedCommand.Arguments[0]);
        Assert.Equal("hello world", result.ParsedCommand.Arguments[1]);
    }

    [Fact]
    public void ParseInput_PublishWithTopicAndFileRef_ReturnsPublishCommandWithArgs()
    {
        var result = _parser.ParseInput(":publish my/topic @data.json", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Equal(2, result.ParsedCommand.Arguments.Count);
        Assert.Equal("my/topic", result.ParsedCommand.Arguments[0]);
        Assert.StartsWith("@", result.ParsedCommand.Arguments[1]);
    }

    [Theory]
    [InlineData(":Publish")]
    [InlineData(":PUBLISH")]
    public void ParseInput_PublishCaseInsensitive_ReturnsPublishCommand(string input)
    {
        var result = _parser.ParseInput(input, _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
    }

    [Fact]
    public void ParseInput_PublishWithMultiLevelTopic_ReturnsCorrectTopic()
    {
        var result = _parser.ParseInput(":publish sensors/temperature/room1 \"25.5\"", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Equal(2, result.ParsedCommand.Arguments.Count);
        Assert.Equal("sensors/temperature/room1", result.ParsedCommand.Arguments[0]);
        Assert.Equal("25.5", result.ParsedCommand.Arguments[1]);
    }

    [Fact]
    public void ParseInput_PublishWithQuotedPayload_HandlesQuotes()
    {
        var result = _parser.ParseInput(":publish test \"hello world with spaces\"", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Equal(2, result.ParsedCommand.Arguments.Count);
        Assert.Equal("test", result.ParsedCommand.Arguments[0]);
        Assert.Equal("hello world with spaces", result.ParsedCommand.Arguments[1]);
    }

    [Fact]
    public void ParseInput_PublishWithSingleFileRef_ReturnsSingleArgStartingWithAt()
    {
        // :publish @file.md should parse as a single argument so the handler
        // can interpret it as a file reference with the currently-selected
        // topic (not mistake it for the topic itself).
        var result = _parser.ParseInput(":publish @file.md", _defaultSettings);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedCommand);
        Assert.Equal(CommandType.Publish, result.ParsedCommand.Type);
        Assert.Single(result.ParsedCommand.Arguments);
        Assert.Equal("@file.md", result.ParsedCommand.Arguments[0]);
    }
}
