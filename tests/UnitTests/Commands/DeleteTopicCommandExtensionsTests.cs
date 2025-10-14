using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.Commands;
using NSubstitute;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Commands;

public class DeleteTopicCommandExtensionsTests
{
    private readonly ICommandProcessor _mockProcessor;
    private readonly IDeleteTopicService _mockDeleteService;

    public DeleteTopicCommandExtensionsTests()
    {
        _mockProcessor = Substitute.For<ICommandProcessor>();
        _mockDeleteService = Substitute.For<IDeleteTopicService>();
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithValidSingleTopic_ReturnsSuccess()
    {
        // Arrange
        var arguments = new[] { "test/topic" };
        var deleteResult = new DeleteTopicResult
        {
            Status = DeleteOperationStatus.CompletedSuccessfully,
            SummaryMessage = "Successfully deleted 1 topic"
        };

        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(deleteResult);

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Successfully deleted", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithWildcardPattern_ReturnsSuccess()
    {
        // Arrange
        var arguments = new[] { "sensor/+/temperature" };
        var deleteResult = new DeleteTopicResult
        {
            Status = DeleteOperationStatus.CompletedSuccessfully,
            SummaryMessage = "Successfully deleted 5 topics"
        };

        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(deleteResult);

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Successfully deleted", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithMultiLevelWildcard_ReturnsSuccess()
    {
        // Arrange
        var arguments = new[] { "sensor/#" };
        var deleteResult = new DeleteTopicResult
        {
            Status = DeleteOperationStatus.CompletedSuccessfully,
            SummaryMessage = "Successfully deleted 10 topics"
        };

        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(deleteResult);

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Successfully deleted", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithNoArguments_ReturnsError()
    {
        // Arrange
        var arguments = Array.Empty<string>();

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Topic pattern required", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithTooManyArguments_ReturnsError()
    {
        // Arrange
        var arguments = new[] { "topic1", "topic2" };

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid arguments", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithEmptyTopicPattern_ReturnsError()
    {
        // Arrange
        var arguments = new[] { "" };

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("cannot be empty", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithWhitespaceTopicPattern_ReturnsError()
    {
        // Arrange
        var arguments = new[] { "   " };

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("cannot be empty", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithInvalidWildcardUsage_ReturnsError()
    {
        // Arrange
        var arguments = new[] { "topic#/invalid" }; // # must be at end

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid topic pattern", result.Message);
    }


    [Fact]
    public async Task ExecuteDeleteTopicCommand_WhenOperationCancelled_ReturnsCancelledMessage()
    {
        // Arrange
        var arguments = new[] { "test/topic" };
        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeleteTopicResult>(new OperationCanceledException()));

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WhenExceptionThrown_ReturnsErrorMessage()
    {
        // Arrange
        var arguments = new[] { "test/topic" };
        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DeleteTopicResult>(new InvalidOperationException("Test error")));

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Test error", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_PassesCorrectConfigurationValues()
    {
        // Arrange
        var arguments = new[] { "test/topic" };
        DeleteTopicCommand? capturedCommand = null;

        _mockDeleteService.DeleteTopicAsync(Arg.Do<DeleteTopicCommand>(cmd => capturedCommand = cmd), Arg.Any<CancellationToken>())
            .Returns(new DeleteTopicResult
            {
                Status = DeleteOperationStatus.CompletedSuccessfully,
                SummaryMessage = "Success"
            });

        // Act
        await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.NotNull(capturedCommand);
        Assert.Equal("test/topic", capturedCommand.TopicPattern);
        Assert.Equal(500, capturedCommand.MaxTopicLimit); // Default from config
        Assert.Equal(4, capturedCommand.ParallelismDegree); // Default from config
        Assert.False(capturedCommand.RequireConfirmation); // Always false for UI commands
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithFailedStatus_ReturnsFailure()
    {
        // Arrange
        var arguments = new[] { "test/topic" };
        var deleteResult = new DeleteTopicResult
        {
            Status = DeleteOperationStatus.Failed,
            SummaryMessage = "Operation failed"
        };

        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(deleteResult);

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Operation failed", result.Message);
    }

    [Theory]
    [InlineData("sensor/+")]
    [InlineData("sensor/+/temp")]
    [InlineData("+/+/+")]
    [InlineData("sensor/#")]
    [InlineData("#")]
    public async Task ExecuteDeleteTopicCommand_WithValidWildcardPatterns_Succeeds(string topicPattern)
    {
        // Arrange
        var arguments = new[] { topicPattern };
        var deleteResult = new DeleteTopicResult
        {
            Status = DeleteOperationStatus.CompletedSuccessfully,
            SummaryMessage = "Success"
        };

        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Any<CancellationToken>())
            .Returns(deleteResult);

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_WithHashInMiddle_ReturnsError()
    {
        // Arrange
        var arguments = new[] { "topic/#/invalid" }; // # must be at end

        // Act
        var result = await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid topic pattern", result.Message);
    }

    [Fact]
    public async Task ExecuteDeleteTopicCommand_SupportsCustomCancellationToken()
    {
        // Arrange
        var arguments = new[] { "test/topic" };
        var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;

        _mockDeleteService.DeleteTopicAsync(Arg.Any<DeleteTopicCommand>(), Arg.Do<CancellationToken>(ct => capturedToken = ct))
            .Returns(new DeleteTopicResult
            {
                Status = DeleteOperationStatus.CompletedSuccessfully,
                SummaryMessage = "Success"
            });

        // Act
        await _mockProcessor.ExecuteDeleteTopicCommand(arguments, _mockDeleteService, cts.Token);

        // Assert
        Assert.Equal(cts.Token, capturedToken);
    }
}
