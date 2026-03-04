using Xunit;
using Avalonia.Controls;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Views;
using ReactiveUI;
using ViewLocator = CrowsNestMqtt.UI.ViewLocator;

namespace CrowsNestMqtt.UnitTests.UI
{
    /// <summary>
    /// Tests for the ViewLocator class
    /// </summary>
    public class ViewLocatorTests : AvaloniaTestBase
    {
        public ViewLocatorTests(AvaloniaFixture fixture) : base(fixture)
        {
        }
        [Fact]
        public void ViewLocator_Build_WithNull_ReturnsNull()
        {
            // Arrange
            var viewLocator = new ViewLocator();

            // Act
            var result = viewLocator.Build(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ViewLocator_Build_WithValidViewModel_ReturnsMainWindow()
        {
            // Arrange
            var viewLocator = new ViewLocator();
            using var viewModel = CreateTestMainViewModel();

            // Act
            var result = viewLocator.Build(viewModel);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<MainWindow>(result);
            
            var mainWindow = result as MainWindow;
            Assert.NotNull(mainWindow?.DataContext);
            Assert.IsType<MainViewModel>(mainWindow.DataContext);
        }

        [Fact]
        public void ViewLocator_Build_WithNonReactiveObject_ReturnsMainWindow()
        {
            // Arrange
            var viewLocator = new ViewLocator();
            var nonReactiveObject = new object();

            // Act
            var result = viewLocator.Build(nonReactiveObject);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<MainWindow>(result);
        }

        [Fact]
        public void ViewLocator_Match_WithReactiveObject_ReturnsTrue()
        {
            // Arrange
            var viewLocator = new ViewLocator();
            using var reactiveObject = CreateTestMainViewModel();

            // Act
            var result = viewLocator.Match(reactiveObject);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ViewLocator_Match_WithNonReactiveObject_ReturnsFalse()
        {
            // Arrange
            var viewLocator = new ViewLocator();
            var nonReactiveObject = new object();

            // Act
            var result = viewLocator.Match(nonReactiveObject);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ViewLocator_Match_WithNull_ReturnsFalse()
        {
            // Arrange
            var viewLocator = new ViewLocator();

            // Act
            var result = viewLocator.Match(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ViewLocator_Build_CreatesNewMainWindowInstance()
        {
            // Arrange
            var viewLocator = new ViewLocator();
            using var viewModel = CreateTestMainViewModel();

            // Act
            var result1 = viewLocator.Build(viewModel);
            var result2 = viewLocator.Build(viewModel);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotSame(result1, result2); // Should create new instances
        }
    }
}
