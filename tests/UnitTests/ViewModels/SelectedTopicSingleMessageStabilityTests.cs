using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Reflection;
using MQTTnet;
using Xunit;
using NSubstitute;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    /// <summary>
    /// Verifies that when a topic is already selected, arrival of a single message
    /// (image, octet-stream, trailing-slash topic forms) remains visible and does not disappear (no flash).
    /// Targets regression covered by normalization + post-AddRange fallback selection logic.
    /// </summary>
    public class SelectedTopicSingleMessageStabilityTests
    {
        private readonly ICommandParserService _commandParser = Substitute.For<ICommandParserService>();
        private readonly IMqttService _mqttService = Substitute.For<IMqttService>();

        private MainViewModel CreateVm()
        {
            // Immediate scheduler makes reactive pipeline synchronous for determinism
            return new MainViewModel(_commandParser, _mqttService, uiScheduler: ImmediateScheduler.Instance);
        }

        private void InvokePrivateBatch(MainViewModel vm, List<IdentifiedMqttApplicationMessageReceivedEventArgs> batch)
        {
            // Use reflection to invoke the private ProcessMessageBatchOnUIThread(List<Identified...>)
            var mi = typeof(MainViewModel)
                .GetMethod("ProcessMessageBatchOnUIThread", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);
            mi!.Invoke(vm, new object[] { batch });
        }

        private IdentifiedMqttApplicationMessageReceivedEventArgs BuildEvent(string topic, byte[] payload, string? contentType = null)
        {
            var builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload);
            if (!string.IsNullOrEmpty(contentType))
            {
                builder = builder.WithContentType(contentType);
            }
            var msg = builder.Build();
            return new IdentifiedMqttApplicationMessageReceivedEventArgs(Guid.NewGuid(), msg, "test-client");
        }

        [Fact]
        public void TopicSelected_BeforeSingleImageMessageArrival_MessageVisible()
        {
            using var vm = CreateVm();
            var topic = "test/imageTopic";

            // Select node before message arrives
            var node = new NodeViewModel("imageTopic", null) { FullPath = topic };
            vm.SelectedNode = node;

            var ev = BuildEvent(topic, new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png");
            InvokePrivateBatch(vm, new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { ev });

            Assert.Single(vm.FilteredMessageHistory);
            Assert.NotNull(vm.SelectedMessage);
            Assert.Equal(topic, vm.SelectedMessage.Topic);
        }

        [Fact]
        public void TopicSelected_BeforeSingleOctetStreamMessageArrival_MessageVisible()
        {
            using var vm = CreateVm();
            var topic = "test/binaryTopic";

            var node = new NodeViewModel("binaryTopic", null) { FullPath = topic };
            vm.SelectedNode = node;

            var payload = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
            var ev = BuildEvent(topic, payload, "application/octet-stream");
            InvokePrivateBatch(vm, new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { ev });

            Assert.Single(vm.FilteredMessageHistory);
            Assert.NotNull(vm.SelectedMessage);
            Assert.Equal(topic, vm.SelectedMessage.Topic);
        }

        [Fact]
        public void TopicSelectedWithTrailingSlash_SingleMessageArrival_MessageVisible()
        {
            using var vm = CreateVm();
            var topic = "devices/sensor1";
            // Simulate tree node having trailing slash (normalization fix should handle)
            var node = new NodeViewModel("sensor1", new NodeViewModel("devices", null))
            {
                FullPath = topic + "/"
            };
            vm.SelectedNode = node;

            var ev = BuildEvent(topic, Encoding.UTF8.GetBytes("42.0"));
            InvokePrivateBatch(vm, new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { ev });

            Assert.Single(vm.FilteredMessageHistory);
            Assert.NotNull(vm.SelectedMessage);
            Assert.Equal(topic, vm.SelectedMessage.Topic);
        }

        [Fact]
        public void MessageRemainsVisible_AfterFilterReevaluation()
        {
            using var vm = CreateVm();
            var topic = "status/one";
            vm.SelectedNode = new NodeViewModel("one", new NodeViewModel("status", null)) { FullPath = topic };

            var ev = BuildEvent(topic, Encoding.UTF8.GetBytes("OK"));
            InvokePrivateBatch(vm, new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { ev });

            Assert.Single(vm.FilteredMessageHistory);

            // Trigger predicate rebuild by touching search term
            vm.CurrentSearchTerm = "OK";
            vm.CurrentSearchTerm = string.Empty;

            Assert.Single(vm.FilteredMessageHistory);
            Assert.NotNull(vm.SelectedMessage);
        }
    }
}
