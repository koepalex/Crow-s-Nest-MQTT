using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class ResponseMessageTests
{
    [Fact]
    public void IsValid_ValidResponseMessage_ReturnsTrue()
    {
        // Arrange
        var message = new ResponseMessage
        {
            TopicName = "response/topic",
            MessageId = "resp-123",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_EmptyTopicName_ReturnsFalse()
    {
        // Arrange
        var message = new ResponseMessage
        {
            TopicName = string.Empty,
            MessageId = "resp-123",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_EmptyMessageId_ReturnsFalse()
    {
        // Arrange
        var message = new ResponseMessage
        {
            TopicName = "response/topic",
            MessageId = string.Empty,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_FutureTimestamp_ReturnsFalse()
    {
        // Arrange
        var message = new ResponseMessage
        {
            TopicName = "response/topic",
            MessageId = "resp-123",
            Timestamp = DateTime.UtcNow.AddHours(10)
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void HasCorrelationData_WithCorrelationData_ReturnsTrue()
    {
        // Arrange
        var message = new ResponseMessage
        {
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act & Assert
        Assert.True(message.HasCorrelationData);
    }

    [Fact]
    public void HasCorrelationData_WithoutCorrelationData_ReturnsFalse()
    {
        // Arrange
        var message = new ResponseMessage();

        // Act & Assert
        Assert.False(message.HasCorrelationData);
    }

    [Fact]
    public void HasCorrelationData_WithEmptyCorrelationData_ReturnsFalse()
    {
        // Arrange
        var message = new ResponseMessage
        {
            CorrelationData = Array.Empty<byte>()
        };

        // Act & Assert
        Assert.False(message.HasCorrelationData);
    }

    [Fact]
    public void CorrelationDataString_WithCorrelationData_ReturnsBase64()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var message = new ResponseMessage
        {
            CorrelationData = correlationData
        };

        // Act
        var result = message.CorrelationDataString;

        // Assert
        Assert.Equal(Convert.ToBase64String(correlationData), result);
    }

    [Fact]
    public void CorrelationDataString_WithoutCorrelationData_ReturnsEmpty()
    {
        // Arrange
        var message = new ResponseMessage();

        // Act
        var result = message.CorrelationDataString;

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void PayloadAsString_Utf8Payload_ReturnsDecodedString()
    {
        // Arrange
        var text = "Response payload";
        var message = new ResponseMessage
        {
            Payload = System.Text.Encoding.UTF8.GetBytes(text)
        };

        // Act
        var result = message.PayloadAsString;

        // Assert
        Assert.Equal(text, result);
    }

    [Fact]
    public void CorrelatesWith_RequestMessage_MatchingCorrelationData_ReturnsTrue()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var request = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "req-123",
            ResponseTopic = "response/topic",
            CorrelationData = correlationData
        };
        var response = new ResponseMessage
        {
            CorrelationData = correlationData
        };

        // Act
        var result = response.CorrelatesWith(request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CorrelatesWith_RequestMessage_DifferentCorrelationData_ReturnsFalse()
    {
        // Arrange
        var request = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "req-123",
            ResponseTopic = "response/topic",
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };
        var response = new ResponseMessage
        {
            CorrelationData = new byte[] { 5, 6, 7, 8 }
        };

        // Act
        var result = response.CorrelatesWith(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CorrelatesWith_RequestMessage_NullRequest_ReturnsFalse()
    {
        // Arrange
        var response = new ResponseMessage
        {
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var result = response.CorrelatesWith((RequestMessage)null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CorrelatesWith_RequestMessage_ResponseWithoutCorrelationData_ReturnsFalse()
    {
        // Arrange
        var request = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "req-123",
            ResponseTopic = "response/topic",
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };
        var response = new ResponseMessage();

        // Act
        var result = response.CorrelatesWith(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CorrelatesWith_ByCorrelationData_Matching_ReturnsTrue()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var response = new ResponseMessage
        {
            CorrelationData = correlationData
        };

        // Act
        var result = response.CorrelatesWith(correlationData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CorrelatesWith_ByCorrelationData_Different_ReturnsFalse()
    {
        // Arrange
        var response = new ResponseMessage
        {
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var result = response.CorrelatesWith(new byte[] { 5, 6, 7, 8 });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanLinkToRequest_ValidLink_ReturnsTrue()
    {
        // Arrange
        var requestTimestamp = DateTime.UtcNow.AddSeconds(-10);
        var responseTopic = "response/topic";
        var response = new ResponseMessage
        {
            TopicName = responseTopic,
            Timestamp = DateTime.UtcNow,
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var result = response.CanLinkToRequest(requestTimestamp, responseTopic);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanLinkToRequest_DifferentTopic_ReturnsFalse()
    {
        // Arrange
        var requestTimestamp = DateTime.UtcNow.AddSeconds(-10);
        var response = new ResponseMessage
        {
            TopicName = "response/topic",
            Timestamp = DateTime.UtcNow,
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var result = response.CanLinkToRequest(requestTimestamp, "different/topic");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanLinkToRequest_ResponseBeforeRequest_ReturnsFalse()
    {
        // Arrange
        var requestTimestamp = DateTime.UtcNow;
        var responseTopic = "response/topic";
        var response = new ResponseMessage
        {
            TopicName = responseTopic,
            Timestamp = DateTime.UtcNow.AddSeconds(-10),
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var result = response.CanLinkToRequest(requestTimestamp, responseTopic);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanLinkToRequest_NoCorrelationData_ReturnsFalse()
    {
        // Arrange
        var requestTimestamp = DateTime.UtcNow.AddSeconds(-10);
        var responseTopic = "response/topic";
        var response = new ResponseMessage
        {
            TopicName = responseTopic,
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = response.CanLinkToRequest(requestTimestamp, responseTopic);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void WithUpdates_UpdateTopicName_ReturnsNewInstance()
    {
        // Arrange
        var original = new ResponseMessage
        {
            TopicName = "original/topic",
            MessageId = "resp-123"
        };

        // Act
        var updated = original.WithUpdates(topicName: "updated/topic");

        // Assert
        Assert.Equal("updated/topic", updated.TopicName);
        Assert.Equal(original.MessageId, updated.MessageId);
    }

    [Fact]
    public void WithUpdates_UpdateMultipleFields_ReturnsNewInstance()
    {
        // Arrange
        var original = new ResponseMessage
        {
            TopicName = "original/topic",
            MessageId = "resp-123",
            Payload = new byte[] { 1, 2, 3 }
        };
        var newPayload = new byte[] { 4, 5, 6 };
        var newCorrelationData = new byte[] { 7, 8, 9 };

        // Act
        var updated = original.WithUpdates(
            topicName: "updated/topic",
            payload: newPayload,
            correlationData: newCorrelationData,
            messageId: "resp-456");

        // Assert
        Assert.Equal("updated/topic", updated.TopicName);
        Assert.Equal("resp-456", updated.MessageId);
        Assert.Equal(newPayload, updated.Payload);
        Assert.Equal(newCorrelationData, updated.CorrelationData);
    }

    [Fact]
    public void EstimatedMemoryUsage_ReturnsPositiveValue()
    {
        // Arrange
        var message = new ResponseMessage
        {
            TopicName = "response/topic",
            MessageId = "resp-123",
            Payload = new byte[] { 1, 2, 3, 4, 5 },
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var memoryUsage = message.EstimatedMemoryUsage;

        // Assert
        Assert.True(memoryUsage > 0);
    }

    [Fact]
    public void EstimatedMemoryUsage_IncludesUserProperties()
    {
        // Arrange
        var message1 = new ResponseMessage
        {
            TopicName = "response/topic",
            MessageId = "resp-123"
        };
        var message2 = new ResponseMessage
        {
            TopicName = "response/topic",
            MessageId = "resp-123",
            UserProperties = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            }
        };

        // Act
        var usage1 = message1.EstimatedMemoryUsage;
        var usage2 = message2.EstimatedMemoryUsage;

        // Assert
        Assert.True(usage2 > usage1);
    }

    [Fact]
    public void Equals_SameMessageId_ReturnsTrue()
    {
        // Arrange
        var message1 = new ResponseMessage { MessageId = "resp-123" };
        var message2 = new ResponseMessage { MessageId = "resp-123" };

        // Act & Assert
        Assert.Equal(message1, message2);
    }

    [Fact]
    public void Equals_DifferentMessageId_ReturnsFalse()
    {
        // Arrange
        var message1 = new ResponseMessage { MessageId = "resp-123" };
        var message2 = new ResponseMessage { MessageId = "resp-456" };

        // Act & Assert
        Assert.NotEqual(message1, message2);
    }

    [Fact]
    public void Equals_WithNonResponseMessage_ReturnsFalse()
    {
        // Arrange
        var message = new ResponseMessage { MessageId = "resp-123" };
        var other = new object();

        // Act & Assert
        Assert.False(message.Equals(other));
    }

    [Fact]
    public void GetHashCode_SameMessageId_ReturnsSameHash()
    {
        // Arrange
        var message1 = new ResponseMessage { MessageId = "resp-123" };
        var message2 = new ResponseMessage { MessageId = "resp-123" };

        // Act & Assert
        Assert.Equal(message1.GetHashCode(), message2.GetHashCode());
    }

    [Fact]
    public void ToString_WithoutCorrelationData_ReturnsFormattedString()
    {
        // Arrange
        var message = new ResponseMessage
        {
            MessageId = "resp-123",
            TopicName = "response/topic",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = message.ToString();

        // Assert
        Assert.Contains("resp-123", result);
        Assert.Contains("response/topic", result);
        Assert.Contains("Response", result);
    }

    [Fact]
    public void ToString_WithCorrelationData_IncludesCorrelationInfo()
    {
        // Arrange
        var message = new ResponseMessage
        {
            MessageId = "resp-123",
            TopicName = "response/topic",
            CorrelationData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = message.ToString();

        // Assert
        Assert.Contains("resp-123", result);
        Assert.Contains("response/topic", result);
    }
}
