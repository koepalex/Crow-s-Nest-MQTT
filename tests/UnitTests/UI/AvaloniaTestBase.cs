using Avalonia;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Services;
using NSubstitute;

namespace CrowsNestMqtt.UnitTests.UI
{
    /// <summary>
    /// Base class for Avalonia UI tests. The Avalonia headless runtime and its per-test dispatcher
    /// are wired up by <c>Avalonia.Headless.XUnit</c> (<see cref="TestInfrastructure.TestAppBuilder"/>);
    /// this base only carries reusable helpers.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public because concrete test classes deriving from this base live in child namespaces across the test assembly and are themselves public. Test infrastructure visibility is intentional.")]
    public abstract class AvaloniaTestBase
    {
        protected AvaloniaTestBase()
        {
            // Clear FluentAvalonia's static window-handle registry before every test method.
            // Under headless testing the OS never sends WM_DESTROY, so entries accumulate and
            // constructing a second MainWindow throws "An item with the same key has already
            // been added". Because xUnit constructs a fresh test-class instance per test, this
            // constructor runs before every window-constructing test.
            ClearFluentAvaloniaWindowRegistry();
        }

        protected static Application Application => Avalonia.Application.Current ?? throw new InvalidOperationException("Application not initialized. Did the test forget to use [AvaloniaFact] / [AvaloniaTheory]?");

        /// <summary>
        /// Clears FluentAvalonia's static Win32 window-handle registry via reflection to avoid
        /// duplicate-key collisions when multiple FAAppWindow instances are created in headless tests.
        /// FluentAvalonia's <c>FAAppWindow</c> registers each native window handle in a private
        /// static dictionary and only removes the entry when the OS sends <c>WM_DESTROY</c>. Under
        /// the headless test platform that message never fires, so handles accumulate. Call this
        /// from window-constructing test methods (or at the top of the test class's Dispose) to
        /// avoid "An item with the same key has already been added" on the second and later windows.
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
