using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class CorrelationEntryTests
{
    [Fact]
    public void Constructor_ValidParameters_CreatesInstance()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");

        // Act
        var entry = new CorrelationEntry(key, correlation);

        // Assert
        Assert.Equal(key, entry.Key);
        Assert.Equal(correlation, entry.Correlation);
        Assert.Equal(ResponseStatus.Pending, entry.Status);
        Assert.Empty(entry.ResponseMessageIds);
    }

    [Fact]
    public void Constructor_NullCorrelation_ThrowsArgumentNullException()
    {
        // Arrange
        var key = new CorrelationKey(new byte[] { 1, 2, 3, 4 });

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CorrelationEntry(key, null!));
    }

    [Fact]
    public void Constructor_CorrelationWithResponses_InitializesResponseIds()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        correlation.LinkResponse("resp-1");
        correlation.LinkResponse("resp-2");

        // Act
        var entry = new CorrelationEntry(key, correlation);

        // Assert
        Assert.Equal(2, entry.ResponseMessageIds.Count);
        Assert.Contains("resp-1", entry.ResponseMessageIds);
        Assert.Contains("resp-2", entry.ResponseMessageIds);
        Assert.Equal(ResponseStatus.Received, entry.Status);
    }

    [Fact]
    public void Constructor_ExpiredCorrelation_SetsNavigationDisabledStatus()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation
        {
            CorrelationData = correlationData,
            RequestMessageId = "req-123",
            ResponseTopic = "response/topic",
            RequestTimestamp = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        var entry = new CorrelationEntry(key, correlation);

        // Assert
        Assert.Equal(ResponseStatus.NavigationDisabled, entry.Status);
    }

    [Fact]
    public void AddResponse_ValidResponseId_AddsToEntry()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Act
        var result = entry.AddResponse("resp-1");

        // Assert
        Assert.True(result);
        Assert.Single(entry.ResponseMessageIds);
        Assert.Contains("resp-1", entry.ResponseMessageIds);
        Assert.Equal(ResponseStatus.Received, entry.Status);
    }

    [Fact]
    public void AddResponse_DuplicateResponseId_ReturnsFalse()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");

        // Act
        var result = entry.AddResponse("resp-1");

        // Assert
        Assert.False(result);
        Assert.Single(entry.ResponseMessageIds);
    }

    [Fact]
    public void AddResponse_NullResponseId_ReturnsFalse()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Act
        var result = entry.AddResponse(null!);

        // Assert
        Assert.False(result);
        Assert.Empty(entry.ResponseMessageIds);
    }

    [Fact]
    public void AddResponse_EmptyResponseId_ReturnsFalse()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Act
        var result = entry.AddResponse(string.Empty);

        // Assert
        Assert.False(result);
        Assert.Empty(entry.ResponseMessageIds);
    }

    [Fact]
    public void AddResponse_FirstResponse_UpdatesStatusToPending()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        Assert.Equal(ResponseStatus.Pending, entry.Status);

        // Act
        entry.AddResponse("resp-1");

        // Assert
        Assert.Equal(ResponseStatus.Received, entry.Status);
    }

    [Fact]
    public void AddResponse_UpdatesLastAccessedAt()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        var initialAccessTime = entry.LastAccessedAt;

        // Act
        System.Threading.Thread.Sleep(10);
        entry.AddResponse("resp-1");

        // Assert
        Assert.True(entry.LastAccessedAt > initialAccessTime);
    }

    [Fact]
    public void RefreshStatus_UpdatesLastAccessedAt()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        var initialAccessTime = entry.LastAccessedAt;

        // Act
        System.Threading.Thread.Sleep(10);
        entry.RefreshStatus();

        // Assert
        Assert.True(entry.LastAccessedAt > initialAccessTime);
    }

    [Fact]
    public void RefreshStatus_ExpiredCorrelation_SetsNavigationDisabled()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Expire the correlation
        correlation.MarkExpired();

        // Act
        entry.RefreshStatus();

        // Assert
        Assert.Equal(ResponseStatus.NavigationDisabled, entry.Status);
    }

    [Fact]
    public void RefreshStatus_WithResponses_SetsReceivedStatus()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");
        entry.Status = ResponseStatus.Pending; // Reset status

        // Act
        entry.RefreshStatus();

        // Assert
        Assert.Equal(ResponseStatus.Received, entry.Status);
    }

    [Fact]
    public void ShouldCleanup_ExpiredCorrelation_ReturnsTrue()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation
        {
            CorrelationData = correlationData,
            RequestMessageId = "req-123",
            ResponseTopic = "response/topic",
            RequestTimestamp = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        var entry = new CorrelationEntry(key, correlation);

        // Act & Assert
        Assert.True(entry.ShouldCleanup);
    }

    [Fact]
    public void ShouldCleanup_ActiveCorrelation_ReturnsFalse()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Act & Assert
        Assert.False(entry.ShouldCleanup);
    }

    [Fact]
    public void HasResponses_NoResponses_ReturnsFalse()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Act & Assert
        Assert.False(entry.HasResponses);
    }

    [Fact]
    public void HasResponses_WithResponses_ReturnsTrue()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");

        // Act & Assert
        Assert.True(entry.HasResponses);
    }

    [Fact]
    public void EstimatedMemoryUsage_ReturnsPositiveValue()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");

        // Act
        var memoryUsage = entry.EstimatedMemoryUsage;

        // Assert
        Assert.True(memoryUsage > 0);
    }

    [Fact]
    public void EstimatedMemoryUsage_IncludesResponseIds()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry1 = new CorrelationEntry(key, correlation);
        var entry2 = new CorrelationEntry(key, correlation);
        entry2.AddResponse("resp-1");
        entry2.AddResponse("resp-2");
        entry2.AddResponse("resp-3");

        // Act
        var usage1 = entry1.EstimatedMemoryUsage;
        var usage2 = entry2.EstimatedMemoryUsage;

        // Assert
        Assert.True(usage2 > usage1);
    }

    [Fact]
    public void WithUpdates_UpdatesCorrelation_ReturnsNewInstance()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation1 = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation1);
        var correlation2 = new MessageCorrelation(correlationData, "req-456", "response/topic2");

        // Act
        var updated = entry.WithUpdates(correlation: correlation2);

        // Assert
        Assert.Equal(correlation2, updated.Correlation);
        Assert.Equal(entry.Key, updated.Key);
    }

    [Fact]
    public void WithUpdates_UpdatesStatus_ReturnsNewInstance()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);

        // Act
        var updated = entry.WithUpdates(status: ResponseStatus.NavigationDisabled);

        // Assert
        Assert.Equal(ResponseStatus.NavigationDisabled, updated.Status);
    }

    [Fact]
    public void WithUpdates_CopiesResponseIds()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");
        entry.AddResponse("resp-2");

        // Act
        var updated = entry.WithUpdates();

        // Assert
        Assert.Equal(2, updated.ResponseMessageIds.Count);
        Assert.Contains("resp-1", updated.ResponseMessageIds);
        Assert.Contains("resp-2", updated.ResponseMessageIds);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");

        // Act
        var result = entry.ToString();

        // Assert
        Assert.Contains("Entry", result);
        Assert.Contains("Received", result);
        Assert.Contains("1 responses", result);
    }

    [Fact]
    public void ToString_MultipleResponses_ShowsCount()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation(correlationData, "req-123", "response/topic");
        var entry = new CorrelationEntry(key, correlation);
        entry.AddResponse("resp-1");
        entry.AddResponse("resp-2");
        entry.AddResponse("resp-3");

        // Act
        var result = entry.ToString();

        // Assert
        Assert.Contains("3 responses", result);
    }

    [Fact]
    public void ToString_ExpiredEntry_ShowsExpired()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var key = new CorrelationKey(correlationData);
        var correlation = new MessageCorrelation
        {
            CorrelationData = correlationData,
            RequestMessageId = "req-123",
            ResponseTopic = "response/topic",
            RequestTimestamp = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };
        var entry = new CorrelationEntry(key, correlation);

        // Act
        var result = entry.ToString();

        // Assert
        Assert.Contains("expired", result);
    }
}
