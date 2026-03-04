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