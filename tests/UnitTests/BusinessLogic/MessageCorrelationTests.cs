using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class MessageCorrelationTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var requestMessageId = "req-123";
        var responseTopic = "response/topic";

        // Act
        var correlation = new MessageCorrelation(correlationData, requestMessageId, responseTopic);

        // Assert
        Assert.Equal(correlationData, correlation.CorrelationData);
        Assert.Equal(requestMessageId, correlation.RequestMessageId);
        Assert.Equal(responseTopic, correlation.ResponseTopic);
        Assert.Equal(CorrelationStatus.Pending, correlation.Status);
        Assert.Empty(correlation.ResponseMessageIds);
    }

    [Fact]
    public void Constructor_CustomTtl_SetsCorrectExpiration()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var requestMessageId = "req-123";
        var responseTopic = "response/topic";
        var ttlMinutes = 60;
        var beforeCreation = DateTime.UtcNow;

        // Act
        var correlation = new MessageCorrelation(correlationData, requestMessageId, responseTopic, ttlMinutes);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(correlation.ExpiresAt >= beforeCreation.AddMinutes(ttlMinutes));
        Assert.True(correlation.ExpiresAt <= afterCreation.AddMinutes(ttlMinutes));
    }

    [Fact]
    public void Constructor_NullCorrelationData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageCorrelation(null!, "req-123", "response/topic"));
    }

    [Fact]
    public void Constructor_NullRequestMessageId_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageCorrelation(new byte[] { 1, 2, 3 }, null!, "response/topic"));
    }

    [Fact]
    public void Constructor_NullResponseTopic_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MessageCorrelation(new byte[] { 1, 2, 3 }, "req-123", null!));
    }

    [Fact]
    public void IsValid_ValidCorrelation_ReturnsTrue()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        var isValid = correlation.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_EmptyCorrelationData_ReturnsFalse()
    {
        // Arrange
        var correlation = new MessageCorrelation
        {
            CorrelationData = Array.Empty<byte>(),
            RequestMessageId = "req-123",
            ResponseTopic = "response/topic",
            RequestTimestamp = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // Act
        var isValid = correlation.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_EmptyRequestMessageId_ReturnsFalse()
    {
        // Arrange
        var correlation = new MessageCorrelation
        {
            CorrelationData = new byte[] { 1, 2, 3 },
            RequestMessageId = string.Empty,
            ResponseTopic = "response/topic",
            RequestTimestamp = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // Act
        var isValid = correlation.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_ResponseTopicWithWildcard_ReturnsFalse()
    {
        // Arrange
        var correlation = new MessageCorrelation
        {
            CorrelationData = new byte[] { 1, 2, 3 },
            RequestMessageId = "req-123",
            ResponseTopic = "response/#",
            RequestTimestamp = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // Act
        var isValid = correlation.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void LinkResponse_ValidResponseId_AddsToList()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");
        var responseId = "resp-456";

        // Act
        var result = correlation.LinkResponse(responseId);

        // Assert
        Assert.True(result);
        Assert.Single(correlation.ResponseMessageIds);
        Assert.Contains(responseId, correlation.ResponseMessageIds);
        Assert.Equal(CorrelationStatus.Responded, correlation.Status);
        Assert.NotNull(correlation.FirstResponseTimestamp);
        Assert.NotNull(correlation.LastResponseTimestamp);
    }

    [Fact]
    public void LinkResponse_MultipleResponses_AddsAll()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        var result1 = correlation.LinkResponse("resp-1");
        var result2 = correlation.LinkResponse("resp-2");
        var result3 = correlation.LinkResponse("resp-3");

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        Assert.Equal(3, correlation.ResponseMessageIds.Count);
        Assert.Equal(CorrelationStatus.Responded, correlation.Status);
    }

    [Fact]
    public void LinkResponse_DuplicateResponseId_ReturnsFalse()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");
        var responseId = "resp-456";
        correlation.LinkResponse(responseId);

        // Act
        var result = correlation.LinkResponse(responseId);

        // Assert
        Assert.False(result);
        Assert.Single(correlation.ResponseMessageIds);
    }

    [Fact]
    public void LinkResponse_NullOrEmptyResponseId_ThrowsArgumentException()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => correlation.LinkResponse(null!));
        Assert.Throws<ArgumentException>(() => correlation.LinkResponse(string.Empty));
    }

    [Fact]
    public void LinkResponse_FirstResponse_ExtendsTtl()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic",
            ttlMinutes: 5);
        var initialExpiration = correlation.ExpiresAt;

        // Act
        correlation.LinkResponse("resp-1");

        // Assert
        Assert.True(correlation.ExpiresAt > initialExpiration);
    }

    [Fact]
    public void IsExpired_BeforeExpiration_ReturnsFalse()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic",
            ttlMinutes: 30);

        // Act & Assert
        Assert.False(correlation.IsExpired);
    }

    [Fact]
    public void IsExpired_AfterExpiration_ReturnsTrue()
    {
        // Arrange
        var correlation = new MessageCorrelation
        {
            CorrelationData = new byte[] { 1, 2, 3 },
            RequestMessageId = "req-123",
            ResponseTopic = "response/topic",
            RequestTimestamp = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act & Assert
        Assert.True(correlation.IsExpired);
    }

    [Fact]
    public void HasResponses_NoResponses_ReturnsFalse()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act & Assert
        Assert.False(correlation.HasResponses);
    }

    [Fact]
    public void HasResponses_WithResponses_ReturnsTrue()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");
        correlation.LinkResponse("resp-1");

        // Act & Assert
        Assert.True(correlation.HasResponses);
    }

    [Fact]
    public void AverageResponseTime_NoResponse_ReturnsZero()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        var avgTime = correlation.AverageResponseTime;

        // Assert
        Assert.Equal(TimeSpan.Zero, avgTime);
    }

    [Fact]
    public void AverageResponseTime_WithResponse_ReturnsTimeDifference()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        System.Threading.Thread.Sleep(50); // Small delay
        correlation.LinkResponse("resp-1");

        // Assert
        Assert.True(correlation.AverageResponseTime > TimeSpan.Zero);
    }

    [Fact]
    public void MarkExpired_UpdatesStatusAndExpiration()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        correlation.MarkExpired();

        // Assert
        Assert.Equal(CorrelationStatus.Expired, correlation.Status);
        Assert.True(correlation.IsExpired);
    }

    [Fact]
    public void EstimatedMemoryUsage_ReturnsPositiveValue()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");
        correlation.LinkResponse("resp-1");

        // Act
        var memoryUsage = correlation.EstimatedMemoryUsage;

        // Assert
        Assert.True(memoryUsage > 0);
    }

    [Fact]
    public void CorrelationDataString_ReturnsBase64String()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var correlation = new MessageCorrelation(
            correlationData,
            "req-123",
            "response/topic");

        // Act
        var base64String = correlation.CorrelationDataString;

        // Assert
        Assert.Equal(Convert.ToBase64String(correlationData), base64String);
    }

    [Fact]
    public void WithUpdates_UpdatesStatus_ReturnsNewInstance()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        var updated = correlation.WithUpdates(status: CorrelationStatus.Expired);

        // Assert
        Assert.Equal(CorrelationStatus.Expired, updated.Status);
        Assert.Equal(correlation.CorrelationData, updated.CorrelationData);
        Assert.Equal(correlation.RequestMessageId, updated.RequestMessageId);
    }

    [Fact]
    public void Equals_SameCorrelationData_ReturnsTrue()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var correlation1 = new MessageCorrelation(correlationData, "req-1", "topic1");
        var correlation2 = new MessageCorrelation(correlationData, "req-2", "topic2");

        // Act & Assert
        Assert.Equal(correlation1, correlation2);
    }

    [Fact]
    public void Equals_DifferentCorrelationData_ReturnsFalse()
    {
        // Arrange
        var correlation1 = new MessageCorrelation(new byte[] { 1, 2, 3 }, "req-1", "topic1");
        var correlation2 = new MessageCorrelation(new byte[] { 4, 5, 6 }, "req-2", "topic2");

        // Act & Assert
        Assert.NotEqual(correlation1, correlation2);
    }

    [Fact]
    public void GetHashCode_SameCorrelationData_ReturnsSameHash()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var correlation1 = new MessageCorrelation(correlationData, "req-1", "topic1");
        var correlation2 = new MessageCorrelation(correlationData, "req-2", "topic2");

        // Act & Assert
        Assert.Equal(correlation1.GetHashCode(), correlation2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var correlation = new MessageCorrelation(
            new byte[] { 1, 2, 3, 4 },
            "req-123",
            "response/topic");

        // Act
        var result = correlation.ToString();

        // Assert
        Assert.Contains("req-123", result);
        Assert.Contains("response/topic", result);
        Assert.Contains("Pending", result);
    }
}
