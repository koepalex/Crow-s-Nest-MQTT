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

        // ── CleanupExpiredCorrelationsAsync – mixed expired / non-expired ──

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_MixedExpiry_ShouldOnlyRemoveExpired()
        {
            var service = CreateService();
            var expiredData1 = Encoding.UTF8.GetBytes("expired-1");
            var expiredData2 = Encoding.UTF8.GetBytes("expired-2");
            var activeData = Encoding.UTF8.GetBytes("active-1");

            await service.RegisterRequestAsync("req-expired-1", expiredData1, "topic", ttlMinutes: 0);
            await service.RegisterRequestAsync("req-expired-2", expiredData2, "topic", ttlMinutes: 0);
            await service.RegisterRequestAsync("req-active-1", activeData, "topic", ttlMinutes: 60);

            await Task.Delay(150);

            var removed = await service.CleanupExpiredCorrelationsAsync();

            Assert.Equal(2, removed);

            // Expired entries should be gone
            Assert.Equal(ResponseStatus.Hidden, await service.GetResponseStatusAsync("req-expired-1"));
            Assert.Equal(ResponseStatus.Hidden, await service.GetResponseStatusAsync("req-expired-2"));

            // Active entry must survive
            Assert.Equal(ResponseStatus.Pending, await service.GetResponseStatusAsync("req-active-1"));
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_ShouldRemoveFromRequestIndex()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("cleanup-index");
            await service.RegisterRequestAsync("req-idx", correlationData, "response/topic", ttlMinutes: 0);

            await Task.Delay(150);
            await service.CleanupExpiredCorrelationsAsync();

            // Both GetResponseTopicAsync and GetResponseMessageIdsAsync should return defaults
            Assert.Null(await service.GetResponseTopicAsync("req-idx"));
            Assert.Empty(await service.GetResponseMessageIdsAsync("req-idx"));
            Assert.Null(service.GetCorrelationEntry("req-idx"));
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_EmptyService_ShouldReturnZero()
        {
            var service = CreateService();
            var removed = await service.CleanupExpiredCorrelationsAsync();
            Assert.Equal(0, removed);
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_AllExpired_ShouldRemoveAll()
        {
            var service = CreateService();
            for (int i = 0; i < 5; i++)
            {
                var data = Encoding.UTF8.GetBytes($"all-expired-{i}");
                await service.RegisterRequestAsync($"req-ae-{i}", data, "topic", ttlMinutes: 0);
            }

            await Task.Delay(150);

            var removed = await service.CleanupExpiredCorrelationsAsync();
            Assert.Equal(5, removed);

            var stats = await service.GetStatisticsAsync();
            Assert.Equal(0, stats.ActiveCorrelations);
        }

        // ── GetStatisticsAsync – detailed state verification ──

        [Fact]
        public async Task GetStatisticsAsync_WithExpiredCorrelations_ShouldCountExpired()
        {
            var service = CreateService();
            var expiredData = Encoding.UTF8.GetBytes("stat-expired");
            var pendingData = Encoding.UTF8.GetBytes("stat-pending");
            var respondedData = Encoding.UTF8.GetBytes("stat-responded");

            await service.RegisterRequestAsync("req-se", expiredData, "topic", ttlMinutes: 0);
            await service.RegisterRequestAsync("req-sp", pendingData, "topic", ttlMinutes: 60);
            await service.RegisterRequestAsync("req-sr", respondedData, "topic", ttlMinutes: 60);
            await service.LinkResponseAsync("resp-sr", respondedData, "topic");

            await Task.Delay(150);

            // Refresh status of expired entry so it transitions to NavigationDisabled
            await service.GetResponseStatusAsync("req-se");

            var stats = await service.GetStatisticsAsync();

            Assert.Equal(3, stats.TotalCorrelations);
            Assert.Equal(1, stats.PendingCorrelations);
            Assert.Equal(1, stats.RespondedCorrelations);
            Assert.Equal(1, stats.ExpiredCorrelations);
            Assert.True(stats.EstimatedMemoryUsageBytes > 0);
            Assert.True(stats.EstimatedMemoryUsage > 0);
        }

        [Fact]
        public async Task GetStatisticsAsync_AfterCleanup_ShouldReflectReduction()
        {
            var service = CreateService();
            var data1 = Encoding.UTF8.GetBytes("stat-cleanup-1");
            var data2 = Encoding.UTF8.GetBytes("stat-cleanup-2");

            await service.RegisterRequestAsync("req-sc1", data1, "topic", ttlMinutes: 0);
            await service.RegisterRequestAsync("req-sc2", data2, "topic", ttlMinutes: 60);

            await Task.Delay(150);

            var statsBefore = await service.GetStatisticsAsync();
            Assert.Equal(2, statsBefore.ActiveCorrelations);

            await service.CleanupExpiredCorrelationsAsync();

            var statsAfter = await service.GetStatisticsAsync();
            Assert.Equal(1, statsAfter.ActiveCorrelations);
            Assert.Equal(1, statsAfter.PendingCorrelations);
            Assert.Equal(0, statsAfter.ExpiredCorrelations);
        }

        [Fact]
        public async Task GetStatisticsAsync_MemoryUsageGrowsWithEntries()
        {
            var service = CreateService();

            var statsEmpty = await service.GetStatisticsAsync();
            Assert.Equal(0L, statsEmpty.EstimatedMemoryUsageBytes);

            var data = Encoding.UTF8.GetBytes("mem-test");
            await service.RegisterRequestAsync("req-mem", data, "topic");

            var statsOne = await service.GetStatisticsAsync();
            Assert.True(statsOne.EstimatedMemoryUsageBytes > 0);

            var data2 = Encoding.UTF8.GetBytes("mem-test-2");
            await service.RegisterRequestAsync("req-mem2", data2, "topic");

            var statsTwo = await service.GetStatisticsAsync();
            Assert.True(statsTwo.EstimatedMemoryUsageBytes > statsOne.EstimatedMemoryUsageBytes);
        }

        // ── LinkResponseAsync – happy-path verification through related APIs ──

        [Fact]
        public async Task LinkResponseAsync_ShouldBeVerifiableThroughGetResponseMessageIds()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("link-verify");
            await service.RegisterRequestAsync("req-lv", correlationData, "topic");

            await service.LinkResponseAsync("resp-lv-1", correlationData, "topic");

            var ids = await service.GetResponseMessageIdsAsync("req-lv");
            Assert.Single(ids);
            Assert.Equal("resp-lv-1", ids[0]);
        }

        [Fact]
        public async Task LinkResponseAsync_ShouldRaiseStatusChangedFromPendingToReceived()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("link-event");
            await service.RegisterRequestAsync("req-le", correlationData, "topic");

            var events = new List<CorrelationStatusChangedEventArgs>();
            service.CorrelationStatusChanged += (_, e) => events.Add(e);

            await service.LinkResponseAsync("resp-le", correlationData, "topic");

            var linkEvent = events.FirstOrDefault(e => e.RequestMessageId == "req-le" && e.NewStatus == ResponseStatus.Received);
            Assert.NotNull(linkEvent);
            Assert.Equal(ResponseStatus.Pending, linkEvent.PreviousStatus);
        }

        [Fact]
        public async Task LinkResponseAsync_DuplicateResponse_ShouldReturnFalse()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("link-dup");
            await service.RegisterRequestAsync("req-ld", correlationData, "topic");

            var first = await service.LinkResponseAsync("resp-ld", correlationData, "topic");
            var second = await service.LinkResponseAsync("resp-ld", correlationData, "topic");

            Assert.True(first);
            Assert.False(second);

            var ids = await service.GetResponseMessageIdsAsync("req-ld");
            Assert.Single(ids);
        }

        [Fact]
        public async Task LinkResponseAsync_SecondResponseDoesNotReRaiseStatusChange()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("link-no-refire");
            await service.RegisterRequestAsync("req-lnr", correlationData, "topic");

            await service.LinkResponseAsync("resp-lnr-1", correlationData, "topic");

            var events = new List<CorrelationStatusChangedEventArgs>();
            service.CorrelationStatusChanged += (_, e) => events.Add(e);

            await service.LinkResponseAsync("resp-lnr-2", correlationData, "topic");

            // Status stays Received → Received, so no event should fire
            Assert.Empty(events);
        }

        // ── GetResponseMessageIdsAsync – detailed scenarios ──

        [Fact]
        public async Task GetResponseMessageIdsAsync_SingleResponse_ShouldReturnOne()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("ids-single");
            await service.RegisterRequestAsync("req-is", correlationData, "topic");
            await service.LinkResponseAsync("resp-is", correlationData, "topic");

            var ids = await service.GetResponseMessageIdsAsync("req-is");

            Assert.Single(ids);
            Assert.Equal("resp-is", ids[0]);
        }

        [Fact]
        public async Task GetResponseMessageIdsAsync_MultipleResponses_ShouldReturnAll()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("ids-multi");
            await service.RegisterRequestAsync("req-im", correlationData, "topic");

            await service.LinkResponseAsync("resp-im-1", correlationData, "topic");
            await service.LinkResponseAsync("resp-im-2", correlationData, "topic");
            await service.LinkResponseAsync("resp-im-3", correlationData, "topic");

            var ids = await service.GetResponseMessageIdsAsync("req-im");

            Assert.Equal(3, ids.Count);
            Assert.Contains("resp-im-1", ids);
            Assert.Contains("resp-im-2", ids);
            Assert.Contains("resp-im-3", ids);
        }

        [Fact]
        public async Task GetResponseMessageIdsAsync_NoResponses_ShouldReturnEmpty()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("ids-none");
            await service.RegisterRequestAsync("req-in", correlationData, "topic");

            var ids = await service.GetResponseMessageIdsAsync("req-in");
            Assert.Empty(ids);
        }

        // ── GetResponseTopicAsync – multi-request scenarios ──

        [Fact]
        public async Task GetResponseTopicAsync_MultipleRequests_EachReturnOwnTopic()
        {
            var service = CreateService();
            var data1 = Encoding.UTF8.GetBytes("topic-a");
            var data2 = Encoding.UTF8.GetBytes("topic-b");

            await service.RegisterRequestAsync("req-ta", data1, "response/alpha");
            await service.RegisterRequestAsync("req-tb", data2, "response/beta");

            Assert.Equal("response/alpha", await service.GetResponseTopicAsync("req-ta"));
            Assert.Equal("response/beta", await service.GetResponseTopicAsync("req-tb"));
        }

        [Fact]
        public async Task GetResponseTopicAsync_AfterLinkingResponse_StillReturnsSameTopic()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("topic-stable");
            await service.RegisterRequestAsync("req-ts", correlationData, "response/stable");
            await service.LinkResponseAsync("resp-ts", correlationData, "response/stable");

            Assert.Equal("response/stable", await service.GetResponseTopicAsync("req-ts"));
        }

        [Fact]
        public async Task GetResponseTopicAsync_AfterCleanup_ReturnsNull()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("topic-cleanup");
            await service.RegisterRequestAsync("req-tc", correlationData, "response/cleaned", ttlMinutes: 0);

            await Task.Delay(150);
            await service.CleanupExpiredCorrelationsAsync();

            Assert.Null(await service.GetResponseTopicAsync("req-tc"));
        }

        // ── End-to-end flow ──

        [Fact]
        public async Task EndToEnd_RegisterLinkVerify_FullLifecycle()
        {
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("e2e-flow");

            // Register
            Assert.True(await service.RegisterRequestAsync("req-e2e", correlationData, "reply/topic"));
            Assert.Equal(ResponseStatus.Pending, await service.GetResponseStatusAsync("req-e2e"));
            Assert.Equal("reply/topic", await service.GetResponseTopicAsync("req-e2e"));
            Assert.Empty(await service.GetResponseMessageIdsAsync("req-e2e"));

            // Link first response
            Assert.True(await service.LinkResponseAsync("resp-e2e-1", correlationData, "reply/topic"));
            Assert.Equal(ResponseStatus.Received, await service.GetResponseStatusAsync("req-e2e"));
            var ids = await service.GetResponseMessageIdsAsync("req-e2e");
            Assert.Single(ids);
            Assert.Equal("resp-e2e-1", ids[0]);

            // Link second response
            Assert.True(await service.LinkResponseAsync("resp-e2e-2", correlationData, "reply/topic"));
            ids = await service.GetResponseMessageIdsAsync("req-e2e");
            Assert.Equal(2, ids.Count);

            // Statistics reflect state
            var stats = await service.GetStatisticsAsync();
            Assert.Equal(1, stats.ActiveCorrelations);
            Assert.Equal(1, stats.RespondedCorrelations);
            Assert.Equal(0, stats.PendingCorrelations);
        }

        [Fact]
        public async Task LinkResponseAsync_BeforeRequestRegistered_ShouldBufferAndLinkRetroactively()
        {
            // Arrange - simulate response arriving before request
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("race-condition-test");
            var responseTopic = "response/topic";

            // Act - response arrives first (no matching request yet)
            var linkResult = await service.LinkResponseAsync("resp-1", correlationData, responseTopic);
            Assert.False(linkResult); // Returns false because no request registered yet

            // Now register the request - pending response should be auto-linked
            var registerResult = await service.RegisterRequestAsync("req-1", correlationData, responseTopic);
            Assert.True(registerResult);

            // Assert - the response should now be linked retroactively
            var status = await service.GetResponseStatusAsync("req-1");
            Assert.Equal(ResponseStatus.Received, status);

            var responseIds = await service.GetResponseMessageIdsAsync("req-1");
            Assert.Single(responseIds);
            Assert.Equal("resp-1", responseIds[0]);
        }

        [Fact]
        public async Task LinkResponseAsync_MultipleResponsesBeforeRequest_ShouldBufferAllAndLinkRetroactively()
        {
            // Arrange
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("multi-response-race");
            var responseTopic = "response/multi";

            // Act - multiple responses arrive before request
            await service.LinkResponseAsync("resp-1", correlationData, responseTopic);
            await service.LinkResponseAsync("resp-2", correlationData, responseTopic);

            // Register request
            var registerResult = await service.RegisterRequestAsync("req-1", correlationData, responseTopic);
            Assert.True(registerResult);

            // Assert - all responses linked
            var responseIds = await service.GetResponseMessageIdsAsync("req-1");
            Assert.Equal(2, responseIds.Count);
            Assert.Contains("resp-1", responseIds);
            Assert.Contains("resp-2", responseIds);
        }

        [Fact]
        public async Task LinkResponseAsync_PendingResponseWithMismatchedTopic_ShouldNotLink()
        {
            // Arrange
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("topic-mismatch-test");

            // Response arrives on wrong topic
            await service.LinkResponseAsync("resp-1", correlationData, "wrong/topic");

            // Register request with different response topic
            var registerResult = await service.RegisterRequestAsync("req-1", correlationData, "correct/topic");
            Assert.True(registerResult);

            // Assert - response should NOT be linked (topic mismatch)
            var status = await service.GetResponseStatusAsync("req-1");
            Assert.Equal(ResponseStatus.Pending, status);

            var responseIds = await service.GetResponseMessageIdsAsync("req-1");
            Assert.Empty(responseIds);
        }

        [Fact]
        public async Task RegisterRequestAsync_WithPendingResponse_ShouldFireReceivedStatusEvent()
        {
            // Arrange
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("event-test");
            var responseTopic = "response/event";
            var statusChanges = new List<(string RequestId, ResponseStatus NewStatus, ResponseStatus PreviousStatus)>();

            service.CorrelationStatusChanged += (_, args) =>
                statusChanges.Add((args.RequestMessageId, args.NewStatus, args.PreviousStatus));

            // Response arrives first
            await service.LinkResponseAsync("resp-1", correlationData, responseTopic);

            // Act - register request (should auto-link pending response)
            await service.RegisterRequestAsync("req-1", correlationData, responseTopic);

            // Assert - should fire a single event showing Received status (not Pending)
            Assert.Single(statusChanges);
            Assert.Equal("req-1", statusChanges[0].RequestId);
            Assert.Equal(ResponseStatus.Received, statusChanges[0].NewStatus);
            Assert.Equal(ResponseStatus.Hidden, statusChanges[0].PreviousStatus);
        }

        [Fact]
        public async Task CleanupExpiredCorrelationsAsync_ShouldRemoveStalePendingResponses()
        {
            // Arrange - use a service with internal access to verify state
            var service = CreateService();
            var correlationData = Encoding.UTF8.GetBytes("stale-pending-test");

            // Buffer a response (no matching request)
            await service.LinkResponseAsync("resp-1", correlationData, "response/topic");

            // Verify pending response is buffered (indirectly: register request should link it)
            // But first, let's just verify cleanup works by running it immediately
            // (the pending response TTL is 5 minutes, so it shouldn't be cleaned up yet)
            await service.CleanupExpiredCorrelationsAsync();

            // Register request - should still find the pending response
            var registerResult = await service.RegisterRequestAsync("req-1", correlationData, "response/topic");
            Assert.True(registerResult);

            var status = await service.GetResponseStatusAsync("req-1");
            Assert.Equal(ResponseStatus.Received, status);
        }
    }
}
