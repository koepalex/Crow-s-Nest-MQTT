using System;
using System.Reactive.Concurrency;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CrowsNestMqtt.UnitTests.TestInfrastructure
{
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
    /// Module initializer runs once per test assembly load, before any tests are executed.
    /// Ensures Avalonia Dispatcher.UIThread uses the ImmediateDispatcher for all tests,
    /// and initializes ReactiveUI with immediate schedulers for deterministic test execution.
    /// </summary>
    internal static class TestDispatcherSetup
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            try
            {
                var dispatcherType = typeof(Dispatcher);
                var field = dispatcherType.GetField("_uiThread", BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(null, new ImmediateDispatcher());
                }
            }
            catch
            {
                // Swallow: tests that rely on dispatcher sync will still fail clearly if this setup breaks.
            }

            // Initialize ReactiveUI for test context (required by ReactiveUI 23.2+)
            try
            {
                var builder = RxAppBuilder.CreateReactiveUIBuilder();
                builder.WithMainThreadScheduler(Scheduler.Immediate);
                builder.WithTaskPoolScheduler(Scheduler.Immediate);
                builder.BuildApp();
            }
            catch
            {
                // Swallow: if already initialized or fails, tests will fail clearly.
            }

            // Ensure schedulers are set to Immediate for deterministic test execution
            RxSchedulers.MainThreadScheduler = Scheduler.Immediate;
            RxSchedulers.TaskpoolScheduler = Scheduler.Immediate;
        }
    }
}
