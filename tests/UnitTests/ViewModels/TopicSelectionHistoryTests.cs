using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Reactive.Concurrency;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.BusinessLogic;
using DynamicData;
using MQTTnet;
using NSubstitute;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    /// <summary>
    /// Verifies that selecting a topic node shows the message history for that topic (core functionality).
    /// Uses an injected deterministic scheduler (ImmediateScheduler) so the reactive pipeline is synchronous.
    /// </summary>
    public class TopicSelectionHistoryTests
    {
        private readonly ICommandParserService _commandParserService;

        public TopicSelectionHistoryTests()
        {
            _commandParserService = Substitute.For<ICommandParserService>();
        }

        [Fact]
        public void SelectingTopic_PopulatesFilteredMessageHistory_ForThatTopic()
        {
            // Arrange
            using var vm = new MainViewModel(_commandParserService, uiScheduler: ImmediateScheduler.Instance);

            // Add messages for two topics
            AddTestMessage(vm, "sensors/temp", "temperature: 21.5C");
            AddTestMessage(vm, "sensors/temp", "temperature: 21.6C");
            AddTestMessage(vm, "sensors/humidity", "humidity: 48%");

            // Create topic nodes (sensors/temp and sensors/humidity)
            InvokePrivate(vm, "UpdateOrCreateNode", "sensors/temp", true);
            InvokePrivate(vm, "UpdateOrCreateNode", "sensors/humidity", true);

            // Sanity: topics exist
            var sensorsNode = vm.TopicTreeNodes.FirstOrDefault(n => n.Name == "sensors");
            Assert.NotNull(sensorsNode);
            var tempNode = sensorsNode!.Children.FirstOrDefault(n => n.Name == "temp");
            var humidityNode = sensorsNode.Children.FirstOrDefault(n => n.Name == "humidity");
            Assert.NotNull(tempNode);
            Assert.NotNull(humidityNode);

            // Act: select temp node
            vm.SelectedNode = tempNode;

            // Assert
            Assert.Equal(2, vm.FilteredMessageHistory.Count);
            Assert.All(vm.FilteredMessageHistory, m => Assert.Equal("sensors/temp", m.Topic));
        }

        [Fact]
        public void SelectingParentTopic_IncludesSubTopics()
        {
            // Arrange
            using var vm = new MainViewModel(_commandParserService, uiScheduler: ImmediateScheduler.Instance);

            AddTestMessage(vm, "devices/door/front", "open");
            AddTestMessage(vm, "devices/door/back", "closed");
            AddTestMessage(vm, "devices/temp/living", "20.1");

            InvokePrivate(vm, "UpdateOrCreateNode", "devices/door/front", true);
            InvokePrivate(vm, "UpdateOrCreateNode", "devices/door/back", true);
            InvokePrivate(vm, "UpdateOrCreateNode", "devices/temp/living", true);

            var devicesNode = vm.TopicTreeNodes.FirstOrDefault(n => n.Name == "devices");
            Assert.NotNull(devicesNode);
            var doorNode = devicesNode!.Children.FirstOrDefault(n => n.Name == "door");
            Assert.NotNull(doorNode);

            // Act: select parent 'devices'
            vm.SelectedNode = devicesNode;

            // Assert: all 3 messages should appear
            Assert.Equal(3, vm.FilteredMessageHistory.Count);

            // Act: select 'door' node => only door sub-topics (2)
            vm.SelectedNode = doorNode;
            Assert.Equal(2, vm.FilteredMessageHistory.Count);
            Assert.All(vm.FilteredMessageHistory, m => Assert.StartsWith("devices/door/", m.Topic, StringComparison.OrdinalIgnoreCase));
        }

        #region Helpers

        private void AddTestMessage(MainViewModel vm, string topic, string payload)
        {
            var field = typeof(MainViewModel).GetField("_messageHistorySource", BindingFlags.NonPublic | BindingFlags.Instance);
            var source = field?.GetValue(vm) as SourceList<MessageViewModel>;
            Assert.NotNull(source);

            var messageId = Guid.NewGuid();
            var timestamp = DateTime.Now;

            // Build a minimal MqttApplicationMessage for full message caching if later requested
            var fullMsg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .Build();

            var messageVm = new MessageViewModel(
                messageId,
                topic,
                timestamp,
                payload,
                (int)fullMsg.Payload.Length,
                Substitute.For<IMqttService>(), // Not needed for these assertions
                vm, // IStatusBarService
                fullMsg
            );

            source!.Add(messageVm);
        }

        private void InvokePrivate(object target, string methodName, params object[] args)
        {
            var mi = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);
            mi!.Invoke(target, args);
        }

        #endregion
    }
}
