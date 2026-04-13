using System;
using Xunit;
using MQTTnet;
using NSubstitute;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.UI.Services;

namespace UnitTests.ViewModels
{
    public class MessageExpiryTests
    {
        private readonly IMqttService _mqttServiceMock;
        private readonly IStatusBarService _statusBarServiceMock;

        public MessageExpiryTests()
        {
            _mqttServiceMock = Substitute.For<IMqttService>();
            _statusBarServiceMock = Substitute.For<IStatusBarService>();
        }

        [Fact]
        public void IsExpired_WhenExpiryIntervalIsZero_ShouldReturnFalse()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 0);

            Assert.False(vm.IsExpired);
            Assert.False(vm.HasExpiry);
        }

        [Fact]
        public void IsExpired_WhenMessageIsWithinExpiryWindow_ShouldReturnFalse()
        {
            // Message created now with 3600s (1 hour) expiry - should not be expired
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 3600);

            Assert.True(vm.HasExpiry);
            Assert.False(vm.IsExpired);
        }

        [Fact]
        public void IsExpired_WhenMessageHasExpired_ShouldReturnTrue()
        {
            // Message created 10 seconds ago with 5s expiry - should be expired
            var pastTimestamp = DateTime.Now.AddSeconds(-10);
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", pastTimestamp, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 5);

            Assert.True(vm.HasExpiry);
            Assert.True(vm.IsExpired);
        }

        [Fact]
        public void TimeRemaining_WhenNoExpiry_ShouldReturnNull()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 0);

            Assert.Null(vm.TimeRemaining);
        }

        [Fact]
        public void TimeRemaining_WhenExpired_ShouldReturnZero()
        {
            var pastTimestamp = DateTime.Now.AddSeconds(-10);
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", pastTimestamp, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 5);

            Assert.Equal(TimeSpan.Zero, vm.TimeRemaining);
        }

        [Fact]
        public void TimeRemaining_WhenNotExpired_ShouldReturnPositiveValue()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 3600);

            var remaining = vm.TimeRemaining;
            Assert.NotNull(remaining);
            Assert.True(remaining.Value > TimeSpan.Zero);
            Assert.True(remaining.Value <= TimeSpan.FromSeconds(3600));
        }

        [Fact]
        public void RefreshExpiry_WhenMessageExpires_ShouldUpdateIsExpired()
        {
            // Message created 4 seconds ago with 2s expiry - already expired
            var pastTimestamp = DateTime.Now.AddSeconds(-4);
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", pastTimestamp, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 2);

            vm.RefreshExpiry();

            Assert.True(vm.IsExpired);
        }

        [Fact]
        public void RefreshExpiry_WhenNoExpiry_ShouldNotChangeState()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 0);

            vm.RefreshExpiry();

            Assert.False(vm.IsExpired);
        }

        [Fact]
        public void DisplayText_ShouldWorkCorrectlyRegardlessOfExpiry()
        {
            var timestamp = DateTime.Now;
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", timestamp, "test payload", 12,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 5);

            var expected = $"{timestamp:HH:mm:ss.fff} ({12,10} B): test payload";
            Assert.Equal(expected, vm.DisplayText);
        }

        [Fact]
        public void HasExpiry_WhenIntervalGreaterThanZero_ShouldReturnTrue()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 1);

            Assert.True(vm.HasExpiry);
        }

        [Fact]
        public void MessageExpiryInterval_ShouldBeStoredCorrectly()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock,
                messageExpiryInterval: 42);

            Assert.Equal(42u, vm.MessageExpiryInterval);
        }

        [Fact]
        public void Constructor_DefaultExpiryInterval_ShouldBeZero()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(), "test/topic", DateTime.Now, "payload", 7,
                _mqttServiceMock, _statusBarServiceMock);

            Assert.Equal(0u, vm.MessageExpiryInterval);
            Assert.False(vm.HasExpiry);
            Assert.False(vm.IsExpired);
        }
    }
}
