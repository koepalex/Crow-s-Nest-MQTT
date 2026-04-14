using System;
using Xunit;
using NSubstitute;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.UI.Services;
using MQTTnet;

namespace UnitTests.ViewModels
{
    public class OwnMessageTests
    {
        private readonly IMqttService _mqttService = Substitute.For<IMqttService>();
        private readonly IStatusBarService _statusBarService = Substitute.For<IStatusBarService>();

        private MessageViewModel CreateViewModel(bool isOwnMessage = false, string payloadPreview = "hello payload", int size = 13)
        {
            return new MessageViewModel(
                Guid.NewGuid(),
                "test/topic",
                DateTime.UtcNow,
                payloadPreview,
                size,
                _mqttService,
                _statusBarService,
                isOwnMessage: isOwnMessage);
        }

        [Fact]
        public void IsOwnMessage_DefaultsFalse_WhenNotSpecified()
        {
            var vm = new MessageViewModel(
                Guid.NewGuid(),
                "test/topic",
                DateTime.UtcNow,
                "payload",
                7,
                _mqttService,
                _statusBarService);

            Assert.False(vm.IsOwnMessage);
        }

        [Fact]
        public void IsOwnMessage_ReturnsTrue_WhenPassedTrue()
        {
            var vm = CreateViewModel(isOwnMessage: true);

            Assert.True(vm.IsOwnMessage);
        }

        [Fact]
        public void IsOwnMessage_ReturnsFalse_WhenPassedFalse()
        {
            var vm = CreateViewModel(isOwnMessage: false);

            Assert.False(vm.IsOwnMessage);
        }

        [Fact]
        public void DisplayText_ContainsUpArrowPrefix_WhenIsOwnMessage()
        {
            var vm = CreateViewModel(isOwnMessage: true);

            Assert.StartsWith("↑ ", vm.DisplayText);
        }

        [Fact]
        public void DisplayText_DoesNotContainUpArrowPrefix_WhenNotOwnMessage()
        {
            var vm = CreateViewModel(isOwnMessage: false);

            Assert.DoesNotContain("↑", vm.DisplayText);
        }

        [Fact]
        public void DisplayText_ContainsPayloadPreview_WhenIsOwnMessage()
        {
            var vm = CreateViewModel(isOwnMessage: true, payloadPreview: "test-payload");

            Assert.Contains("test-payload", vm.DisplayText);
        }

        [Fact]
        public void DisplayText_ContainsTimestampAndSize_WhenIsOwnMessage()
        {
            var vm = CreateViewModel(isOwnMessage: true, size: 42);

            Assert.Contains("42", vm.DisplayText);
            Assert.Contains("B)", vm.DisplayText);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void DisplayText_ContainsPayloadPreview_RegardlessOfIsOwnMessage(bool isOwnMessage)
        {
            var vm = CreateViewModel(isOwnMessage: isOwnMessage, payloadPreview: "my-data");

            Assert.Contains("my-data", vm.DisplayText);
        }

        [Fact]
        public void IdentifiedEventArgs_IsOwnMessage_DefaultsFalse()
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("test/topic")
                .WithPayload("payload"u8.ToArray())
                .Build();

            var args = new IdentifiedMqttApplicationMessageReceivedEventArgs(
                Guid.NewGuid(), msg, "client-1");

            Assert.False(args.IsOwnMessage);
        }
    }
}
