using System;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CrowsNestMqtt.UnitTests.TestInfrastructure;

/// <summary>
/// Provides a synchronous dispatcher implementation to make UI-thread dependent
/// logic deterministic inside unit tests (avoids timing issues when asserting
/// immediately after setting properties like SelectedMessage).
/// </summary>
internal sealed class ImmediateDispatcher : IDispatcher
{
    public bool CheckAccess() => true;
    public static void Post(Action action) => action();
    public void Post(Action action, DispatcherPriority priority) => action();
    public void VerifyAccess() { }
    public static DispatcherPriority Priority => DispatcherPriority.Normal;
}

/// <summary>
/// Module initializer runs once per test-assembly load, before any tests are executed.
/// Configures ReactiveUI so that plain
/// <c>[Fact]</c> tests that use <see cref="ReactiveUI.ReactiveObject"/> or <c>WhenAnyValue</c>
/// use immediate schedulers, allowing plain <c>[Fact]</c> tests to assert synchronously
/// right after mutating reactive state.
/// Avalonia headless dispatcher wiring for the UI test classes is handled by
/// <c>Avalonia.Headless.XUnit</c>'s <c>[AvaloniaFact]</c> runner (see <see cref="TestAppBuilder"/>).
/// </summary>
internal static class TestModuleSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            var dispatcherType = typeof(Dispatcher);
            var field = dispatcherType.GetField("_uiThread", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, new ImmediateDispatcher());
        }
        catch
        {
            // Swallow: tests that rely on dispatcher sync will still fail clearly if this setup breaks.
        }

        RxApp.MainThreadScheduler = Scheduler.Immediate;
        RxApp.TaskpoolScheduler = Scheduler.Immediate;
    }
}
