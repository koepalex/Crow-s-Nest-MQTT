using Xunit;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CrowsNestMqtt.UI.Views;
using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.UnitTests.UI
{
    /// <summary>
    /// Tests for the MainWindow class using Avalonia headless testing
    /// </summary>
    public class MainWindowTests : AvaloniaTestBase
    {
        public MainWindowTests(AvaloniaFixture fixture) : base(fixture)
        {
        }
        [AvaloniaFact]
        public void MainWindow_Constructor_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => new MainWindow());
            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void MainWindow_Constructor_InitializesCorrectly()
        {
            // Act
            var mainWindow = new MainWindow();

            // Assert
            Assert.NotNull(mainWindow);
            Assert.IsType<MainWindow>(mainWindow);
        }

        [AvaloniaFact]
        public void MainWindow_IsWindow()
        {
            // Act
            var mainWindow = new MainWindow();

            // Assert
            Assert.IsAssignableFrom<Window>(mainWindow);
        }

        [AvaloniaFact]
        public void MainWindow_WithDataContext_AcceptsMainViewModel()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var viewModel = CreateTestMainViewModel();

            // Act
            mainWindow.DataContext = viewModel;

            // Assert
            Assert.Equal(viewModel, mainWindow.DataContext);
            Assert.IsType<MainViewModel>(mainWindow.DataContext);
        }

        [AvaloniaFact]
        public void MainWindow_DataContextChange_HandledCorrectly()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var viewModel1 = CreateTestMainViewModel();
            var viewModel2 = CreateTestMainViewModel();

            // Act
            mainWindow.DataContext = viewModel1;
            mainWindow.DataContext = viewModel2;

            // Assert
            Assert.Equal(viewModel2, mainWindow.DataContext);
        }

        [AvaloniaFact]
        public void MainWindow_DataContextChange_ToNull_HandledCorrectly()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var viewModel = CreateTestMainViewModel();

            // Act
            mainWindow.DataContext = viewModel;
            mainWindow.DataContext = null;

            // Assert
            Assert.Null(mainWindow.DataContext);
        }

        [AvaloniaFact]
        public void MainWindow_HasTitle()
        {
            // Act
            var mainWindow = new MainWindow();

            // Assert
            // The title should be set in XAML, so let's verify it's not null or empty
            Assert.False(string.IsNullOrEmpty(mainWindow.Title));
        }

        [AvaloniaFact]
        public void MainWindow_CanBeDisposed()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var viewModel = CreateTestMainViewModel();
            mainWindow.DataContext = viewModel;

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => 
            {
                // Simulate cleanup
                mainWindow.DataContext = null;
                viewModel.Dispose();
                mainWindow.Close();
            });

            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void MainWindow_MultipleInstances_CanBeCreated()
        {
            // Act
            var mainWindow1 = new MainWindow();
            var mainWindow2 = new MainWindow();

            // Assert
            Assert.NotNull(mainWindow1);
            Assert.NotNull(mainWindow2);
            Assert.NotSame(mainWindow1, mainWindow2);
        }

        [AvaloniaFact]
        public void MainWindow_CanSetWidth()
        {
            // Arrange
            var mainWindow = new MainWindow();
            const double testWidth = 800;

            // Act
            mainWindow.Width = testWidth;

            // Assert
            Assert.Equal(testWidth, mainWindow.Width);
        }

        [AvaloniaFact]
        public void MainWindow_CanSetHeight()
        {
            // Arrange
            var mainWindow = new MainWindow();
            const double testHeight = 600;

            // Act
            mainWindow.Height = testHeight;

            // Assert
            Assert.Equal(testHeight, mainWindow.Height);
        }
    }
}
