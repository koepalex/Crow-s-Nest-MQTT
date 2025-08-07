using Xunit;
using CrowsNestMqtt.BusinessLogic;
using MQTTnet;
using System;

namespace CrowsNestMqtt.UnitTests.BusinessLogic
{
    /// <summary>
    /// Tests for the IdentifiedMqttApplicationMessageReceivedEventArgs class
    /// </summary>
    public class IdentifiedMqttApplicationMessageReceivedEventArgsTests
    {
        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = "test/topic",
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };
            const string clientId = "testClient";

            // Act
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId);

            // Assert
            Assert.Equal(messageId, eventArgs.MessageId);
            Assert.Equal("test/topic", eventArgs.Topic);
            Assert.Equal(applicationMessage, eventArgs.ApplicationMessage);
            Assert.Equal(clientId, eventArgs.ClientId);
            Assert.False(eventArgs.ProcessingFailed);
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_Constructor_WithNullApplicationMessage_ThrowsArgumentNullException()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            const string clientId = "testClient";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, null!, clientId));
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_Constructor_WithNullTopic_ThrowsArgumentNullException()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = null!, // Null topic
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };
            const string clientId = "testClient";

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId));
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_ProcessingFailed_CanBeSet()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = "test/topic",
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };
            const string clientId = "testClient";
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId);

            // Act
            eventArgs.ProcessingFailed = true;

            // Assert
            Assert.True(eventArgs.ProcessingFailed);
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_IsEventArgs()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = "test/topic",
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };
            const string clientId = "testClient";

            // Act
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId);

            // Assert
            Assert.IsAssignableFrom<EventArgs>(eventArgs);
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_WithEmptyGuid_SetsGuidCorrectly()
        {
            // Arrange
            var messageId = Guid.Empty;
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = "test/topic",
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };
            const string clientId = "testClient";

            // Act
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId);

            // Assert
            Assert.Equal(Guid.Empty, eventArgs.MessageId);
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_WithEmptyClientId_SetsClientIdCorrectly()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = "test/topic",
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };
            const string clientId = "";

            // Act
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId);

            // Assert
            Assert.Equal("", eventArgs.ClientId);
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_WithNullClientId_SetsClientIdCorrectly()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = "test/topic",
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("test payload"))
            };

            // Act
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, null!);

            // Assert
            Assert.Null(eventArgs.ClientId);
        }

        [Fact]
        public void IdentifiedMqttApplicationMessageReceivedEventArgs_Topic_ExtractedFromApplicationMessage()
        {
            // Arrange
            var messageId = Guid.NewGuid();
            const string expectedTopic = "sensors/temperature/room1";
            var applicationMessage = new MqttApplicationMessage
            {
                Topic = expectedTopic,
                PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes("25.5"))
            };
            const string clientId = "sensor_client";

            // Act
            var eventArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(messageId, applicationMessage, clientId);

            // Assert
            Assert.Equal(expectedTopic, eventArgs.Topic);
        }
    }
}
