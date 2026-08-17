using CrowsNestMqtt.BusinessLogic.Services;
using Xunit;

namespace UnitTests.Services;

/// <summary>
/// Tests for <see cref="TopicStatisticsService"/> — the lifetime per-topic
/// aggregator that backs the <c>:stats</c> command.
/// </summary>
public class TopicStatisticsServiceTests
{
    private static DateTime Utc(int hour, int minute, int second, int ms = 0) =>
        new(2024, 1, 1, hour, minute, second, ms, DateTimeKind.Utc);

    [Fact]
    public void Snapshot_Empty_ReturnsNoRows()
    {
        var service = new TopicStatisticsService();

        Assert.Empty(service.Snapshot());
    }

    [Fact]
    public void Record_Single_ProducesRowWithNullMeanInterval()
    {
        var service = new TopicStatisticsService();
        service.Record("a/b", 100, Utc(12, 0, 0));

        var snap = Assert.Single(service.Snapshot());
        Assert.Equal("a/b", snap.Topic);
        Assert.Equal(1, snap.MessageCount);
        Assert.Equal(100, snap.TotalPayloadBytes);
        Assert.Equal(100.0, snap.AveragePayloadBytes);
        Assert.Null(snap.MeanInterval);
        Assert.Equal(Utc(12, 0, 0), snap.FirstTimestampUtc);
        Assert.Equal(Utc(12, 0, 0), snap.LastTimestampUtc);
    }

    [Fact]
    public void Record_MultipleMessagesSameTopic_ComputesAveragesAndInterval()
    {
        var service = new TopicStatisticsService();
        service.Record("sensors/temp", 100, Utc(12, 0, 0));
        service.Record("sensors/temp", 300, Utc(12, 0, 2));
        service.Record("sensors/temp", 200, Utc(12, 0, 4));

        var snap = Assert.Single(service.Snapshot());
        Assert.Equal("sensors/temp", snap.Topic);
        Assert.Equal(3, snap.MessageCount);
        Assert.Equal(600, snap.TotalPayloadBytes);
        Assert.Equal(200.0, snap.AveragePayloadBytes);
        Assert.NotNull(snap.MeanInterval);
        // 3 messages over 4 seconds → 2 intervals → 2 s each
        Assert.Equal(TimeSpan.FromSeconds(2), snap.MeanInterval);
    }

    [Fact]
    public void Record_MultipleTopics_TrackedIndependently()
    {
        var service = new TopicStatisticsService();
        service.Record("a", 100, Utc(12, 0, 0));
        service.Record("a", 100, Utc(12, 0, 1));
        service.Record("b", 500, Utc(12, 0, 0));

        var snapshots = service.Snapshot().OrderBy(s => s.Topic).ToList();
        Assert.Equal(2, snapshots.Count);

        Assert.Equal("a", snapshots[0].Topic);
        Assert.Equal(2, snapshots[0].MessageCount);
        Assert.Equal(200, snapshots[0].TotalPayloadBytes);
        Assert.Equal(TimeSpan.FromSeconds(1), snapshots[0].MeanInterval);

        Assert.Equal("b", snapshots[1].Topic);
        Assert.Equal(1, snapshots[1].MessageCount);
        Assert.Equal(500, snapshots[1].TotalPayloadBytes);
        Assert.Null(snapshots[1].MeanInterval);
    }

    [Fact]
    public void Record_NegativePayloadTreatedAsZero()
    {
        var service = new TopicStatisticsService();
        service.Record("a", -50, Utc(12, 0, 0));
        service.Record("a", 100, Utc(12, 0, 1));

        var snap = Assert.Single(service.Snapshot());
        Assert.Equal(100, snap.TotalPayloadBytes);
        Assert.Equal(50.0, snap.AveragePayloadBytes);
    }

    [Fact]
    public void Record_NullOrWhitespaceTopic_Ignored()
    {
        var service = new TopicStatisticsService();
        service.Record(null!, 100, Utc(12, 0, 0));
        service.Record("", 100, Utc(12, 0, 0));
        service.Record("   ", 100, Utc(12, 0, 0));

        Assert.Empty(service.Snapshot());
    }

    [Fact]
    public void Reset_ClearsAllTopics()
    {
        var service = new TopicStatisticsService();
        service.Record("a", 100, Utc(12, 0, 0));
        service.Record("b", 200, Utc(12, 0, 1));
        Assert.Equal(2, service.Snapshot().Count);

        service.Reset();

        Assert.Empty(service.Snapshot());
    }

    [Fact]
    public void Record_AfterReset_TreatsTopicAsFresh()
    {
        var service = new TopicStatisticsService();
        service.Record("a", 100, Utc(12, 0, 0));
        service.Record("a", 100, Utc(12, 0, 5));
        service.Reset();

        service.Record("a", 50, Utc(13, 0, 0));

        var snap = Assert.Single(service.Snapshot());
        Assert.Equal(1, snap.MessageCount);
        Assert.Equal(50, snap.TotalPayloadBytes);
        Assert.Equal(Utc(13, 0, 0), snap.FirstTimestampUtc);
        Assert.Null(snap.MeanInterval);
    }

    [Fact]
    public void Record_OutOfOrderTimestamps_ClampedToZeroInterval()
    {
        // Guarantees mean-interval calculation never produces a negative TimeSpan
        // when a batch arrives with a slightly earlier timestamp than the previous
        // one due to clock skew.
        var service = new TopicStatisticsService();
        service.Record("a", 100, Utc(12, 0, 5));
        service.Record("a", 100, Utc(12, 0, 0)); // earlier

        var snap = Assert.Single(service.Snapshot());
        Assert.NotNull(snap.MeanInterval);
        Assert.True(snap.MeanInterval!.Value >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Record_ManyMessages_ConcurrentSafe()
    {
        var service = new TopicStatisticsService();
        const int workers = 8;
        const int perWorker = 5000;
        using var barrier = new Barrier(workers);
        var start = Utc(0, 0, 0);

        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < perWorker; i++)
            {
                service.Record($"topic/{w % 3}", 10, start.AddMilliseconds(i));
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var snapshots = service.Snapshot().OrderBy(s => s.Topic).ToList();
        var total = snapshots.Sum(s => s.MessageCount);
        Assert.Equal((long)workers * perWorker, total);
        // 3 partitions (w % 3): 0,1,2 → but only workers assigned partitions
        // Distribution: worker w -> partition w % 3, so with 8 workers we cover partitions {0,1,2}.
        Assert.Equal(3, snapshots.Count);
        foreach (var snap in snapshots)
        {
            Assert.True(snap.MessageCount > 0);
            Assert.Equal(snap.MessageCount * 10, snap.TotalPayloadBytes);
        }
    }
}
