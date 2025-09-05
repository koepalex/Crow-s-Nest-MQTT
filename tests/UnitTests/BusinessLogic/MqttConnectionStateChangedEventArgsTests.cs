using Xunit;
using CrowsNestMqtt.BusinessLogic;
using System;

namespace CrowsNestMqtt.UnitTests.BusinessLogic
{
    /// <summary>
    /// Tests for the MqttConnectionStateChangedEventArgs class
    /// </summary>
    public class MqttConnectionStateChangedEventArgsTests
    {
        [Fact]
        public void MqttConnectionStateChangedEventArgs_Constructor_WithAllParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            const bool isConnected = true;
            var error = new Exception("Test error");
            const ConnectionStatusState status = ConnectionStatusState.Connected;
            const string reconnectInfo = "Reconnect attempt 1";

            // Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(isConnected, error, status, reconnectInfo);

            // Assert
            Assert.Equal(isConnected, eventArgs.IsConnected);
            Assert.Equal(error, eventArgs.Error);
            Assert.Equal(status, eventArgs.ConnectionStatus);
            Assert.Equal(reconnectInfo, eventArgs.ReconnectInfo);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_Constructor_WithMinimalParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            const bool isConnected = false;
            const ConnectionStatusState status = ConnectionStatusState.Disconnected;

            // Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(isConnected, null, status);

            // Assert
            Assert.Equal(isConnected, eventArgs.IsConnected);
            Assert.Null(eventArgs.Error);
            Assert.Equal(status, eventArgs.ConnectionStatus);
            Assert.Null(eventArgs.ReconnectInfo);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_Constructor_WithErrorButConnected_SetsPropertiesCorrectly()
        {
            // Arrange
            const bool isConnected = true;
            var error = new InvalidOperationException("Connection warning");
            const ConnectionStatusState status = ConnectionStatusState.Connected;

            // Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(isConnected, error, status);

            // Assert
            Assert.True(eventArgs.IsConnected);
            Assert.Equal(error, eventArgs.Error);
            Assert.Equal(status, eventArgs.ConnectionStatus);
            Assert.Null(eventArgs.ReconnectInfo);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_Constructor_WithConnectingStatus_SetsPropertiesCorrectly()
        {
            // Arrange
            const bool isConnected = false;
            const ConnectionStatusState status = ConnectionStatusState.Connecting;
            const string reconnectInfo = "Attempting connection";

            // Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(isConnected, null, status, reconnectInfo);

            // Assert
            Assert.False(eventArgs.IsConnected);
            Assert.Null(eventArgs.Error);
            Assert.Equal(status, eventArgs.ConnectionStatus);
            Assert.Equal(reconnectInfo, eventArgs.ReconnectInfo);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_Constructor_WithEmptyReconnectInfo_SetsPropertiesCorrectly()
        {
            // Arrange
            const bool isConnected = false;
            const ConnectionStatusState status = ConnectionStatusState.Disconnected;
            const string reconnectInfo = "";

            // Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(isConnected, null, status, reconnectInfo);

            // Assert
            Assert.False(eventArgs.IsConnected);
            Assert.Null(eventArgs.Error);
            Assert.Equal(status, eventArgs.ConnectionStatus);
            Assert.Equal(reconnectInfo, eventArgs.ReconnectInfo);
        }

        [Fact]
        public void MqttConnectionStateChangedEventArgs_IsEventArgs()
        {
            // Arrange & Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(false, null, ConnectionStatusState.Disconnected);

            // Assert
            Assert.IsAssignableFrom<EventArgs>(eventArgs);
        }

        [Theory]
        [InlineData(ConnectionStatusState.Connected)]
        [InlineData(ConnectionStatusState.Disconnected)]
        [InlineData(ConnectionStatusState.Connecting)]
        public void MqttConnectionStateChangedEventArgs_AllConnectionStates_CanBeSet(ConnectionStatusState status)
        {
            // Act
            var eventArgs = new MqttConnectionStateChangedEventArgs(true, null, status);

            // Assert
            Assert.Equal(status, eventArgs.ConnectionStatus);
        }
    }
}
