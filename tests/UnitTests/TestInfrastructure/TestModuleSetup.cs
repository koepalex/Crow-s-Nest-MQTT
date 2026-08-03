using System;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI.Builder;
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
/// Bootstraps ReactiveUI 23 (bundled with ReactiveUI.Avalonia 12.0.3) so that plain
/// <c>[Fact]</c> tests that use <see cref="ReactiveUI.ReactiveObject"/> or <c>WhenAnyValue</c>
/// can hydrate their static type initialisers without hitting
/// <c>"ReactiveUI has not been initialized"</c>, and installs immediate schedulers so plain
/// <c>[Fact]</c> tests can assert synchronously right after mutating reactive state.
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

        // Initialize ReactiveUI for test context (required by ReactiveUI 24 / RxAppBuilder pattern)
        try
        {
            RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        }
        catch
        {
            // Swallow: if already initialised or fails, tests will fail clearly.
        }

        // ReactiveUI 24 removed the setter for RxSchedulers.MainThreadScheduler / TaskpoolScheduler
        // (they now return ISequencer instead of IScheduler and are read-only). Tests that need
        // deterministic scheduling inject Scheduler.Immediate through the ViewModel constructors
        // instead.
    }
}
