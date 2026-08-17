using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using ReactiveUI;
using RxVoid = ReactiveUI.Primitives.RxVoid;
using Serilog;

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel behind the statistics window (opened via the <c>:stats</c> command).
/// Publishes a live-updated list of per-topic aggregates and exposes commands to
/// copy the current view as a GitHub-flavored Markdown table.
/// </summary>
public sealed class StatsViewModel : ReactiveObject, IDisposable
{
    private static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly ITopicStatisticsService _service;
    private readonly IScheduler _scheduler;
    private readonly Dictionary<string, TopicStatsRowViewModel> _rowsByTopic = new(StringComparer.Ordinal);
    private IDisposable? _refreshSubscription;
    private string _statusText = string.Empty;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="StatsViewModel"/>.
    /// </summary>
    /// <param name="service">Statistics service to observe.</param>
    /// <param name="scheduler">Scheduler for the refresh timer. Defaults to <see cref="DefaultScheduler.Instance"/>.</param>
    /// <param name="refreshInterval">Refresh cadence. Defaults to 1 s.</param>
    public StatsViewModel(
        ITopicStatisticsService service,
        IScheduler? scheduler = null,
        TimeSpan? refreshInterval = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _scheduler = scheduler ?? DefaultScheduler.Instance;
        RefreshInterval = refreshInterval ?? DefaultRefreshInterval;

        Rows = new ObservableCollection<TopicStatsRowViewModel>();

        CopyTextToClipboardInteraction = new Interaction<string, Unit>();

        RefreshCommand = ReactiveCommand.Create(RefreshNow);
        CopyAsMarkdownCommand = ReactiveCommand.CreateFromTask(CopyAsMarkdownAsync);
        CloseCommand = ReactiveCommand.Create(() => { CloseRequested?.Invoke(this, EventArgs.Empty); });

        // Initial synchronous refresh so the window has data the moment it appears.
        RefreshNow();
    }

    /// <summary>The current refresh cadence.</summary>
    public TimeSpan RefreshInterval { get; }

    /// <summary>Live-updated collection of one row per topic that has received messages.</summary>
    public ObservableCollection<TopicStatsRowViewModel> Rows { get; }

    /// <summary>Status text shown at the bottom of the window (e.g., "Copied 12 rows to clipboard").</summary>
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    /// <summary>Interaction the view uses to fulfill clipboard requests.</summary>
    public Interaction<string, Unit> CopyTextToClipboardInteraction { get; }

    /// <summary>Manual refresh command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }

    /// <summary>Copies the current rows to the clipboard as a Markdown table.</summary>
    public ReactiveCommand<RxVoid, RxVoid> CopyAsMarkdownCommand { get; }

    /// <summary>Closes the window (bound to Escape/close button).</summary>
    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    /// <summary>Raised when <see cref="CloseCommand"/> is invoked so the view can close the window.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Starts the live refresh timer. Idempotent.</summary>
    public void StartLiveRefresh()
    {
        if (_disposed || _refreshSubscription != null)
        {
            return;
        }

        _refreshSubscription = Observable
            .Interval(RefreshInterval, _scheduler)
            .Subscribe(_ => RefreshNow());
    }

    /// <summary>Stops the live refresh timer. Idempotent.</summary>
    public void StopLiveRefresh()
    {
        _refreshSubscription?.Dispose();
        _refreshSubscription = null;
    }

    /// <summary>
    /// Applies a fresh snapshot from the statistics service to <see cref="Rows"/>.
    /// Existing rows are updated in place; new rows are added; rows that disappear
    /// from the snapshot are removed.
    /// </summary>
    public void RefreshNow()
    {
        if (_disposed)
        {
            return;
        }

        IReadOnlyList<TopicStatisticsSnapshot> snapshot;
        try
        {
            snapshot = _service.Snapshot();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to acquire statistics snapshot for :stats window.");
            return;
        }

        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(IReadOnlyList<TopicStatisticsSnapshot> snapshot)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in snapshot)
        {
            seen.Add(s.Topic);
            if (_rowsByTopic.TryGetValue(s.Topic, out var existing))
            {
                existing.MessageCount = s.MessageCount;
                existing.TotalPayloadBytes = s.TotalPayloadBytes;
                existing.AveragePayloadBytes = s.AveragePayloadBytes;
                existing.MeanInterval = s.MeanInterval;
            }
            else
            {
                var row = new TopicStatsRowViewModel(s.Topic)
                {
                    MessageCount = s.MessageCount,
                    TotalPayloadBytes = s.TotalPayloadBytes,
                    AveragePayloadBytes = s.AveragePayloadBytes,
                    MeanInterval = s.MeanInterval,
                };
                _rowsByTopic.Add(s.Topic, row);
                Rows.Add(row);
            }
        }

