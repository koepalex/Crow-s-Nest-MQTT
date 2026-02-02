using System;
using System.Collections.Generic;
using MQTTnet;
using Xunit;
using CrowsNestMQTT.BusinessLogic.Navigation;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Navigation
{
    public class MessageNavigationStateUnitTests
    {
        private static MqttApplicationMessage CreateMessage(string topic) =>
            new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .Build();

        [Fact]
        public void Constructor_Default_ShouldInitializeEmpty()
        {
            var state = new MessageNavigationState();
            Assert.Empty(state.Messages);
            Assert.Equal(-1, state.SelectedIndex);
            Assert.False(state.HasMessages);
        }

        [Fact]
        public void Constructor_WithMessages_ShouldInitialize()
        {
            var messages = new[] { CreateMessage("test") };
            var state = new MessageNavigationState(messages);
            Assert.Single(state.Messages);
            Assert.Equal(0, state.SelectedIndex);
            Assert.True(state.HasMessages);
        }

        [Fact]
        public void Constructor_WithNullMessages_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new MessageNavigationState(null!));
        }

        [Fact]
        public void Constructor_WithEmptyMessages_ShouldSetIndexToNegativeOne()
        {
            var state = new MessageNavigationState(Array.Empty<MqttApplicationMessage>());
            Assert.Equal(-1, state.SelectedIndex);
            Assert.False(state.HasMessages);
        }

        [Fact]
        public void SelectedIndex_SetValidIndex_ShouldUpdate()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2") };
            var state = new MessageNavigationState(messages);
            state.SelectedIndex = 1;
            Assert.Equal(1, state.SelectedIndex);
        }

        [Fact]
        public void SelectedIndex_SetOutOfRange_ShouldThrow()
        {
            var messages = new[] { CreateMessage("t1") };
            var state = new MessageNavigationState(messages);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectedIndex = 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectedIndex = -2);
        }

        [Fact]
        public void SelectedIndex_WithNoMessages_ShouldSetToNegativeOne()
        {
            var state = new MessageNavigationState();
            state.SelectedIndex = 5;
            Assert.Equal(-1, state.SelectedIndex);
        }

        [Fact]
        public void SelectedIndex_Change_ShouldRaisePropertyChanged()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2") };
            var state = new MessageNavigationState(messages);
            var eventRaised = false;
            state.PropertyChanged += (s, e) => eventRaised = true;
            state.SelectedIndex = 1;
            Assert.True(eventRaised);
        }

        [Fact]
        public void MoveDown_WithMessages_ShouldAdvanceIndex()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2"), CreateMessage("t3") };
            var state = new MessageNavigationState(messages);
            state.MoveDown();
            Assert.Equal(1, state.SelectedIndex);
            state.MoveDown();
            Assert.Equal(2, state.SelectedIndex);
        }

        [Fact]
        public void MoveDown_AtEnd_ShouldWrapToStart()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2") };
            var state = new MessageNavigationState(messages);
            state.SelectedIndex = 1;
            state.MoveDown();
            Assert.Equal(0, state.SelectedIndex);
        }

        [Fact]
        public void MoveDown_WithNoMessages_ShouldBeNoOp()
        {
            var state = new MessageNavigationState();
            state.MoveDown();
            Assert.Equal(-1, state.SelectedIndex);
        }

        [Fact]
        public void MoveUp_WithMessages_ShouldDecrementIndex()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2"), CreateMessage("t3") };
            var state = new MessageNavigationState(messages);
            state.SelectedIndex = 2;
            state.MoveUp();
            Assert.Equal(1, state.SelectedIndex);
            state.MoveUp();
            Assert.Equal(0, state.SelectedIndex);
        }

        [Fact]
        public void MoveUp_AtStart_ShouldWrapToEnd()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2") };
            var state = new MessageNavigationState(messages);
            state.MoveUp();
            Assert.Equal(1, state.SelectedIndex);
        }

        [Fact]
        public void MoveUp_WithNoMessages_ShouldBeNoOp()
        {
            var state = new MessageNavigationState();
            state.MoveUp();
            Assert.Equal(-1, state.SelectedIndex);
        }

        [Fact]
        public void GetSelectedMessage_WithValidIndex_ShouldReturnMessage()
        {
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2") };
            var state = new MessageNavigationState(messages);
            state.SelectedIndex = 1;
            var msg = state.GetSelectedMessage();
            Assert.NotNull(msg);
            Assert.Equal("t2", msg.Topic);
        }

        [Fact]
        public void GetSelectedMessage_WithNoMessages_ShouldReturnNull()
        {
            var state = new MessageNavigationState();
            var msg = state.GetSelectedMessage();
            Assert.Null(msg);
        }

        [Fact]
        public void UpdateMessages_WithNewMessages_ShouldUpdateAndResetIndex()
        {
            var state = new MessageNavigationState();
            var messages = new[] { CreateMessage("t1"), CreateMessage("t2") };
            state.UpdateMessages(messages);
            Assert.Equal(2, state.Messages.Count);
            Assert.Equal(0, state.SelectedIndex);
        }

        [Fact]
        public void UpdateMessages_WithEmptyList_ShouldSetIndexToNegativeOne()
        {
            var messages = new[] { CreateMessage("t1") };
            var state = new MessageNavigationState(messages);
            state.UpdateMessages(Array.Empty<MqttApplicationMessage>());
            Assert.Equal(-1, state.SelectedIndex);
            Assert.False(state.HasMessages);
        }

        [Fact]
        public void UpdateMessages_WithNullMessages_ShouldThrow()
        {
            var state = new MessageNavigationState();
            Assert.Throws<ArgumentNullException>(() => state.UpdateMessages(null!));
        }

        [Fact]
        public void UpdateMessages_ShouldRaisePropertyChanged()
        {
            var state = new MessageNavigationState();
            var eventCount = 0;
            state.PropertyChanged += (s, e) => eventCount++;
            var messages = new[] { CreateMessage("t1") };
            state.UpdateMessages(messages);
            Assert.True(eventCount > 0);
        }

        [Fact]
        public void HasMessages_WithMessages_ShouldReturnTrue()
        {
            var messages = new[] { CreateMessage("t1") };
            var state = new MessageNavigationState(messages);
            Assert.True(state.HasMessages);
        }

        [Fact]
        public void HasMessages_WithoutMessages_ShouldReturnFalse()
        {
            var state = new MessageNavigationState();
            Assert.False(state.HasMessages);
        }
    }
}
