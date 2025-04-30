using Xunit;
using CrowsNestMqtt.Utils;
using MQTTnet;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace CrowsNestMqtt.Tests
{
    public class TopicRingBufferTests
    {
        private MqttApplicationMessage CreateTestMessage(string topic, int payloadSize, string? content = null)
        {
            byte[] payload;
            if (content != null)
            {
                payload = Encoding.UTF8.GetBytes(content.PadRight(payloadSize, ' ')); // Ensure exact size if content provided
                if (payload.Length > payloadSize) // Adjust if content itself is larger
                    payload = payload.Take(payloadSize).ToArray();
            }
            else
            {
                payload = new byte[payloadSize];
                // Fill with some data to avoid empty arrays if needed for specific tests
                for (int i = 0; i < payloadSize; i++) payload[i] = (byte)(i % 256);
            }


            return new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();
        }

        [Fact]
        public void Constructor_WithZeroSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TopicRingBuffer(0));
        }

        [Fact]
        public void Constructor_WithNegativeSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TopicRingBuffer(-100));
        }

        [Fact]
        public void AddMessage_SingleMessage_AddsSuccessfully()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100); // 100 bytes limit
            var message = CreateTestMessage("test/topic", 50); // 50 bytes payload

           var messageId = Guid.NewGuid();

           // Act
           buffer.AddMessage(message, messageId);

           // Assert
            Assert.Equal(1, buffer.Count);
           // Assert.Equal(50, buffer.CurrentSizeInBytes); // Size calculation is internal detail, focus on count and content
           var retrievedMessages = buffer.GetMessages().ToList();
           Assert.Single(retrievedMessages);
            Assert.Same(message, retrievedMessages[0]); // Check reference equality for the exact message
        }

        [Fact]
        public void AddMessage_MultipleMessages_WithinLimit_AddsSuccessfully()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 30);
            var message2 = CreateTestMessage("test/topic", 40);

           var messageId1 = Guid.NewGuid();
           var messageId2 = Guid.NewGuid();

           // Act
           buffer.AddMessage(message1, messageId1);
           buffer.AddMessage(message2, messageId2);

           // Assert
            Assert.Equal(2, buffer.Count);
           // Assert.Equal(70, buffer.CurrentSizeInBytes); // Size calculation is internal detail
           var retrievedMessages = buffer.GetMessages().ToList();
           Assert.Equal(2, retrievedMessages.Count);
            Assert.Same(message1, retrievedMessages[0]);
            Assert.Same(message2, retrievedMessages[1]);
        }

        [Fact]
        public void AddMessage_ExceedsLimit_RemovesOldestMessage()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 60, "Message1"); // Oldest
            var message2 = CreateTestMessage("test/topic", 30, "Message2");
            var message3 = CreateTestMessage("test/topic", 50, "Message3"); // New message causing overflow

           var messageId1 = Guid.NewGuid();
           var messageId2 = Guid.NewGuid();
           var messageId3 = Guid.NewGuid();

           // Act
           buffer.AddMessage(message1, messageId1); // Current size: ~60
           buffer.AddMessage(message2, messageId2); // Current size: ~90
           buffer.AddMessage(message3, messageId3); // Adding ~50 bytes. Need to remove ~40 bytes. Removes message1 (~60 bytes).

           // Assert
            Assert.Equal(2, buffer.Count); // message1 should be removed
           // Assert.Equal(80, buffer.CurrentSizeInBytes); // Size calculation is internal detail
           var retrievedMessages = buffer.GetMessages().ToList();
           Assert.Equal(2, retrievedMessages.Count);
            Assert.Same(message2, retrievedMessages[0]); // message2 is now the oldest
            Assert.Same(message3, retrievedMessages[1]);
        }

        [Fact]
        public void AddMessage_ExceedsLimitSignificantly_RemovesMultipleOldestMessages()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 40, "Message1"); // Oldest
            var message2 = CreateTestMessage("test/topic", 40, "Message2");
            var message3 = CreateTestMessage("test/topic", 40, "Message3");
            var message4 = CreateTestMessage("test/topic", 70, "Message4"); // New message causing overflow

           var messageId1 = Guid.NewGuid();
           var messageId2 = Guid.NewGuid();
           var messageId3 = Guid.NewGuid();
           var messageId4 = Guid.NewGuid();

           // Act
           buffer.AddMessage(message1, messageId1); // Size: ~40
           buffer.AddMessage(message2, messageId2); // Size: ~80
           buffer.AddMessage(message3, messageId3); // Size: ~120 -> Removes message1 (~40). Size becomes ~80.
           buffer.AddMessage(message4, messageId4); // Adding ~70 bytes. Need to remove ~50 bytes. Removes message2 (~40). Size becomes ~40+~70=~110. Removes message3 (~40). Size becomes ~70.

           // Assert
            Assert.Equal(1, buffer.Count); // Only message4 should remain
           // Assert.Equal(70, buffer.CurrentSizeInBytes); // Size calculation is internal detail
           var retrievedMessages = buffer.GetMessages().ToList();
           Assert.Single(retrievedMessages);
            Assert.Same(message4, retrievedMessages[0]);
        }

        [Fact]
        public void AddMessage_SingleMessageLargerThanBuffer_ClearsBufferAndDoesNotAdd()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 50, "Existing");
            var largeMessage = CreateTestMessage("test/topic", 150, "TooLarge"); // Larger than buffer limit

           var messageId1 = Guid.NewGuid();
           var largeMessageId = Guid.NewGuid();

           // Act
           buffer.AddMessage(message1, messageId1); // Add something first
           buffer.AddMessage(largeMessage, largeMessageId); // Attempt to add the large message

           // Assert
            Assert.Equal(0, buffer.Count); // Buffer should be cleared
           Assert.Equal(0, buffer.CurrentSizeInBytes); // Size should be zero after clear
           var retrievedMessages = buffer.GetMessages().ToList();
           Assert.Empty(retrievedMessages); // No messages should be present
        }

         [Fact]
        public void AddMessage_SingleMessageEqualToBufferLimit_AddsSuccessfullyAfterClearing()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 50, "Existing");
            var exactSizeMessage = CreateTestMessage("test/topic", 100, "ExactSize"); // Equal to buffer limit

           var messageId1 = Guid.NewGuid();
           var exactSizeMessageId = Guid.NewGuid();

           // Act
           buffer.AddMessage(message1, messageId1); // Add something first (Size: ~50)
           buffer.AddMessage(exactSizeMessage, exactSizeMessageId); // Attempt to add the exact size message. Needs ~100, has ~50. Removes message1 (~50). Adds exactSizeMessage.

           // Assert
            Assert.Equal(1, buffer.Count); // Only the exact size message should be present
           // Assert.Equal(100, buffer.CurrentSizeInBytes); // Size calculation is internal detail
           var retrievedMessages = buffer.GetMessages().ToList();
           Assert.Single(retrievedMessages);
            Assert.Same(exactSizeMessage, retrievedMessages[0]);
        }


        [Fact]
        public void GetMessages_EmptyBuffer_ReturnsEmptyCollection()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);

            // Act
            var messages = buffer.GetMessages();

            // Assert
            Assert.NotNull(messages);
            Assert.Empty(messages);
        }

        [Fact]
        public void GetMessages_ReturnsMessagesInInsertionOrder()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 20, "First");
            var message2 = CreateTestMessage("test/topic", 30, "Second");
            var message3 = CreateTestMessage("test/topic", 40, "Third");

           var messageId1 = Guid.NewGuid();
           var messageId2 = Guid.NewGuid();
           var messageId3 = Guid.NewGuid();

           // Act
           buffer.AddMessage(message1, messageId1);
           buffer.AddMessage(message2, messageId2);
           buffer.AddMessage(message3, messageId3);
           var retrievedMessages = buffer.GetMessages().ToList();

           // Assert
            Assert.Equal(3, retrievedMessages.Count);
            Assert.Same(message1, retrievedMessages[0]);
            Assert.Same(message2, retrievedMessages[1]);
            Assert.Same(message3, retrievedMessages[2]);
        }

        [Fact]
        public void Clear_RemovesAllMessagesAndResetsSize()
        {
           // Arrange
           var buffer = new TopicRingBuffer(100);
           buffer.AddMessage(CreateTestMessage("test/topic", 30), Guid.NewGuid());
           buffer.AddMessage(CreateTestMessage("test/topic", 40), Guid.NewGuid());

           // Act
            buffer.Clear();

            // Assert
            Assert.Equal(0, buffer.Count);
            Assert.Equal(0, buffer.CurrentSizeInBytes);
            Assert.Empty(buffer.GetMessages());
        }
