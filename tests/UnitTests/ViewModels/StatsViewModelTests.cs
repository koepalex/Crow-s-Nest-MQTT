using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using ReactiveUI;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public sealed class StatsViewModelTests : IDisposable
{

    public StatsViewModelTests()
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static DateTime Utc(int hour, int minute, int second, int ms = 0) =>
        new(2024, 1, 1, hour, minute, second, ms, DateTimeKind.Utc);

    private static StatsViewModel CreateVm(out TopicStatisticsService service, IScheduler? scheduler = null)
    {
        service = new TopicStatisticsService();
        return new StatsViewModel(service, scheduler ?? Scheduler.Immediate);
    }

    [Fact]
    public void RefreshNow_PopulatesRowsFromService()
    {
        using var vm = CreateVm(out var service);
        service.Record("a/b", 100, Utc(12, 0, 0));
        service.Record("a/b", 300, Utc(12, 0, 2));

        vm.RefreshNow();

        var row = Assert.Single(vm.Rows);
        Assert.Equal("a/b", row.Topic);
        Assert.Equal(2, row.MessageCount);
        Assert.Equal(400, row.TotalPayloadBytes);
        Assert.Equal(200.0, row.AveragePayloadBytes);
        Assert.Equal(TimeSpan.FromSeconds(2), row.MeanInterval);
    }

    [Fact]
    public void RefreshNow_ExistingRow_UpdatedInPlace()
    {
        using var vm = CreateVm(out var service);
        service.Record("a", 100, Utc(12, 0, 0));
        vm.RefreshNow();
        var originalRow = Assert.Single(vm.Rows);

        service.Record("a", 200, Utc(12, 0, 1));
        vm.RefreshNow();

        // Same row instance preserved (important for DataGrid sort/selection stability)
        var updatedRow = Assert.Single(vm.Rows);
        Assert.Same(originalRow, updatedRow);
        Assert.Equal(2, updatedRow.MessageCount);
        Assert.Equal(300, updatedRow.TotalPayloadBytes);
    }

    [Fact]
    public void RefreshNow_TopicRemovedFromService_RowRemovedFromView()
    {
        using var vm = CreateVm(out var service);
        service.Record("a", 100, Utc(12, 0, 0));
        vm.RefreshNow();
        Assert.Single(vm.Rows);

        service.Reset();
        vm.RefreshNow();

        Assert.Empty(vm.Rows);
    }

    [Fact]
    public void BuildMarkdown_ProducesExpectedTable()
    {
        var rows = new[]
        {
            new TopicStatsRowViewModel("sensors/temp")
            {
                MessageCount = 10,
                TotalPayloadBytes = 2048,
                AveragePayloadBytes = 204.8,
                MeanInterval = TimeSpan.FromMilliseconds(250)
            },
            new TopicStatsRowViewModel("sensors/humidity")
            {
                MessageCount = 1,
                TotalPayloadBytes = 42,
                AveragePayloadBytes = 42.0,
                MeanInterval = null
            }
        };

        var md = StatsViewModel.BuildMarkdown(rows);

        var lines = md.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("| Topic | # Messages | Total Size | Avg Size | Mean Interval |", lines[0]);
        Assert.StartsWith("| --- | ---: | ---: | ---: | ---: |", lines[1]);
        Assert.Contains("sensors/temp", lines[2]);
        Assert.Contains("10", lines[2]);
        Assert.Contains("250 ms", lines[2]);
        Assert.Contains("sensors/humidity", lines[3]);
        Assert.Contains("N/A", lines[3]);
    }

    [Fact]
    public void BuildMarkdown_EscapesPipeCharacterInTopic()
    {
        var rows = new[]
        {
            new TopicStatsRowViewModel("weird|topic")
            {
                MessageCount = 1,
                TotalPayloadBytes = 1,
                AveragePayloadBytes = 1.0,
                MeanInterval = null
            }
        };

        var md = StatsViewModel.BuildMarkdown(rows);

        Assert.Contains("weird\\|topic", md);
    }

    [Fact]
    public async Task CopyAsMarkdownCommand_InvokesClipboardInteraction()
    {
        using var vm = CreateVm(out var service);
        service.Record("a", 100, Utc(12, 0, 0));
        vm.RefreshNow();

        string? captured = null;
        using (vm.CopyTextToClipboardInteraction.RegisterHandler(interaction =>
        {
            captured = interaction.Input;
            interaction.SetOutput(Unit.Default);
        }))
        {
            await vm.CopyAsMarkdownCommand.Execute();
        }

        Assert.NotNull(captured);
        Assert.Contains("| Topic | # Messages", captured);
        Assert.Contains("a", captured);
        Assert.Contains("Copied", vm.StatusText);
    }

    [Fact]
    public async Task CopyAsMarkdownCommand_WithoutHandler_StatusMentionsMissingHandler()
    {
        using var vm = CreateVm(out _);

        await vm.CopyAsMarkdownCommand.Execute();

        Assert.Contains("clipboard handler", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CloseCommand_RaisesCloseRequestedEvent()
    {
        using var vm = CreateVm(out _);
        var raised = false;
        vm.CloseRequested += (_, _) => raised = true;

        vm.CloseCommand.Execute().Subscribe();

        Assert.True(raised);
    }

    [Fact]
    public void StartLiveRefresh_IsIdempotent()
    {
        var service = new TopicStatisticsService();
        using var vm = new StatsViewModel(service, TaskPoolScheduler.Default, TimeSpan.FromMilliseconds(500));
        vm.StartLiveRefresh();
        vm.StartLiveRefresh(); // Second call must not throw

        vm.StopLiveRefresh();
    }

    [Fact]
    public void StopLiveRefresh_IsSafeWhenNotStarted()
    {
        using var vm = CreateVm(out _);
        vm.StopLiveRefresh(); // must not throw
    }

    [Fact]
    public async Task StartLiveRefresh_TicksApplySnapshotsFromService()
    {
        var service = new TopicStatisticsService();
        using var vm = new StatsViewModel(service, TaskPoolScheduler.Default, TimeSpan.FromMilliseconds(20));
        service.Record("a", 100, Utc(12, 0, 0));

        vm.StartLiveRefresh();

        // Wait for at least one tick to fire.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (vm.Rows.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        vm.StopLiveRefresh();

        var row = Assert.Single(vm.Rows);
        Assert.Equal(1, row.MessageCount);
    }
}