        // Remove rows for topics that are no longer present (e.g., after Reset()).
        if (_rowsByTopic.Count > seen.Count)
        {
            var toRemove = _rowsByTopic.Keys.Where(k => !seen.Contains(k)).ToList();
            foreach (var topic in toRemove)
            {
                if (_rowsByTopic.TryGetValue(topic, out var row))
                {
                    Rows.Remove(row);
                    _rowsByTopic.Remove(topic);
                }
            }
        }
    }

    private async Task CopyAsMarkdownAsync()
    {
        var markdown = BuildMarkdown(Rows);
        try
        {
            await CopyTextToClipboardInteraction.Handle(markdown);
            StatusText = $"Copied {Rows.Count} row(s) to clipboard as Markdown.";
        }
        catch (UnhandledInteractionException<string, Unit>)
        {
            // No handler registered (e.g., unit test). Still succeed silently
            // and expose the payload via StatusText for debug/inspection.
            StatusText = $"Prepared Markdown for {Rows.Count} row(s) (no clipboard handler).";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to copy statistics to clipboard.");
            StatusText = $"Failed to copy: {ex.Message}";
        }
    }

    /// <summary>
    /// Builds a GitHub-flavored Markdown table representation of the given rows,
    /// preserving their current order (so the copy reflects the user's chosen sort).
    /// </summary>
    public static string BuildMarkdown(IEnumerable<TopicStatsRowViewModel> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Topic | # Messages | Total Size | Avg Size | Mean Interval |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: |");

        foreach (var r in rows)
        {
            sb.Append("| ").Append(EscapeMarkdown(r.Topic)).Append(" | ")
                .Append(r.MessageCount.ToString("N0", CultureInfo.InvariantCulture)).Append(" | ")
                .Append(r.TotalPayloadDisplay).Append(" | ")
                .Append(r.AveragePayloadDisplay).Append(" | ")
                .Append(r.MeanIntervalDisplay).AppendLine(" |");
        }

        return sb.ToString();
    }

    private static string EscapeMarkdown(string value)
    {
        // Escape pipes so multi-segment MQTT topics or user-defined names
        // don't break the table layout.
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        StopLiveRefresh();
        RefreshCommand.Dispose();
        CopyAsMarkdownCommand.Dispose();
        CloseCommand.Dispose();
    }
}

/// <summary>
/// Byte- and time-formatting helpers used by the statistics window.
/// </summary>
internal static class StatsFormatting
{
    private static readonly string[] ByteUnits = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatBytes(long bytes) => FormatBytes((double)bytes);

    public static string FormatBytes(double bytes)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

        var unit = 0;
        var value = bytes;
        while (value >= 1024 && unit < ByteUnits.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        // 0 decimals for bytes, 2 decimals otherwise for compactness.
        var format = unit == 0 ? "F0" : "F2";
        return string.Create(CultureInfo.InvariantCulture, $"{value.ToString(format, CultureInfo.InvariantCulture)} {ByteUnits[unit]}");
    }

    public static string FormatInterval(TimeSpan? interval)
    {
        if (interval is null)
        {
            return "N/A";
        }

        var span = interval.Value;
        if (span.TotalMilliseconds < 1)
        {
            return "< 1 ms";
        }

        if (span.TotalSeconds < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{span.TotalMilliseconds:F0} ms");
        }

        if (span.TotalMinutes < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{span.TotalSeconds:F2} s");
        }

        if (span.TotalHours < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{span.TotalMinutes:F2} m");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{span.TotalHours:F2} h");
    }
}
