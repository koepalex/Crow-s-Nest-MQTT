using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.Utils;

namespace CrowsNestMqtt.UnitTests;

/// <summary>
/// Unit tests for DeleteTopicService error handling and edge cases.
/// Tests service behavior in isolation with mocked dependencies.
/// </summary>
public class DeleteTopicServiceTests
{
    private readonly IMqttService _mockMqttService;
    private readonly ILogger<DeleteTopicService> _mockLogger;

    public DeleteTopicServiceTests()
    {
        _mockMqttService = Substitute.For<IMqttService>();
        _mockLogger = Substitute.For<ILogger<DeleteTopicService>>();
    }

    [Fact]
    public void Constructor_WithNullMqttService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteTopicService(null!, _mockLogger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DeleteTopicService(_mockMqttService, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesService()
    {
        // Act
        var service = new DeleteTopicService(_mockMqttService, _mockLogger);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task DeleteTopicAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.DeleteTopicAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteTopicAsync_WithExactTopicName_DirectlyClearsRetainedMessage()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "nonexistent/topic" };

        // Mock no topics found in buffer (this should be bypassed for exact topic names)
        _mockMqttService.GetBufferedTopics().Returns(new List<string>());

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DeleteOperationStatus.CompletedSuccessfully, result.Status);
        Assert.Equal(1, result.TotalTopicsFound); // Direct topic deletion bypasses discovery
        Assert.Equal(1, result.SuccessfulDeletions); // Should attempt to clear the retained message
        Assert.Empty(result.FailedDeletions);

        // Verify that ClearRetainedMessageAsync was called for the exact topic
        await _mockMqttService.Received(1).ClearRetainedMessageAsync("nonexistent/topic",
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTopicAsync_WithWildcardPatternAndNoTopics_ReturnsSuccessWithZeroCount()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/+/temperature" }; // Wildcard pattern

        // Mock no topics found
        _mockMqttService.GetBufferedTopics().Returns(new List<string>());

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DeleteOperationStatus.CompletedSuccessfully, result.Status);
        Assert.Equal(0, result.TotalTopicsFound); // Should use discovery for wildcard patterns
        Assert.Equal(0, result.SuccessfulDeletions);
        Assert.Empty(result.FailedDeletions);

        // Should not call ClearRetainedMessageAsync when no topics found via discovery
        await _mockMqttService.DidNotReceive().ClearRetainedMessageAsync(Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteTopicAsync_WithInvalidTopicPattern_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "" }; // Invalid empty pattern

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Contains("Topic pattern cannot be empty", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_ExceedsLimitWithoutConfirmation_ReturnsAwaitingConfirmation()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand
        {
            TopicPattern = "sensor/#",
            MaxTopicLimit = 5,
            RequireConfirmation = false
        };

        // Mock many topics found (exceeds limit)
        var topics = new[] { "sensor/1", "sensor/2", "sensor/3", "sensor/4", "sensor/5", "sensor/6" };
        _mockMqttService.GetBufferedTopics().Returns(topics);

        // Mock all topics have retained messages
        foreach (var topic in topics)
        {
            var messages = new[]
            {
                new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic))
            };
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(messages);
        }

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DeleteOperationStatus.AwaitingConfirmation, result.Status);
        Assert.Equal(6, result.TotalTopicsFound);
        Assert.Contains("Confirmation required", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_WithCancellation_ReturnsCancelledResult()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/temperature" };
        using var cts = new CancellationTokenSource();

        // Setup topics
        _mockMqttService.GetBufferedTopics().Returns(new[] { "sensor/temperature" });
        var message = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage("sensor/temperature"));
        _mockMqttService.GetBufferedMessagesForTopic("sensor/temperature").Returns(new[] { message });

        // Mock publish to throw cancellation
        _mockMqttService.PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(new CancellationToken(true)));

        // Cancel immediately
        cts.Cancel();

        // Act
        var result = await service.DeleteTopicAsync(command, cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DeleteOperationStatus.Aborted, result.Status);
        Assert.True(result.WasCancelled);
    }

    [Fact]
    public async Task FindTopicsWithRetainedMessages_WithValidPattern_ReturnsMatchingTopics()
    {
        // Arrange
        var service = CreateService();
        var topicPattern = "sensor/+";

        // Mock topics
        var allTopics = new[] { "sensor/temperature", "sensor/humidity", "other/data" };
        _mockMqttService.GetBufferedTopics().Returns(allTopics);

        // Mock retained messages for sensor topics only
        var retainedMessage1 = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage("sensor/temperature"));
        var retainedMessage2 = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage("sensor/humidity"));
        var nonRetainedMessage = new BufferedMqttMessage(Guid.NewGuid(), CreateNonRetainedMessage("other/data"));

        _mockMqttService.GetBufferedMessagesForTopic("sensor/temperature").Returns(new[] { retainedMessage1 });
        _mockMqttService.GetBufferedMessagesForTopic("sensor/humidity").Returns(new[] { retainedMessage2 });
        _mockMqttService.GetBufferedMessagesForTopic("other/data").Returns(new[] { nonRetainedMessage });

        // Act
        var result = await service.FindTopicsWithRetainedMessages(topicPattern);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains("sensor/temperature", result);
        Assert.Contains("sensor/humidity", result);
        Assert.DoesNotContain("other/data", result);
    }

    [Fact]
    public async Task EstimatePerformanceImpact_WithVariousTopicCounts_ReturnsReasonableEstimates()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var smallResult = await service.EstimatePerformanceImpact(10);
        Assert.True(smallResult.TopicsPerSecond > 0);
        Assert.True(smallResult.UIRemainedResponsive);

        var largeResult = await service.EstimatePerformanceImpact(1000);
        Assert.True(largeResult.TopicsPerSecond > 0);
        Assert.True(largeResult.TopicsPerSecond >= smallResult.TopicsPerSecond); // Large batches should be more efficient
    }

    [Theory]
    [InlineData("sensor/temperature", "sensor/temperature", true)]
    [InlineData("sensor/+", "sensor/temperature", true)]
    [InlineData("sensor/#", "sensor/room1/temperature", true)]
    [InlineData("sensor/+/temp", "sensor/room1/temp", true)]
    [InlineData("sensor/temperature", "sensor/humidity", false)]
    [InlineData("sensor/+", "building/temperature", false)]
    public async Task TopicMatching_WithVariousPatterns_WorksCorrectly(string pattern, string topic, bool shouldMatch)
    {
        // This tests the private IsTopicMatchingPattern method indirectly through FindTopicsWithRetainedMessages
        // Arrange
        var service = CreateService();

        var allTopics = new[] { topic };
        _mockMqttService.GetBufferedTopics().Returns(allTopics);

        var message = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
        _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { message });

        // Act
        var result = await service.FindTopicsWithRetainedMessages(pattern);

        // Assert
        if (shouldMatch)
        {
            Assert.Contains(topic, result);
        }
        else
        {
            Assert.DoesNotContain(topic, result);
        }
    }

    #region Error Classification Tests

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsMqttCommunicationException_ClassifiesAsNetworkError()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/temp" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new MQTTnet.Exceptions.MqttCommunicationException("Connection lost"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.NetworkError, result.FailedDeletions[0].ErrorType);
        Assert.True(result.FailedDeletions[0].IsRetryable);
        Assert.Contains("Connection lost", result.FailedDeletions[0].ErrorMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsOperationCanceled_ClassifiesAsTimeout()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/temp" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new OperationCanceledException("The operation timed out"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.Timeout, result.FailedDeletions[0].ErrorType);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsUnauthorizedAccessException_ClassifiesAsPermissionDenied()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "restricted/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new UnauthorizedAccessException("No publish permission"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.PermissionDenied, result.FailedDeletions[0].ErrorType);
        Assert.False(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsGenericException_ClassifiesAsUnknown()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "some/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new Exception("Something unexpected happened"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.Unknown, result.FailedDeletions[0].ErrorType);
        Assert.False(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsArgumentException_ClassifiesAsInvalidTopic()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "bad/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new ArgumentException("Invalid topic name"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.InvalidTopic, result.FailedDeletions[0].ErrorType);
        Assert.False(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsMqttProtocolViolationException_ClassifiesAsBrokerError()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "proto/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new MQTTnet.Exceptions.MqttProtocolViolationException("Protocol violation"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.BrokerError, result.FailedDeletions[0].ErrorType);
        Assert.False(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsInvalidOperationNotConnected_ClassifiesAsNetworkError()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "offline/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new InvalidOperationException("Client is not connected"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.NetworkError, result.FailedDeletions[0].ErrorType);
        Assert.True(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenClearThrowsInvalidOperationOther_ClassifiesAsUnknown()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "other/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new InvalidOperationException("Some other invalid state"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Single(result.FailedDeletions);
        Assert.Equal(DeletionErrorType.Unknown, result.FailedDeletions[0].ErrorType);
        Assert.False(result.FailedDeletions[0].IsRetryable);
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task DeleteTopicAsync_MqttCommunicationException_IsRetryable()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "retry/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new MQTTnet.Exceptions.MqttCommunicationException("Transient failure"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Single(result.FailedDeletions);
        Assert.True(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_OperationCanceledWithTimeoutMessage_IsRetryable()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "timeout/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new OperationCanceledException("The operation timeout occurred"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Single(result.FailedDeletions);
        Assert.True(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_UnauthorizedAccessException_IsNotRetryable()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "perm/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new UnauthorizedAccessException("Forbidden"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Single(result.FailedDeletions);
        Assert.False(result.FailedDeletions[0].IsRetryable);
    }

    [Fact]
    public async Task DeleteTopicAsync_InvalidOperationNotConnected_IsRetryable()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "disc/topic" };

        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new InvalidOperationException("Client is not connected to broker"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Single(result.FailedDeletions);
        Assert.True(result.FailedDeletions[0].IsRetryable);
    }

    #endregion

    #region Summary Message Generation Tests

    [Fact]
    public async Task DeleteTopicAsync_AllSuccesses_SummaryIndicatesAllDeleted()
    {
        // Arrange
        var service = CreateService();
        var topics = new[] { "sensor/a", "sensor/b", "sensor/c" };
        var command = new DeleteTopicCommand { TopicPattern = "sensor/#" };

        _mockMqttService.GetBufferedTopics().Returns(topics);
        foreach (var topic in topics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }
        // ClearRetainedMessageAsync succeeds by default (returns completed task)

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.CompletedSuccessfully, result.Status);
        Assert.Equal(3, result.SuccessfulDeletions);
        Assert.Empty(result.FailedDeletions);
        Assert.Contains("3 topics cleared successfully", result.SummaryMessage);
        Assert.DoesNotContain("failed", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_MixedSuccessesAndFailures_SummaryContainsBothCounts()
    {
        // Arrange
        var service = CreateService();
        var topics = new[] { "sensor/ok1", "sensor/fail1", "sensor/ok2" };
        var command = new DeleteTopicCommand { TopicPattern = "sensor/#" };

        _mockMqttService.GetBufferedTopics().Returns(topics);
        foreach (var topic in topics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }

        // Fail only for sensor/fail1
        _mockMqttService.ClearRetainedMessageAsync(
            "sensor/fail1",
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new MQTTnet.Exceptions.MqttCommunicationException("Network error"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.CompletedWithWarnings, result.Status);
        Assert.Equal(2, result.SuccessfulDeletions);
        Assert.Single(result.FailedDeletions);
        Assert.Contains("2 topics cleared successfully", result.SummaryMessage);
        Assert.Contains("1 topics failed", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_AllFailures_SummaryIndicatesAllFailed()
    {
        // Arrange
        var service = CreateService();
        var topics = new[] { "sensor/x", "sensor/y" };
        var command = new DeleteTopicCommand { TopicPattern = "sensor/#" };

        _mockMqttService.GetBufferedTopics().Returns(topics);
        foreach (var topic in topics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }

        // All calls fail
        _mockMqttService.ClearRetainedMessageAsync(
            Arg.Any<string>(),
            Arg.Any<MQTTnet.Protocol.MqttQualityOfServiceLevel>(),
            Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new UnauthorizedAccessException("Denied"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Equal(0, result.SuccessfulDeletions);
        Assert.Equal(2, result.FailedDeletions.Count);
        Assert.Contains("2 topics failed", result.SummaryMessage);
        Assert.DoesNotContain("cleared successfully", result.SummaryMessage);
    }

    #endregion

    #region Wildcard Edge Cases

    [Fact]
    public async Task DeleteTopicAsync_HashOnlyPattern_MatchesAllTopicsWithWarning()
    {
        // Arrange
        var service = CreateService();
        var topics = new[] { "a", "b/c", "d/e/f" };
        var command = new DeleteTopicCommand { TopicPattern = "#" };

        _mockMqttService.GetBufferedTopics().Returns(topics);
        foreach (var topic in topics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.CompletedSuccessfully, result.Status);
        Assert.Equal(3, result.SuccessfulDeletions);
        // Summary should include warnings about broad pattern
        Assert.Contains("warnings", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_PlusAtStart_MatchesSingleLevel()
    {
        // Arrange
        var service = CreateService();
        var allTopics = new[] { "room1/temp", "room2/temp", "building/floor/temp" };
        var command = new DeleteTopicCommand { TopicPattern = "+/temp" };

        _mockMqttService.GetBufferedTopics().Returns(allTopics);
        foreach (var topic in allTopics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert - should match room1/temp and room2/temp but not building/floor/temp
        Assert.Equal(2, result.SuccessfulDeletions);
    }

    [Fact]
    public async Task DeleteTopicAsync_PlusInMiddle_MatchesSingleLevelInMiddle()
    {
        // Arrange
        var service = CreateService();
        var allTopics = new[] { "home/room1/temp", "home/room2/temp", "home/room1/sub/temp" };
        var command = new DeleteTopicCommand { TopicPattern = "home/+/temp" };

        _mockMqttService.GetBufferedTopics().Returns(allTopics);
        foreach (var topic in allTopics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert - + matches single level only, not multi-level
        Assert.Equal(2, result.SuccessfulDeletions);
    }

    [Fact]
    public async Task DeleteTopicAsync_WildcardMatchesZeroTopics_ReturnsSuccessWithZeroCount()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "nonexistent/#" };

        _mockMqttService.GetBufferedTopics().Returns(new[] { "other/topic1", "other/topic2" });
        // No retained messages match the pattern

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.CompletedSuccessfully, result.Status);
        Assert.Equal(0, result.TotalTopicsFound);
        Assert.Equal(0, result.SuccessfulDeletions);
        Assert.Contains("No topics found", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_PlusOnlyPattern_MatchesSingleLevelTopics()
    {
        // Arrange
        var service = CreateService();
        var allTopics = new[] { "topicA", "topicB", "multi/level" };
        var command = new DeleteTopicCommand { TopicPattern = "+" };

        _mockMqttService.GetBufferedTopics().Returns(allTopics);
        foreach (var topic in allTopics)
        {
            var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage(topic));
            _mockMqttService.GetBufferedMessagesForTopic(topic).Returns(new[] { msg });
        }

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert - + matches single level only: topicA, topicB but not multi/level
        Assert.Equal(2, result.SuccessfulDeletions);
    }

    #endregion

    #region Outer Exception Handling Tests

    [Fact]
    public async Task DeleteTopicAsync_WhenGetBufferedTopicsThrows_ReturnsFailedResult()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/#" };

        _mockMqttService.GetBufferedTopics()
            .Returns<IEnumerable<string>>(x => throw new Exception("Service unavailable"));

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Contains("Service unavailable", result.SummaryMessage);
    }

    [Fact]
    public async Task DeleteTopicAsync_WhenCancelledViaCancellationToken_ReturnsAbortedResult()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/#" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockMqttService.GetBufferedTopics().Returns(new[] { "sensor/a" });
        var msg = new BufferedMqttMessage(Guid.NewGuid(), CreateRetainedMessage("sensor/a"));
        _mockMqttService.GetBufferedMessagesForTopic("sensor/a").Returns(new[] { msg });

        // Act
        var result = await service.DeleteTopicAsync(command, cts.Token);

        // Assert
        Assert.Equal(DeleteOperationStatus.Aborted, result.Status);
        Assert.True(result.WasCancelled);
        Assert.Contains("cancelled", result.SummaryMessage);
    }

    #endregion

    #region Validation Edge Cases

    [Theory]
    [InlineData("topic/mid#dle", true)]  // # not at end
    [InlineData("topic/#", false)]        // valid: # at end after /
    [InlineData("#", false)]              // valid: # alone
    [InlineData("topic/a+b", true)]       // + mixed with text
    public void ValidateDeleteOperation_InvalidWildcardPlacement_ReturnsExpectedResult(string pattern, bool shouldFail)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateDeleteOperation(pattern, 500);

        // Assert
        if (shouldFail)
        {
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.ErrorMessages);
        }
        else
        {
            Assert.True(result.IsValid);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateDeleteOperation_InvalidMaxTopicLimit_ReturnsFailure(int limit)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateDeleteOperation("valid/topic", limit);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.ErrorMessages, m => m.Contains("greater than 0"));
    }

    [Fact]
    public void ValidateDeleteOperation_ExceedsSystemMaxLimit_ReturnsFailure()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateDeleteOperation("valid/topic", 10001);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.ErrorMessages, m => m.Contains("exceeds system maximum"));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("+")]
    [InlineData("sensor/+")]
    [InlineData("sensor/#")]
    public void ValidateDeleteOperation_BroadPatterns_ReturnsWarnings(string pattern)
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateDeleteOperation(pattern, 500);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.WarningMessages);
        Assert.Contains(result.WarningMessages, w => w.Contains("large number"));
    }

    [Fact]
    public void ValidateDeleteOperation_ExactTopicPattern_NoWarnings()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.ValidateDeleteOperation("sensor/temperature", 500);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.ErrorMessages);
        Assert.Empty(result.WarningMessages);
    }

    #endregion

    #region Metrics and Result Properties Tests

    [Fact]
    public async Task DeleteTopicAsync_SuccessfulDeletion_HasMetrics()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "sensor/temp" };

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Metrics);
        Assert.True(result.Metrics.TopicsPerSecond > 0);
        Assert.True(result.OperationDuration.TotalMilliseconds >= 0);
        Assert.NotNull(result.OriginalCommand);
        Assert.Equal(command, result.OriginalCommand);
    }

    [Fact]
    public async Task DeleteTopicAsync_FailedValidation_HasNoMetrics()
    {
        // Arrange
        var service = CreateService();
        var command = new DeleteTopicCommand { TopicPattern = "" };

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(DeleteOperationStatus.Failed, result.Status);
        Assert.Null(result.Metrics);
    }

    [Fact]
    public async Task DeleteTopicAsync_SetsTimestamps()
    {
        // Arrange
        var service = CreateService();
        var before = DateTime.UtcNow;
        var command = new DeleteTopicCommand { TopicPattern = "sensor/temp" };

        // Act
        var result = await service.DeleteTopicAsync(command, CancellationToken.None);
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(result.StartTime >= before);
        Assert.NotNull(result.EndTime);
        Assert.True(result.EndTime >= result.StartTime);
        Assert.True(result.EndTime <= after);
    }

    #endregion

    private DeleteTopicService CreateService()
    {
        return new DeleteTopicService(_mockMqttService, _mockLogger);
    }

    private static MQTTnet.MqttApplicationMessage CreateRetainedMessage(string topic)
    {
        return new MQTTnet.MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload("test data")
            .WithRetainFlag(true)
            .Build();
    }

    private static MQTTnet.MqttApplicationMessage CreateNonRetainedMessage(string topic)
    {
        return new MQTTnet.MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload("test data")
            .WithRetainFlag(false)
            .Build();
    }
}