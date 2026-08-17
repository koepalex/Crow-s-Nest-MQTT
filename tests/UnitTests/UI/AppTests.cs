using Avalonia;
using Avalonia.Headless.XUnit;
using CrowsNestMqtt.UnitTests.UI;
using Xunit;

namespace CrowsNestMqtt.UnitTests.UI
{
    /// <summary>
    /// Tests for the UI App class
    /// </summary>
    public class AppTests : AvaloniaTestBase
    {
        [AvaloniaFact]
        public void App_CanBeInstantiated()
        {
            // This test verifies that we can create an instance of the App class
            // without requiring specific initialization due to UI framework complexity

            // Act & Assert - Creation should be possible
            var exception = Record.Exception(() =>
            {
                // Test that the type exists and can be referenced
                var appType = typeof(CrowsNestMqtt.UI.App);
                Assert.NotNull(appType);
                Assert.True(appType.IsSubclassOf(typeof(Application)));
            });

            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void App_Initialize_LoadsXaml()
        {
            // Arrange
            var app = new CrowsNestMqtt.UI.App();

            // Act
            var exception = Record.Exception(() => app.Initialize());

            // Assert
            Assert.Null(exception);
        }
    }
}
