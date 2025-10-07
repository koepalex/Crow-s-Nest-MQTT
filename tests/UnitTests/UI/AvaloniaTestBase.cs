using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Services;
using System.Threading;
using Xunit;

namespace CrowsNestMqtt.UnitTests.UI
{
    public class AvaloniaFixture : IDisposable
    {
        private static int _initialized = 0;

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
        }
    }

    [CollectionDefinition("Avalonia")]
    public class AvaloniaCollection : ICollectionFixture<AvaloniaFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    /// <summary>
    /// Base class for Avalonia UI tests that provides headless testing support
    /// </summary>
    [Collection("Avalonia")]
    public abstract class AvaloniaTestBase
    {
        protected Application Application { get; private set; }

        protected AvaloniaTestBase(AvaloniaFixture fixture)
        {
            Application = Avalonia.Application.Current ?? throw new InvalidOperationException("Application not initialized");
        }

        /// <summary>
        /// Creates a test main view model for testing
        /// </summary>
        protected MainViewModel CreateTestMainViewModel(string? aspireHostname = null, int? aspirePort = null)
        {
            var commandParserService = new CommandParserService();
            // Pass null for the services, as they're not needed for these UI-centric tests
            return new MainViewModel(commandParserService, null, null, null, null, aspireHostname, aspirePort);
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
