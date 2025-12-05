/// <summary>
/// Integration tests for message navigation with j/k keys.
/// Tests MessageNavigationState with wrap-around behavior.
/// Validates FR-013, FR-014, FR-015, FR-016.
/// </summary>
namespace CrowsNestMQTT.Tests.Integration.Navigation;

using System;
using System.Collections.Generic;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;
using MQTTnet;

public class MessageNavigationTests
{
    [Fact]
    public void MoveDown_AdvancesToNextMessage()
    {
        // Arrange - FR-013: Navigate down messages with 'j'
        var messageNav = CreateMessageNavigationWithMessages(3);
        Assert.Equal(0, messageNav.SelectedIndex); // Start at first

        // Act - Simulate 'j' key press
        messageNav.MoveDown();

        // Assert
        Assert.Equal(1, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveDown_AtLastMessage_WrapsToFirst()
    {
        // Arrange - FR-015: Wrap-around when reaching end
        var messageNav = CreateMessageNavigationWithMessages(3);

        // Move to last message
        messageNav.MoveDown(); // 0 -> 1
        messageNav.MoveDown(); // 1 -> 2 (last)
        Assert.Equal(2, messageNav.SelectedIndex);

        // Act - Navigate from last message
        messageNav.MoveDown();

        // Assert - Should wrap to first
        Assert.Equal(0, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveUp_MovesToPreviousMessage()
    {
        // Arrange - FR-014: Navigate up messages with 'k'
        var messageNav = CreateMessageNavigationWithMessages(3);
        messageNav.MoveDown(); // Move to second message
        Assert.Equal(1, messageNav.SelectedIndex);

        // Act - Simulate 'k' key press
        messageNav.MoveUp();

        // Assert
        Assert.Equal(0, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveUp_AtFirstMessage_WrapsToLast()
    {
        // Arrange - FR-016: Wrap-around when reaching start
        var messageNav = CreateMessageNavigationWithMessages(3);
        Assert.Equal(0, messageNav.SelectedIndex); // Start at first

        // Act - Navigate up from first message
        messageNav.MoveUp();

        // Assert - Should wrap to last
        Assert.Equal(2, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveDown_WithNoMessages_IsNoOp()
    {
        // Arrange
        var messageNav = new MessageNavigationState();
        Assert.False(messageNav.HasMessages);
        Assert.Equal(-1, messageNav.SelectedIndex);

        // Act
        messageNav.MoveDown();

        // Assert - Should remain at -1, no exception
        Assert.Equal(-1, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveUp_WithNoMessages_IsNoOp()
    {
        // Arrange
        var messageNav = new MessageNavigationState();
        Assert.False(messageNav.HasMessages);

        // Act
        messageNav.MoveUp();

        // Assert - Should remain at -1, no exception
        Assert.Equal(-1, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveDown_WithSingleMessage_WrapsToSameMessage()
    {
        // Arrange
        var messageNav = CreateMessageNavigationWithMessages(1);
        Assert.Equal(0, messageNav.SelectedIndex);

        // Act
        messageNav.MoveDown();

        // Assert - Wraps back to same (only) message
        Assert.Equal(0, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveUp_WithSingleMessage_WrapsToSameMessage()
    {
        // Arrange
        var messageNav = CreateMessageNavigationWithMessages(1);
        Assert.Equal(0, messageNav.SelectedIndex);

        // Act
        messageNav.MoveUp();

        // Assert - Wraps back to same (only) message
        Assert.Equal(0, messageNav.SelectedIndex);
    }

    [Fact]
    public void NavigateMessageDown_WithKeyboardNavigationService_UpdatesSelection()
    {
        // Arrange - Integration with KeyboardNavigationService
        var searchService = new TopicSearchService(() => new List<TopicReference>());
        var messageNav = CreateMessageNavigationWithMessages(3);
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false // Shortcuts not suppressed
        );

        Assert.Equal(0, messageNav.SelectedIndex);

        // Act - Simulate 'j' key press via service
        keyboardNav.NavigateMessageDown();

        // Assert
        Assert.Equal(1, messageNav.SelectedIndex);
    }

    [Fact]
    public void NavigateMessageUp_WithKeyboardNavigationService_UpdatesSelection()
    {
        // Arrange
        var searchService = new TopicSearchService(() => new List<TopicReference>());
        var messageNav = CreateMessageNavigationWithMessages(3);
        var keyboardNav = new KeyboardNavigationService(
            searchService,
            messageNav,
            () => false
        );

        Assert.Equal(0, messageNav.SelectedIndex);

        // Act - Simulate 'k' key press via service
        keyboardNav.NavigateMessageUp();

        // Assert - Should wrap to last
        Assert.Equal(2, messageNav.SelectedIndex);
    }

    [Fact]
    public void MoveDown_FiresPropertyChangedForSelectedIndex()
    {
        // Arrange
        var messageNav = CreateMessageNavigationWithMessages(3);
        bool propertyChanged = false;

        messageNav.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(MessageNavigationState.SelectedIndex))
            {
                propertyChanged = true;
            }
        };

        // Act
        messageNav.MoveDown();

        // Assert
        Assert.True(propertyChanged, "PropertyChanged should be raised for SelectedIndex");
    }

    [Fact]
    public void UpdateMessages_ResetsToFirstMessage()
    {
        // Arrange
        var messageNav = CreateMessageNavigationWithMessages(5);
        messageNav.MoveDown();
        messageNav.MoveDown(); // At index 2

        var newMessages = CreateMessages(3);

        // Act - Update to new message list
        messageNav.UpdateMessages(newMessages);

        // Assert - Should reset to first message
        Assert.Equal(0, messageNav.SelectedIndex);
        Assert.Equal(3, messageNav.Messages.Count);
    }

    [Fact]
    public void UpdateMessages_ToEmptyList_SetsIndexToNegativeOne()
    {
        // Arrange
        var messageNav = CreateMessageNavigationWithMessages(3);
        Assert.Equal(0, messageNav.SelectedIndex);

        // Act - Update to empty list
        messageNav.UpdateMessages(Array.Empty<MqttApplicationMessage>());

        // Assert
        Assert.Equal(-1, messageNav.SelectedIndex);
        Assert.False(messageNav.HasMessages);
    }

    [Fact]
    public void GetSelectedMessage_ReturnsCorrectMessage()
    {
        // Arrange
        var messages = CreateMessages(3);
        var messageNav = new MessageNavigationState(messages);

        // Act & Assert - First message
        var firstMessage = messageNav.GetSelectedMessage();
        Assert.NotNull(firstMessage);
        Assert.Same(messages[0], firstMessage);

        // Navigate and verify second message
        messageNav.MoveDown();
        var secondMessage = messageNav.GetSelectedMessage();
        Assert.NotNull(secondMessage);
        Assert.Same(messages[1], secondMessage);
    }

    [Fact]
    public void GetSelectedMessage_WithNoMessages_ReturnsNull()
    {
        // Arrange
        var messageNav = new MessageNavigationState();

        // Act
        var selectedMessage = messageNav.GetSelectedMessage();

        // Assert
        Assert.Null(selectedMessage);
    }

    [Fact]
    public void MixedUpDownNavigation_MaintainsCorrectPosition()
    {
        // Arrange
        var messageNav = CreateMessageNavigationWithMessages(5);
        Assert.Equal(0, messageNav.SelectedIndex);

        // Act - Mix up and down navigation
        messageNav.MoveDown();  // 0 -> 1
        messageNav.MoveDown();  // 1 -> 2
        messageNav.MoveUp();    // 2 -> 1
        messageNav.MoveDown();  // 1 -> 2
        messageNav.MoveDown();  // 2 -> 3

        // Assert
        Assert.Equal(3, messageNav.SelectedIndex);
    }

    [Fact]
    public void RapidSequentialDownNavigation_AllProcessed()
    {
        // Arrange - Performance test
        var messageNav = CreateMessageNavigationWithMessages(10);

        // Act - Simulate rapid 'j' key presses (20 times)
        for (int i = 0; i < 20; i++)
        {
            messageNav.MoveDown();
        }

        // Assert - 20 steps down from 0, wraps twice (20 % 10 = 0)
        Assert.Equal(0, messageNav.SelectedIndex);
    }

    /// <summary>
    /// Creates a MessageNavigationState with specified number of messages.
    /// </summary>
    private static MessageNavigationState CreateMessageNavigationWithMessages(int messageCount)
    {
        var messages = CreateMessages(messageCount);
        return new MessageNavigationState(messages);
    }

    /// <summary>
    /// Creates a list of test MQTT messages.
    /// </summary>
    private static IReadOnlyList<MqttApplicationMessage> CreateMessages(int count)
    {
        var messages = new List<MqttApplicationMessage>();

        for (int i = 0; i < count; i++)
        {
            messages.Add(new MqttApplicationMessageBuilder()
                .WithTopic($"test/topic/{i}")
                .WithPayload($"Message {i}")
                .Build());
        }

        return messages;
    }
}