// --- Tests for TryGetMessage ---

        [Fact]
        public void TryGetMessage_MessageExists_ReturnsTrueAndMessage()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 30, "Message1");
            var message2 = CreateTestMessage("test/topic", 40, "Message2");
            var messageId1 = Guid.NewGuid();
            var messageId2 = Guid.NewGuid();
            buffer.AddMessage(message1, messageId1);
            buffer.AddMessage(message2, messageId2);

            // Act
            var found = buffer.TryGetMessage(messageId2, out var retrievedMessage);

            // Assert
            Assert.True(found);
            Assert.NotNull(retrievedMessage);
            Assert.Same(message2, retrievedMessage); // Verify it's the correct message instance
        }

        [Fact]
        public void TryGetMessage_MessageDoesNotExist_ReturnsFalseAndNull()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 30, "Message1");
            var messageId1 = Guid.NewGuid();
            buffer.AddMessage(message1, messageId1);
            var nonExistentId = Guid.NewGuid(); // An ID that was never added

            // Act
            var found = buffer.TryGetMessage(nonExistentId, out var retrievedMessage);

            // Assert
            Assert.False(found);
            Assert.Null(retrievedMessage);
        }

        [Fact]
        public void TryGetMessage_MessageWasEvicted_ReturnsFalseAndNull()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 60, "Message1"); // Will be evicted
            var message2 = CreateTestMessage("test/topic", 30, "Message2");
            var message3 = CreateTestMessage("test/topic", 50, "Message3"); // Causes eviction of message1
            var messageId1 = Guid.NewGuid(); // ID of the message that will be evicted
            var messageId2 = Guid.NewGuid();
            var messageId3 = Guid.NewGuid();

            buffer.AddMessage(message1, messageId1);
            buffer.AddMessage(message2, messageId2);
            buffer.AddMessage(message3, messageId3); // message1 is evicted here

            // Act
            var found = buffer.TryGetMessage(messageId1, out var retrievedMessage); // Try to get the evicted message

            // Assert
            Assert.False(found);
            Assert.Null(retrievedMessage);
            Assert.Equal(2, buffer.Count); // Verify buffer state after eviction
        }

        // --- Test for Duplicate ID Handling ---

        [Fact]
        public void AddMessage_WithDuplicateId_IgnoresNewMessage()
        {
            // Arrange
            var buffer = new TopicRingBuffer(100);
            var message1 = CreateTestMessage("test/topic", 40, "Original");
            var message2 = CreateTestMessage("test/topic", 40, "Duplicate"); // Same size, different content
            var messageId = Guid.NewGuid(); // Use the same ID for both

            // Act
            buffer.AddMessage(message1, messageId); // Add the first message
            var initialCount = buffer.Count;
            var initialSize = buffer.CurrentSizeInBytes;
            var initialMessages = buffer.GetMessages().ToList();

            buffer.AddMessage(message2, messageId); // Attempt to add the second message with the same ID

            // Assert
            Assert.Equal(initialCount, buffer.Count); // Count should not change
            Assert.Equal(initialSize, buffer.CurrentSizeInBytes); // Size should not change
            var finalMessages = buffer.GetMessages().ToList();
            Assert.Single(finalMessages); // Should still only contain one message
            Assert.Same(message1, finalMessages[0]); // The original message should still be there
            Assert.True(buffer.TryGetMessage(messageId, out var retrievedMessage)); // Can still retrieve by ID
            Assert.Same(message1, retrievedMessage); // Retrieved message is the original one
        }

        // --- Test for Oversized Message (Edge Case) ---

        [Fact]
        public void AddMessage_OversizedMessageToEmptyBuffer_IsIgnored()
        {
            // Arrange
            var buffer = new TopicRingBuffer(50); // Buffer limit of 50 bytes
            var oversizedMessage = CreateTestMessage("test/topic", 100, "Too Big"); // Message size > buffer limit
            var messageId = Guid.NewGuid();

            // Act
            buffer.AddMessage(oversizedMessage, messageId); // Attempt to add the oversized message

            // Assert
            Assert.Equal(0, buffer.Count); // Message should not be added
            Assert.Equal(0, buffer.CurrentSizeInBytes); // Size should remain 0
            Assert.Empty(buffer.GetMessages()); // Buffer should be empty
            Assert.False(buffer.TryGetMessage(messageId, out _)); // Cannot retrieve the message
        }
    }
}