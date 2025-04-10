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

            // Act
            buffer.AddMessage(message);

            // Assert
            Assert.Equal(1, buffer.Count);
            Assert.Equal(50, buffer.CurrentSizeInBytes);
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

            // Act
            buffer.AddMessage(message1);
            buffer.AddMessage(message2);

            // Assert
            Assert.Equal(2, buffer.Count);
            Assert.Equal(70, buffer.CurrentSizeInBytes);
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

            // Act
            buffer.AddMessage(message1); // Current size: 60
            buffer.AddMessage(message2); // Current size: 90
            buffer.AddMessage(message3); // Adding 50 bytes. Need to remove 40 bytes. Removes message1 (60 bytes).

            // Assert
            Assert.Equal(2, buffer.Count); // message1 should be removed
            Assert.Equal(80, buffer.CurrentSizeInBytes); // 30 (msg2) + 50 (msg3)
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

            // Act
            buffer.AddMessage(message1); // Size: 40
            buffer.AddMessage(message2); // Size: 80
            buffer.AddMessage(message3); // Size: 120 -> Removes message1 (40). Size becomes 80.
            buffer.AddMessage(message4); // Adding 70 bytes. Need to remove 50 bytes. Removes message2 (40). Size becomes 40+70=110. Removes message3 (40). Size becomes 70.

            // Assert
            Assert.Equal(1, buffer.Count); // Only message4 should remain
            Assert.Equal(70, buffer.CurrentSizeInBytes); // Only message4 size
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

            // Act
            buffer.AddMessage(message1); // Add something first
            buffer.AddMessage(largeMessage); // Attempt to add the large message

            // Assert
            Assert.Equal(0, buffer.Count); // Buffer should be cleared
            Assert.Equal(0, buffer.CurrentSizeInBytes); // Size should be zero
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

            // Act
            buffer.AddMessage(message1); // Add something first (Size: 50)
            buffer.AddMessage(exactSizeMessage); // Attempt to add the exact size message. Needs 100, has 50. Removes message1 (50). Adds exactSizeMessage.

            // Assert
            Assert.Equal(1, buffer.Count); // Only the exact size message should be present
            Assert.Equal(100, buffer.CurrentSizeInBytes); // Size should be 100
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

            // Act
            buffer.AddMessage(message1);
            buffer.AddMessage(message2);
            buffer.AddMessage(message3);
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
            buffer.AddMessage(CreateTestMessage("test/topic", 30));
            buffer.AddMessage(CreateTestMessage("test/topic", 40));

            // Act
            buffer.Clear();

            // Assert
            Assert.Equal(0, buffer.Count);
            Assert.Equal(0, buffer.CurrentSizeInBytes);
            Assert.Empty(buffer.GetMessages());
        }
    }
}