using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Services;
using NSubstitute;
using System.Threading;
using Xunit;

namespace CrowsNestMqtt.UnitTests.UI
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public because it is referenced by a protected constructor on the public AvaloniaTestBase, which propagates to test classes across the assembly.")]
    public sealed class AvaloniaFixture : IDisposable
    {
        private static int _initialized;

        public AvaloniaFixture()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 0)
            {
                AppBuilder.Configure(() => new CrowsNestMqtt.UI.App())
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions
                    {
                        UseHeadlessDrawing = true
                    })
                    .SetupWithoutStarting();
                // Ensure SynchronizationContext is set for UI thread access in tests
                SynchronizationContext.SetSynchronizationContext(Avalonia.Threading.AvaloniaSynchronizationContext.Current);
            }
        }

        public void Dispose()
        {
            // No explicit disposal needed for Avalonia
            GC.SuppressFinalize(this);
        }
    }

    [CollectionDefinition("Avalonia")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Suffix 'Collection' matches xUnit's [CollectionDefinition] convention and is intentional infrastructure naming.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "xUnit v3 rule xUnit1027 requires [CollectionDefinition] classes to be public.")]
    public sealed class AvaloniaCollection : ICollectionFixture<AvaloniaFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Base class for Avalonia UI tests that provides headless testing support
    /// </summary>
    [Collection("Avalonia")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public because concrete test classes deriving from this base live in child namespaces across the test assembly and are themselves public. Test infrastructure visibility is intentional.")]
    public abstract class AvaloniaTestBase
    {
        protected Application Application { get; private set; }

        protected AvaloniaTestBase(AvaloniaFixture fixture)
        {
            Application = Avalonia.Application.Current ?? throw new InvalidOperationException("Application not initialized");

            // FluentAvalonia's FAAppWindow registers each native window handle in a private
            // static dictionary (Win32WindowManager._appWindowRegistry) and only removes the
            // entry when the OS sends WM_DESTROY. Under the headless test platform that message
            // never fires, so handles accumulate and constructing a second MainWindow throws
            // "An item with the same key has already been added". Clear the registry before each
            // test so window-creating tests start from a clean slate.
            ClearFluentAvaloniaWindowRegistry();
        }

        /// <summary>
        /// Clears FluentAvalonia's static Win32 window-handle registry via reflection to avoid
        /// duplicate-key collisions when multiple FAAppWindow instances are created in headless tests.
        /// </summary>
        protected static void ClearFluentAvaloniaWindowRegistry()
        {
            var managerType = typeof(FluentAvalonia.UI.Windowing.FAAppWindow).Assembly
                .GetType("FluentAvalonia.UI.Windowing.Win32WindowManager");

            var registryField = managerType?.GetField(
                "_appWindowRegistry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if (registryField?.GetValue(null) is System.Collections.IDictionary registry)
            {
                registry.Clear();
            }
        }

        /// <summary>
        /// Creates a test main view model for testing
        /// </summary>
        protected static MainViewModel CreateTestMainViewModel(EnvironmentSettingsOverrides? environmentOverrides = null)
        {
            var commandParserService = new CommandParserService();
            var mqttServiceMock = Substitute.For<IMqttService>();
            return new MainViewModel(commandParserService, mqttServiceMock, null, null, null, environmentOverrides);
        }

        /// <summary>
        /// Creates a mock command parser service for testing
        /// </summary>
        protected virtual CommandParserService CreateMockCommandParserService()
        {
            return new CommandParserService();
        }
    }
}
