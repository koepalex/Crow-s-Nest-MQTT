namespace CrowsNestMqtt.UI.Services;

using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Threading;
using Avalonia.Threading;

/// <summary>
/// An <see cref="IScheduler"/> that dispatches work to the Avalonia UI thread via <see cref="Dispatcher.UIThread"/>.
/// Introduced as a drop-in replacement for <c>ReactiveUI.Avalonia.RxSchedulers.MainThreadScheduler</c>, whose return
/// type changed from <see cref="IScheduler"/> to <c>ReactiveUI.Primitives.Concurrency.ISequencer</c> in ReactiveUI 24
/// (bundled with ReactiveUI.Avalonia 12.1.0). See the migration decision in the session plan.
/// Semantics: schedules a work item that dispatches to the Avalonia UI thread; behaves the same as
/// the previous <c>MainThreadScheduler</c> for the codebase's usage (<c>ObserveOn</c> + <c>SubscribeOn</c>).
/// </summary>
public sealed class AvaloniaUIScheduler : IScheduler
{
    /// <summary>
    /// Shared instance suitable for use with Rx <see cref="ObservableExtensions"/> methods like <c>ObserveOn</c>.
    /// </summary>
    public static IScheduler Instance { get; } = new AvaloniaUIScheduler();

    private AvaloniaUIScheduler() { }

    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <inheritdoc />
    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var cancellation = new CancellationDisposable();
        var subscription = new SerialDisposable { Disposable = cancellation };

        Dispatcher.UIThread.Post(() =>
        {
            if (cancellation.IsDisposed)
            {
                return;
            }

            try
            {
                subscription.Disposable = action(this, state);
            }
            catch
            {
                // Swallow to preserve Rx semantics — exceptions on the scheduler must not tear down the dispatcher.
            }
        });

        return subscription;
    }

    /// <inheritdoc />
    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dueTime <= TimeSpan.Zero)
        {
            return Schedule(state, action);
        }

        var subscription = new SerialDisposable();
        var cts = new CancellationTokenSource();
        subscription.Disposable = new CancellationDisposable(cts);

        _ = DispatcherTimer.RunOnce(
            () =>
            {
                if (!cts.IsCancellationRequested)
                {
                    try
                    {
                        subscription.Disposable = action(this, state);
                    }
                    catch
                    {
                        // Swallow — see note above.
                    }
                }
            },
            dueTime);

        return subscription;
    }

    /// <inheritdoc />
    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        var delay = dueTime - Now;
        return Schedule(state, delay < TimeSpan.Zero ? TimeSpan.Zero : delay, action);
    }
}
