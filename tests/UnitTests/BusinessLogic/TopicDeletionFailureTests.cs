using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class TopicDeletionFailureTests
{
    [Fact]
    public void Constructor_ValidProperties_CreatesInstance()
    {
        // Arrange
        var topicName = "sensor/temperature";
        var errorType = DeletionErrorType.NetworkError;
        var errorMessage = "Connection timeout";

        // Act
        var failure = new TopicDeletionFailure
        {
            TopicName = topicName,
            ErrorType = errorType,
            ErrorMessage = errorMessage
        };

        // Assert
        Assert.Equal(topicName, failure.TopicName);
        Assert.Equal(errorType, failure.ErrorType);
        Assert.Equal(errorMessage, failure.ErrorMessage);
        Assert.Null(failure.Exception);
        Assert.False(failure.IsRetryable);
    }

    [Fact]
    public void Constructor_WithException_StoresException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var failure = new TopicDeletionFailure
        {
            TopicName = "test/topic",
            ErrorType = DeletionErrorType.Unknown,
            ErrorMessage = "Error occurred",
            Exception = exception
        };

        // Assert
        Assert.Equal(exception, failure.Exception);
    }

    [Fact]
    public void Constructor_IsRetryable_CanBeSet()
    {
        // Act
        var failure = new TopicDeletionFailure
        {
            TopicName = "test/topic",
            ErrorType = DeletionErrorType.Timeout,
            ErrorMessage = "Timeout",
            IsRetryable = true
        };

        // Assert
        Assert.True(failure.IsRetryable);
    }

    [Fact]
    public void FailureTime_DefaultsToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var failure = new TopicDeletionFailure
        {
            TopicName = "test/topic",
            ErrorType = DeletionErrorType.NetworkError,
            ErrorMessage = "Error"
        };

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(failure.FailureTime >= beforeCreation);
        Assert.True(failure.FailureTime <= afterCreation);
    }

    [Fact]
    public void Record_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var failure1 = new TopicDeletionFailure
        {
            TopicName = "test/topic",
            ErrorType = DeletionErrorType.NetworkError,
            ErrorMessage = "Error"
        };

        var failure2 = new TopicDeletionFailure
        {
            TopicName = "test/topic",
            ErrorType = DeletionErrorType.NetworkError,
            ErrorMessage = "Error"
        };

        // Act & Assert
        Assert.NotEqual(failure1, failure2); // Different because of FailureTime
    }
}
