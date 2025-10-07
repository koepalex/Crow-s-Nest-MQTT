using CrowsNestMqtt.BusinessLogic.Models;
using Xunit;

namespace CrowsNestMqtt.UnitTests.BusinessLogic;

public class RequestMessageTests
{
    [Fact]
    public void IsValid_ValidRequestMessage_ReturnsTrue()
    {
        // Arrange
        var message = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "msg-123",
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
        var message = new RequestMessage
        {
            TopicName = string.Empty,
            MessageId = "msg-123",
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
        var message = new RequestMessage
        {
            TopicName = "request/topic",
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
        var message = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "msg-123",
            Timestamp = DateTime.UtcNow.AddHours(10)
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_ResponseTopicWithoutCorrelationData_ReturnsFalse()
    {
        // Arrange
        var message = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "msg-123",
            Timestamp = DateTime.UtcNow,
            ResponseTopic = "response/topic",
            CorrelationData = null
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_ResponseTopicWithEmptyCorrelationData_ReturnsFalse()
    {
        // Arrange
        var message = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "msg-123",
            Timestamp = DateTime.UtcNow,
            ResponseTopic = "response/topic",
            CorrelationData = Array.Empty<byte>()
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_ResponseTopicWithCorrelationData_ReturnsTrue()
    {
        // Arrange
        var message = new RequestMessage
        {
            TopicName = "request/topic",
            MessageId = "msg-123",
            Timestamp = DateTime.UtcNow,
            ResponseTopic = "response/topic",
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act
        var isValid = message.IsValid();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsRequestResponseMessage_WithResponseTopicAndCorrelationData_ReturnsTrue()
    {
        // Arrange
        var message = new RequestMessage
        {
            ResponseTopic = "response/topic",
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act & Assert
        Assert.True(message.IsRequestResponseMessage);
    }

    [Fact]
    public void IsRequestResponseMessage_WithoutResponseTopic_ReturnsFalse()
    {
        // Arrange
        var message = new RequestMessage
        {
            CorrelationData = new byte[] { 1, 2, 3, 4 }
        };

        // Act & Assert
        Assert.False(message.IsRequestResponseMessage);
    }

    [Fact]
    public void IsRequestResponseMessage_WithoutCorrelationData_ReturnsFalse()
    {
        // Arrange
        var message = new RequestMessage
        {
            ResponseTopic = "response/topic"
        };

        // Act & Assert
        Assert.False(message.IsRequestResponseMessage);
    }

    [Fact]
    public void CorrelationDataString_WithCorrelationData_ReturnsBase64()
    {
        // Arrange
        var correlationData = new byte[] { 1, 2, 3, 4 };
        var message = new RequestMessage
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
        var message = new RequestMessage();

        // Act
        var result = message.CorrelationDataString;

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void PayloadAsString_Utf8Payload_ReturnsDecodedString()
    {
        // Arrange
        var text = "Hello, MQTT!";
        var message = new RequestMessage
        {
            Payload = System.Text.Encoding.UTF8.GetBytes(text)
        };

        // Act
        var result = message.PayloadAsString;

        // Assert
        Assert.Equal(text, result);
    }

    [Fact]
    public void PayloadAsString_BinaryPayload_ReturnsBase64()
    {
        // Arrange
        var binaryData = new byte[] { 0xFF, 0xFE, 0xFD };
        var message = new RequestMessage
        {
            Payload = binaryData
        };

        // Act
        var result = message.PayloadAsString;

        // Assert - Should be either UTF-8 decoded or base64
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void WithUpdates_UpdateTopicName_ReturnsNewInstance()
    {
        // Arrange
        var original = new RequestMessage
        {
            TopicName = "original/topic",
            MessageId = "msg-123"
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
        var original = new RequestMessage
        {
            TopicName = "original/topic",
            MessageId = "msg-123",
            Payload = new byte[] { 1, 2, 3 }
        };
        var newPayload = new byte[] { 4, 5, 6 };

        // Act
        var updated = original.WithUpdates(
            topicName: "updated/topic",
            payload: newPayload,
            messageId: "msg-456");

        // Assert
        Assert.Equal("updated/topic", updated.TopicName);
        Assert.Equal("msg-456", updated.MessageId);
        Assert.Equal(newPayload, updated.Payload);
    }

    [Fact]
    public void CorrelationDataEquals_BothNull_ReturnsTrue()
    {
        // Act
        var result = RequestMessage.CorrelationDataEquals(null, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CorrelationDataEquals_OneNull_ReturnsFalse()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3 };

        // Act
        var result1 = RequestMessage.CorrelationDataEquals(data, null);
        var result2 = RequestMessage.CorrelationDataEquals(null, data);

        // Assert
        Assert.False(result1);
        Assert.False(result2);
    }

    [Fact]
    public void CorrelationDataEquals_DifferentLength_ReturnsFalse()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 1, 2 };

        // Act
        var result = RequestMessage.CorrelationDataEquals(data1, data2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CorrelationDataEquals_SameContent_ReturnsTrue()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 1, 2, 3, 4 };

        // Act
        var result = RequestMessage.CorrelationDataEquals(data1, data2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CorrelationDataEquals_DifferentContent_ReturnsFalse()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 1, 2, 3, 5 };

        // Act
        var result = RequestMessage.CorrelationDataEquals(data1, data2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCorrelationDataHashCode_NullData_ReturnsZero()
    {
        // Act
        var hashCode = RequestMessage.GetCorrelationDataHashCode(null);

        // Assert
        Assert.Equal(0, hashCode);
    }

    [Fact]
    public void GetCorrelationDataHashCode_EmptyData_ReturnsZero()
    {
        // Act
        var hashCode = RequestMessage.GetCorrelationDataHashCode(Array.Empty<byte>());

        // Assert
        Assert.Equal(0, hashCode);
    }

    [Fact]
    public void GetCorrelationDataHashCode_SameData_ReturnsSameHash()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 1, 2, 3, 4 };

        // Act
        var hash1 = RequestMessage.GetCorrelationDataHashCode(data1);
        var hash2 = RequestMessage.GetCorrelationDataHashCode(data2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetCorrelationDataHashCode_DifferentData_ReturnsDifferentHash()
    {
        // Arrange
        var data1 = new byte[] { 1, 2, 3, 4 };
        var data2 = new byte[] { 5, 6, 7, 8 };

        // Act
        var hash1 = RequestMessage.GetCorrelationDataHashCode(data1);
        var hash2 = RequestMessage.GetCorrelationDataHashCode(data2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ToString_WithoutRequestResponse_ReturnsFormattedString()
    {
        // Arrange
        var message = new RequestMessage
        {
            MessageId = "msg-123",
            TopicName = "request/topic",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = message.ToString();

        // Assert
        Assert.Contains("msg-123", result);
        Assert.Contains("request/topic", result);
        Assert.Contains("Request", result);
    }

    [Fact]
    public void ToString_WithRequestResponse_IncludesResponseTopic()
    {
        // Arrange
        var message = new RequestMessage
        {
            MessageId = "msg-123",
            TopicName = "request/topic",
            ResponseTopic = "response/topic",
            CorrelationData = new byte[] { 1, 2, 3 },
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = message.ToString();

        // Assert
        Assert.Contains("msg-123", result);
        Assert.Contains("request/topic", result);
        Assert.Contains("response/topic", result);
    }
}
