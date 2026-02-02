using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;

namespace CrowsNestMqtt.UnitTests.BusinessLogic.Services
{
    public class MessageCorrelationServiceUnitTests
    {
        private static MessageCorrelationService CreateService() => new();

        [Fact]
        public async Task RegisterRequestAsync_WithValidData_ShouldSucceed()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test-correlation");
            var result = await service.RegisterRequestAsync("req-1", correlationData, "response/topic");
            Assert.True(result);
        }

        [Fact]
        public async Task RegisterRequestAsync_WithNullRequestId_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterRequestAsync(null!, correlationData, "topic"));
        }

        [Fact]
        public async Task RegisterRequestAsync_WithEmptyRequestId_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterRequestAsync("", correlationData, "topic"));
        }

        [Fact]
        public async Task RegisterRequestAsync_WithNullCorrelationData_ShouldThrow()
        {
            var service = CreateService();
            await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterRequestAsync("req-1", null!, "topic"));
        }

        [Fact]
        public async Task RegisterRequestAsync_WithEmptyCorrelationData_ShouldThrow()
        {
            var service = CreateService();
            await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterRequestAsync("req-1", Array.Empty<byte>(), "topic"));
        }

        [Fact]
        public async Task RegisterRequestAsync_WithNullResponseTopic_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterRequestAsync("req-1", correlationData, null!));
        }

        [Fact]
        public async Task RegisterRequestAsync_WithEmptyResponseTopic_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterRequestAsync("req-1", correlationData, ""));
        }

        [Fact]
        public async Task RegisterRequestAsync_DuplicateCorrelationData_ShouldReturnFalse()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("duplicate");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            var result = await service.RegisterRequestAsync("req-2", correlationData, "topic");
            Assert.False(result);
        }

        [Fact]
        public async Task RegisterRequestAsync_ShouldRaiseStatusChangedEvent()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            var eventRaised = false;
            string? eventRequestId = null;
            ResponseStatus? newStatus = null;

            service.CorrelationStatusChanged += (s, e) =>
            {
                eventRaised = true;
                eventRequestId = e.RequestMessageId;
                newStatus = e.NewStatus;
            };

            await service.RegisterRequestAsync("req-1", correlationData, "topic");

            Assert.True(eventRaised);
            Assert.Equal("req-1", eventRequestId);
            Assert.Equal(ResponseStatus.Pending, newStatus);
        }

        [Fact]
        public async Task LinkResponseAsync_WithValidData_ShouldSucceed()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            var result = await service.LinkResponseAsync("resp-1", correlationData, "topic");
            Assert.True(result);
        }

        [Fact]
        public async Task LinkResponseAsync_WithNullResponseId_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.LinkResponseAsync(null!, correlationData, "topic"));
        }

        [Fact]
        public async Task LinkResponseAsync_WithEmptyResponseId_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.LinkResponseAsync("", correlationData, "topic"));
        }

        [Fact]
        public async Task LinkResponseAsync_WithNullCorrelationData_ShouldThrow()
        {
            var service = CreateService();
            await Assert.ThrowsAsync<ArgumentException>(() => service.LinkResponseAsync("resp-1", null!, "topic"));
        }

        [Fact]
        public async Task LinkResponseAsync_WithEmptyCorrelationData_ShouldThrow()
        {
            var service = CreateService();
            await Assert.ThrowsAsync<ArgumentException>(() => service.LinkResponseAsync("resp-1", Array.Empty<byte>(), "topic"));
        }

        [Fact]
        public async Task LinkResponseAsync_WithNullResponseTopic_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.LinkResponseAsync("resp-1", correlationData, null!));
        }

        [Fact]
        public async Task LinkResponseAsync_WithEmptyResponseTopic_ShouldThrow()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await Assert.ThrowsAsync<ArgumentException>(() => service.LinkResponseAsync("resp-1", correlationData, ""));
        }

        [Fact]
        public async Task LinkResponseAsync_NonExistentCorrelation_ShouldReturnFalse()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("nonexistent");
            var result = await service.LinkResponseAsync("resp-1", correlationData, "topic");
            Assert.False(result);
        }

        [Fact]
        public async Task LinkResponseAsync_MismatchedResponseTopic_ShouldReturnFalse()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic1");
            var result = await service.LinkResponseAsync("resp-1", correlationData, "topic2");
            Assert.False(result);
        }

        [Fact]
        public async Task LinkResponseAsync_ShouldUpdateStatusToReceived()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            await service.LinkResponseAsync("resp-1", correlationData, "topic");
            var status = await service.GetResponseStatusAsync("req-1");
            Assert.Equal(ResponseStatus.Received, status);
        }

        [Fact]
        public async Task LinkResponseAsync_MultipleResponses_ShouldAllSucceed()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            var result1 = await service.LinkResponseAsync("resp-1", correlationData, "topic");
            var result2 = await service.LinkResponseAsync("resp-2", correlationData, "topic");
            var result3 = await service.LinkResponseAsync("resp-3", correlationData, "topic");
            Assert.True(result1);
            Assert.True(result2);
            Assert.True(result3);
        }

        [Fact]
        public async Task GetResponseStatusAsync_UnregisteredRequest_ShouldReturnHidden()
        {
            var service = CreateService();
            var status = await service.GetResponseStatusAsync("nonexistent");
            Assert.Equal(ResponseStatus.Hidden, status);
        }

        [Fact]
        public async Task GetResponseStatusAsync_NullRequestId_ShouldReturnHidden()
        {
            var service = CreateService();
            var status = await service.GetResponseStatusAsync(null!);
            Assert.Equal(ResponseStatus.Hidden, status);
        }

        [Fact]
        public async Task GetResponseStatusAsync_EmptyRequestId_ShouldReturnHidden()
        {
            var service = CreateService();
            var status = await service.GetResponseStatusAsync("");
            Assert.Equal(ResponseStatus.Hidden, status);
        }

        [Fact]
        public async Task GetResponseStatusAsync_PendingRequest_ShouldReturnPending()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            var status = await service.GetResponseStatusAsync("req-1");
            Assert.Equal(ResponseStatus.Pending, status);
        }

        [Fact]
        public async Task GetResponseMessageIdsAsync_NullRequestId_ShouldReturnEmpty()
        {
            var service = CreateService();
            var ids = await service.GetResponseMessageIdsAsync(null!);
            Assert.Empty(ids);
        }

        [Fact]
        public async Task GetResponseMessageIdsAsync_EmptyRequestId_ShouldReturnEmpty()
        {
            var service = CreateService();
            var ids = await service.GetResponseMessageIdsAsync("");
            Assert.Empty(ids);
        }

        [Fact]
        public async Task GetResponseMessageIdsAsync_UnregisteredRequest_ShouldReturnEmpty()
        {
            var service = CreateService();
            var ids = await service.GetResponseMessageIdsAsync("nonexistent");
            Assert.Empty(ids);
        }

        [Fact]
        public async Task GetResponseMessageIdsAsync_WithLinkedResponses_ShouldReturnAll()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            await service.LinkResponseAsync("resp-1", correlationData, "topic");
            await service.LinkResponseAsync("resp-2", correlationData, "topic");
            var ids = await service.GetResponseMessageIdsAsync("req-1");
            Assert.Equal(2, ids.Count);
            Assert.Contains("resp-1", ids);
            Assert.Contains("resp-2", ids);
        }

        [Fact]
        public async Task GetResponseTopicAsync_NullRequestId_ShouldReturnNull()
        {
            var service = CreateService();
            var topic = await service.GetResponseTopicAsync(null!);
            Assert.Null(topic);
        }

        [Fact]
        public async Task GetResponseTopicAsync_EmptyRequestId_ShouldReturnNull()
        {
            var service = CreateService();
            var topic = await service.GetResponseTopicAsync("");
            Assert.Null(topic);
        }

        [Fact]
        public async Task GetResponseTopicAsync_UnregisteredRequest_ShouldReturnNull()
        {
            var service = CreateService();
            var topic = await service.GetResponseTopicAsync("nonexistent");
            Assert.Null(topic);
        }

        [Fact]
        public async Task GetResponseTopicAsync_RegisteredRequest_ShouldReturnTopic()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "response/topic");
            var topic = await service.GetResponseTopicAsync("req-1");
            Assert.Equal("response/topic", topic);
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_WithNoExpired_ShouldReturnZero()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic", ttlMinutes: 30);
            var count = await service.CleanupExpiredCorrelationsAsync();
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_WithExpired_ShouldRemove()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic", ttlMinutes: 0);
            await Task.Delay(100);
            var count = await service.CleanupExpiredCorrelationsAsync();
            Assert.True(count > 0);
            var status = await service.GetResponseStatusAsync("req-1");
            Assert.Equal(ResponseStatus.Hidden, status);
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_ShouldRaiseStatusChangedEvent()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic", ttlMinutes: 0);
            await Task.Delay(100);

            var eventRaised = false;
            ResponseStatus? newStatus = null;

            service.CorrelationStatusChanged += (s, e) =>
            {
                if (e.NewStatus == ResponseStatus.Hidden)
                {
                    eventRaised = true;
                    newStatus = e.NewStatus;
                }
            };

            await service.CleanupExpiredCorrelationsAsync();
            Assert.True(eventRaised);
            Assert.Equal(ResponseStatus.Hidden, newStatus);
        }

        [Fact]
        public async Task GetStatisticsAsync_InitialState_ShouldReturnDefaults()
        {
            var service = CreateService();
            var stats = await service.GetStatisticsAsync();
            Assert.Equal(0, stats.ActiveCorrelations);
            Assert.Equal(0, stats.PendingCorrelations);
            Assert.Equal(0, stats.RespondedCorrelations);
        }

        [Fact]
        public async Task GetStatisticsAsync_WithActiveCorrelations_ShouldReflectState()
        {
            var service = CreateService();
            var correlationData1 = Encoding.UTF8.GetBytes("test1");
            var correlationData2 = Encoding.UTF8.GetBytes("test2");
            await service.RegisterRequestAsync("req-1", correlationData1, "topic");
            await service.RegisterRequestAsync("req-2", correlationData2, "topic");
            await service.LinkResponseAsync("resp-1", correlationData1, "topic");
            var stats = await service.GetStatisticsAsync();
            Assert.Equal(2, stats.ActiveCorrelations);
            Assert.Equal(1, stats.PendingCorrelations);
            Assert.Equal(1, stats.RespondedCorrelations);
            Assert.True(stats.EstimatedMemoryUsageBytes > 0);
        }

        [Fact]
        public async Task ConcurrentRegisterRequests_ShouldAllSucceed()
        {
            var service = CreateService();
            var tasks = new List<Task<bool>>();
            for (int i = 0; i < 100; i++)
            {
                var correlationData = Encoding.UTF8.GetBytes($"test-{i}");
                tasks.Add(service.RegisterRequestAsync($"req-{i}", correlationData, "topic"));
            }
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r));
            var stats = await service.GetStatisticsAsync();
            Assert.Equal(100, stats.ActiveCorrelations);
        }

        [Fact]
        public async Task ConcurrentLinkResponses_ShouldAllSucceed()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            var tasks = new List<Task<bool>>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(service.LinkResponseAsync($"resp-{i}", correlationData, "topic"));
            }
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r));
            var ids = await service.GetResponseMessageIdsAsync("req-1");
            Assert.Equal(50, ids.Count);
        }

        [Fact]
        public async Task GetCorrelationEntry_WithValidRequestId_ShouldReturnEntry()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("test");
            await service.RegisterRequestAsync("req-1", correlationData, "topic");
            var entry = service.GetCorrelationEntry("req-1");
            Assert.NotNull(entry);
        }

        [Fact]
        public void GetCorrelationEntry_WithNullRequestId_ShouldReturnNull()
        {
            var service = CreateService();
            var entry = service.GetCorrelationEntry(null!);
            Assert.Null(entry);
        }

        [Fact]
        public void GetCorrelationEntry_WithEmptyRequestId_ShouldReturnNull()
        {
            var service = CreateService();
            var entry = service.GetCorrelationEntry("");
            Assert.Null(entry);
        }

        [Fact]
        public async Task GetAllCorrelations_ShouldReturnAllEntries()
        {
            var service = CreateService();
            var correlationData1 = Encoding.UTF8.GetBytes("test1");
            var correlationData2 = Encoding.UTF8.GetBytes("test2");
            await service.RegisterRequestAsync("req-1", correlationData1, "topic");
            await service.RegisterRequestAsync("req-2", correlationData2, "topic");
            var correlations = service.GetAllCorrelations();
            Assert.Equal(2, correlations.Count);
        }
    }
}
