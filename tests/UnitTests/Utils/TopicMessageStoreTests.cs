using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrowsNestMqtt.Utils;
using MQTTnet;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Utils
{
    public class TopicMessageStoreTests
    {
        private static MqttApplicationMessage CreateMessage(string topic, int payloadSize, string? marker = null)
        {
            var payloadText = (marker ?? "X").PadRight(payloadSize, 'X');
            var payload = Encoding.UTF8.GetBytes(payloadText);
            return new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .Build();
        }

        [Fact]
        public void AddBatch_EvictsOnlyWithinSameTopic()
        {
            // Arrange: small limits per topic (in bytes) to trigger eviction
            var limits = new Dictionary<string, long>
            {
                { "a", 70 }, // Enough for two 30B messages + overhead, eviction on 3rd
                { "b", 70 }
            };
            var store = new TopicMessageStore(1000, limits);

            var idA1 = Guid.NewGuid();
            var idA2 = Guid.NewGuid();
            var idA3 = Guid.NewGuid();
            var idB1 = Guid.NewGuid();

            var batch1 = new List<(Guid id, string topic, MqttApplicationMessage msg)>
            {
                (idA1, "a", CreateMessage("a", 30, "A1")),
                (idB1, "b", CreateMessage("b", 30, "B1")),
                (idA2, "a", CreateMessage("a", 30, "A2"))
            };

            // Act 1
            var (added1, evicted1) = store.AddBatch(batch1);

            // Assert phase 1
            Assert.Contains(added1, a => a.MessageId == idA1);
            Assert.Contains(added1, a => a.MessageId == idA2);
            Assert.Contains(added1, a => a.MessageId == idB1);
            Assert.Empty(evicted1);

            // Act 2: add third message to topic a to force eviction of A1
            var batch2 = new List<(Guid id, string topic, MqttApplicationMessage msg)>
            {
                (idA3, "a", CreateMessage("a", 30, "A3"))
            };
            var (added2, evicted2) = store.AddBatch(batch2);

            // Assert phase 2
            Assert.Single(added2);
            Assert.Equal(idA3, added2[0].MessageId);

            // Only A1 should be evicted (oldest in topic a). Topic b message must remain.
            Assert.Single(evicted2);
            Assert.Equal(idA1, evicted2[0].MessageId);
            Assert.Equal("a", evicted2[0].Topic);

            // Verify B1 still retrievable
            var foundB = store.TryGetMessage(idB1, out var bMsg, out var bTopic);
            Assert.True(foundB);
            Assert.NotNull(bMsg);
            Assert.Equal("b", bTopic);

            // Verify A1 no longer retrievable, A2 and A3 present
            var foundA1 = store.TryGetMessage(idA1, out _, out _);
            Assert.False(foundA1);
            Assert.True(store.TryGetMessage(idA2, out _, out _));
            Assert.True(store.TryGetMessage(idA3, out _, out _));
        }

        [Fact]
        public void AddBatch_EmptyBatch_NoChanges()
        {
            var store = new TopicMessageStore(1024);
            var (added, evicted) = store.AddBatch(Array.Empty<(Guid, string, MqttApplicationMessage)>());
            Assert.Empty(added);
            Assert.Empty(evicted);
        }

        [Fact]
        public void TryGetMessage_NonExisting_ReturnsFalse()
        {
            var store = new TopicMessageStore(1024);
            var exists = store.TryGetMessage(Guid.NewGuid(), out var msg, out var topic);
            Assert.False(exists);
            Assert.Null(msg);
            Assert.Null(topic);
        }

        [Fact]
        public void AddBatch_MultipleTopics_NoCrossTopicEviction()
        {
            // Arrange
            var limits = new Dictionary<string, long>
            {
                { "x", 60 }, // fits exactly two 30B messages
                { "y", 60 }
            };
            var store = new TopicMessageStore(1000, limits);

            var idX1 = Guid.NewGuid();
            var idX2 = Guid.NewGuid();
            var idY1 = Guid.NewGuid();
            var idY2 = Guid.NewGuid();
            var idX3 = Guid.NewGuid(); // triggers eviction in x only

            // Act 1
            store.AddBatch(new []
            {
                (idX1, "x", CreateMessage("x", 30, "X1")),
                (idY1, "y", CreateMessage("y", 30, "Y1")),
                (idX2, "x", CreateMessage("x", 30, "X2")),
                (idY2, "y", CreateMessage("y", 30, "Y2"))
            });

            // Act 2: overflow x
            var (added2, evicted2) = store.AddBatch(new []
            {
                (idX3, "x", CreateMessage("x", 30, "X3"))
            });

            // Assert: eviction only from x
            Assert.Single(evicted2);
            Assert.Equal("x", evicted2[0].Topic);
            Assert.True(store.TryGetMessage(idY1, out _, out _));
            Assert.True(store.TryGetMessage(idY2, out _, out _));
        }
    }
}
