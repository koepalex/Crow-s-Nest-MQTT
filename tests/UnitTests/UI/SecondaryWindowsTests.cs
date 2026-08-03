using Avalonia.Controls;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Views;
using Xunit;
using Avalonia.Headless.XUnit;

namespace CrowsNestMqtt.UnitTests.UI
{
    /// <summary>
    /// Headless smoke tests for the secondary windows (publish + stats). These load the
    /// XAML, which catches broken resource lookups, invalid enum glyph names and bad styles.
    /// </summary>
    public class SecondaryWindowsTests : AvaloniaTestBase
    {
        [AvaloniaFact]
        public void PublishWindow_Constructor_DoesNotThrow()
        {
            var exception = Record.Exception(() => new PublishWindow());
            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void PublishWindow_WithViewModel_LoadsAndIsWindow()
        {
            using var viewModel = new PublishViewModel();
            var window = new PublishWindow { DataContext = viewModel };

            Assert.IsAssignableFrom<Window>(window);
            Assert.Same(viewModel, window.DataContext);
            Assert.False(string.IsNullOrEmpty(window.Title));
        }

        [AvaloniaFact]
        public void StatsWindow_Constructor_DoesNotThrow()
        {
            var exception = Record.Exception(() => new StatsWindow());
            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void StatsWindow_ContainsStatsGrid()
        {
            var window = new StatsWindow();

            Assert.NotNull(window.FindControl<DataGrid>("StatsGrid"));
        }
    }
}
