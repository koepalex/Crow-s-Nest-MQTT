using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.Services;
using CrowsNestMqtt.UI.ViewModels;
using MQTTnet;
using NSubstitute;
using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class MessageViewModelFactoryTests
{
    private readonly IMqttService _mockMqttService;
    private readonly IMessageCorrelationService _mockCorrelationService;
    private readonly IStatusBarService _mockStatusBarService;

    public MessageViewModelFactoryTests()
    {
        _mockMqttService = Substitute.For<IMqttService>();
        _mockCorrelationService = Substitute.For<IMessageCorrelationService>();
        _mockStatusBarService = Substitute.For<IStatusBarService>();
    }

    [Fact]
    public void Constructor_WithoutCorrelationService_Succeeds()
    {
        // Act
        var factory = new MessageViewModelFactory();

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void Constructor_WithCorrelationService_Succeeds()
    {
        // Act
        var factory = new MessageViewModelFactory(_mockCorrelationService);

        // Assert
        Assert.NotNull(factory);
    }

    [Fact]
    public void CreateMessageViewModel_WithValidMessage_ReturnsViewModel()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = Encoding.UTF8.GetBytes("Test payload");
        var message = CreateTestMessage("test/topic", payload);

        // Act
        var result = factory.CreateMessageViewModel(message, _mockMqttService, _mockStatusBarService);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(message.MessageId, result.MessageId);
        Assert.Equal("test/topic", result.Topic);
        Assert.Equal("Test payload", result.PayloadPreview);
        Assert.Equal(12, result.Size);
    }

    [Fact]
    public void CreateMessageViewModel_WithNullMessage_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new MessageViewModelFactory();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.CreateMessageViewModel(null!, _mockMqttService, _mockStatusBarService));
    }

    [Fact]
    public void CreateMessageViewModel_WithNullMqttService_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var message = CreateTestMessage("test/topic", Encoding.UTF8.GetBytes("test"));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.CreateMessageViewModel(message, null!, _mockStatusBarService));
    }

    [Fact]
    public void CreateMessageViewModel_WithNullStatusBarService_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var message = CreateTestMessage("test/topic", Encoding.UTF8.GetBytes("test"));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            factory.CreateMessageViewModel(message, _mockMqttService, null!));
    }

    [Fact]
    public void GeneratePayloadPreview_WithEmptyPayload_ReturnsNoPayloadMessage()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = Array.Empty<byte>();

        // Act
        var result = factory.GeneratePayloadPreview(payload);

        // Assert
        Assert.Equal("[No Payload]", result);
    }

    [Fact]
    public void GeneratePayloadPreview_WithNullPayload_ReturnsNoPayloadMessage()
    {
        // Arrange
        var factory = new MessageViewModelFactory();

        // Act
        var result = factory.GeneratePayloadPreview(null!);

        // Assert
        Assert.Equal("[No Payload]", result);
    }

    [Fact]
    public void GeneratePayloadPreview_WithShortUtf8Text_ReturnsFullText()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        var result = factory.GeneratePayloadPreview(payload);

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void GeneratePayloadPreview_WithLongUtf8Text_TruncatesAndAddsEllipsis()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var longText = new string('A', 150);
        var payload = Encoding.UTF8.GetBytes(longText);

        // Act
        var result = factory.GeneratePayloadPreview(payload, maxLength: 100);

        // Assert
        Assert.EndsWith("...", result);
        Assert.Equal(103, result.Length); // 100 chars + "..."
        Assert.StartsWith(new string('A', 100), result);
    }

    [Fact]
    public void GeneratePayloadPreview_WithBinaryData_ReturnsBinaryDataMessage()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = new byte[] { 0xFF, 0xFE, 0xFD, 0x00, 0x01 }; // Invalid UTF-8

        // Act
        var result = factory.GeneratePayloadPreview(payload);

        // Assert
        Assert.Equal("[Binary Data: 5 bytes]", result);
    }

    [Fact]
    public void GeneratePayloadPreview_WithCustomMaxLength_RespectsLimit()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = Encoding.UTF8.GetBytes("This is a test message that is longer than 20 characters");

        // Act
        var result = factory.GeneratePayloadPreview(payload, maxLength: 20);

        // Assert
        Assert.Equal("This is a test messa...", result);
        Assert.Equal(23, result.Length); // 20 + "..."
    }

    [Fact]
    public async Task RegisterCorrelationAsync_WithNullCorrelationService_ReturnsFalse()
    {
        // Arrange
        var factory = new MessageViewModelFactory(null);
        var payload = Encoding.UTF8.GetBytes("test");
        var message = new MqttApplicationMessage
        {
            Topic = "test/topic",
            Payload = new ReadOnlySequence<byte>(payload)
        };

        // Act
        var result = await factory.RegisterCorrelationAsync(Guid.NewGuid(), message, "test/topic");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RegisterCorrelationAsync_WithNullMessage_ReturnsFalse()
    {
        // Arrange
        var factory = new MessageViewModelFactory(_mockCorrelationService);

        // Act
        var result = await factory.RegisterCorrelationAsync(Guid.NewGuid(), null!, "test/topic");

        // Assert
        Assert.False(result);
    }

    // NOTE: RegisterCorrelationAsync tests omitted due to complexity of MQTTnet internal types
    // These are integration-tested through MainViewModel tests instead

    [Fact]
    public void CreateMessageViewModel_WithNewlines_RemovesNewlinesFromPreview()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = Encoding.UTF8.GetBytes("Line 1\nLine 2\r\nLine 3");
        var message = CreateTestMessage("test/topic", payload);

        // Act
        var result = factory.CreateMessageViewModel(message, _mockMqttService, _mockStatusBarService);

        // Assert
        Assert.DoesNotContain("\n", result.PayloadPreview);
        Assert.DoesNotContain("\r", result.PayloadPreview);
    }

    [Fact]
    public void CreateMessageViewModel_WithRetainedMessage_SetsRetainedFlag()
    {
        // Arrange
        var factory = new MessageViewModelFactory();
        var payload = Encoding.UTF8.GetBytes("Retained message");
        var message = CreateTestMessage("test/topic", payload, isRetained: true);

        // Act
        var result = factory.CreateMessageViewModel(message, _mockMqttService, _mockStatusBarService);

        // Assert
        Assert.True(result.IsEffectivelyRetained);
    }

    // Helper method to create test messages
    private IdentifiedMqttApplicationMessageReceivedEventArgs CreateTestMessage(
        string topic,
        byte[] payload,
        bool isRetained = false)
    {
        var messageId = Guid.NewGuid();
        var mqttMessage = new MqttApplicationMessage
        {
            Topic = topic,
            Payload = new System.Buffers.ReadOnlySequence<byte>(payload),
            Retain = isRetained
        };

        var identifiedArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(
            messageId,
            mqttMessage,
            "test-client");

        // Set the IsEffectivelyRetained property using reflection since it's internal
        var property = typeof(IdentifiedMqttApplicationMessageReceivedEventArgs).GetProperty("IsEffectivelyRetained");
        property?.SetValue(identifiedArgs, isRetained);

        return identifiedArgs;
    }
}
