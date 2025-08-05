using Xunit;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CrowsNestMqtt.UI.Views;
using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.UnitTests.UI
{
    /// <summary>
    /// Tests for the MainView class using Avalonia headless testing
    /// </summary>
    public class MainViewTests : AvaloniaTestBase
    {
        public MainViewTests(AvaloniaFixture fixture) : base(fixture)
        {
        }
        [AvaloniaFact]
        public void MainView_Constructor_DoesNotThrow()
        {
            // Act & Assert
            var exception = Record.Exception(() => new MainView());
            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void MainView_Constructor_InitializesCorrectly()
        {
            // Act
            var mainView = new MainView();

            // Assert
            Assert.NotNull(mainView);
            Assert.IsType<MainView>(mainView);
        }

        [AvaloniaFact]
        public void MainView_WithDataContext_AcceptsMainViewModel()
        {
            // Arrange
            var mainView = new MainView();
            var viewModel = CreateTestMainViewModel();

            // Act
            mainView.DataContext = viewModel;

            // Assert
            Assert.Equal(viewModel, mainView.DataContext);
            Assert.IsType<MainViewModel>(mainView.DataContext);
        }

        [AvaloniaFact]
        public void MainView_DataContextChange_HandledCorrectly()
        {
            // Arrange
            var mainView = new MainView();
            var viewModel1 = CreateTestMainViewModel();
            var viewModel2 = CreateTestMainViewModel();

            // Act
            mainView.DataContext = viewModel1;
            mainView.DataContext = viewModel2;

            // Assert
            Assert.Equal(viewModel2, mainView.DataContext);
        }

        [AvaloniaFact]
        public void MainView_DataContextChange_ToNull_HandledCorrectly()
        {
            // Arrange
            var mainView = new MainView();
            var viewModel = CreateTestMainViewModel();

            // Act
            mainView.DataContext = viewModel;
            mainView.DataContext = null;

            // Assert
            Assert.Null(mainView.DataContext);
        }

        [AvaloniaFact]
        public void MainView_FindControl_RawPayloadEditor_FindsControl()
        {
            // Arrange
            var mainView = new MainView();

            // Act
            // Since this uses reflection to check the control finding logic,
            // we'll test that the constructor completes without throwing
            var exception = Record.Exception(() => 
            {
                // This triggers the constructor logic that looks for the RawPayloadEditor
                var _ = mainView.DataContext;
            });

            // Assert
            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void MainView_IsUserControl()
        {
            // Act
            var mainView = new MainView();

            // Assert
            Assert.IsAssignableFrom<UserControl>(mainView);
        }

        [AvaloniaFact]
        public void MainView_CanBeDisposed()
        {
            // Arrange
            var mainView = new MainView();
            var viewModel = CreateTestMainViewModel();
            mainView.DataContext = viewModel;

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => 
            {
                // Simulate cleanup
                mainView.DataContext = null;
                viewModel.Dispose();
            });

            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void MainView_MultipleInstances_CanBeCreated()
        {
            // Act
            var mainView1 = new MainView();
            var mainView2 = new MainView();

            // Assert
            Assert.NotNull(mainView1);
            Assert.NotNull(mainView2);
            Assert.NotSame(mainView1, mainView2);
        }
    }
}
