using Xunit;
using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.UnitTests.ViewModels
{
    /// <summary>
    /// Tests for the TopicViewModel class
    /// </summary>
    public class TopicViewModelTests
    {
        [Fact]
        public void TopicViewModel_Constructor_InitializesCorrectly()
        {
            // Act
            var viewModel = new TopicViewModel();

            // Assert
            Assert.NotNull(viewModel);
            Assert.Equal(string.Empty, viewModel.Name);
            Assert.Equal(0, viewModel.MessageCount);
        }

        [Fact]
        public void TopicViewModel_Name_CanBeSet()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            const string testName = "test/topic";

            // Act
            viewModel.Name = testName;

            // Assert
            Assert.Equal(testName, viewModel.Name);
        }

        [Fact]
        public void TopicViewModel_Name_CanBeSetToNull()
        {
            // Arrange
            var viewModel = new TopicViewModel();

            // Act
            viewModel.Name = null!;

            // Assert
            Assert.Null(viewModel.Name);
        }

        [Fact]
        public void TopicViewModel_Name_CanBeSetToEmpty()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            viewModel.Name = "test";

            // Act
            viewModel.Name = string.Empty;

            // Assert
            Assert.Equal(string.Empty, viewModel.Name);
        }

        [Fact]
        public void TopicViewModel_MessageCount_CanBeSet()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            const int testCount = 42;

            // Act
            viewModel.MessageCount = testCount;

            // Assert
            Assert.Equal(testCount, viewModel.MessageCount);
        }

        [Fact]
        public void TopicViewModel_MessageCount_CanBeSetToZero()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            viewModel.MessageCount = 10;

            // Act
            viewModel.MessageCount = 0;

            // Assert
            Assert.Equal(0, viewModel.MessageCount);
        }

        [Fact]
        public void TopicViewModel_MessageCount_CanBeSetToNegative()
        {
            // Arrange
            var viewModel = new TopicViewModel();

            // Act
            viewModel.MessageCount = -1;

            // Assert
            Assert.Equal(-1, viewModel.MessageCount);
        }

        [Fact]
        public void TopicViewModel_ToString_ReturnsName()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            const string testName = "sensor/temperature";

            // Act
            viewModel.Name = testName;
            var result = viewModel.ToString();

            // Assert
            Assert.Equal(testName, result);
        }

        [Fact]
        public void TopicViewModel_ToString_ReturnsEmptyStringWhenNameEmpty()
        {
            // Arrange
            var viewModel = new TopicViewModel();

            // Act
            var result = viewModel.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void TopicViewModel_ToString_ReturnsNullWhenNameNull()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            viewModel.Name = null!;

            // Act
            var result = viewModel.ToString();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TopicViewModel_Name_PropertyChangedFired()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            var propertyChangedFired = false;
            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TopicViewModel.Name))
                    propertyChangedFired = true;
            };

            // Act
            viewModel.Name = "test/topic";

            // Assert
            Assert.True(propertyChangedFired);
        }

        [Fact]
        public void TopicViewModel_MessageCount_PropertyChangedFired()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            var propertyChangedFired = false;
            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TopicViewModel.MessageCount))
                    propertyChangedFired = true;
            };

            // Act
            viewModel.MessageCount = 5;

            // Assert
            Assert.True(propertyChangedFired);
        }

        [Fact]
        public void TopicViewModel_SetSameValue_DoesNotFirePropertyChanged()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            viewModel.Name = "test";
            var propertyChangedFired = false;
            viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(TopicViewModel.Name))
                    propertyChangedFired = true;
            };

            // Act
            viewModel.Name = "test"; // Same value

            // Assert
            Assert.False(propertyChangedFired);
        }

        [Fact]
        public void TopicViewModel_MultipleProperties_CanBeSetIndependently()
        {
            // Arrange
            var viewModel = new TopicViewModel();
            const string testName = "sensor/humidity";
            const int testCount = 100;

            // Act
            viewModel.Name = testName;
            viewModel.MessageCount = testCount;

            // Assert
            Assert.Equal(testName, viewModel.Name);
            Assert.Equal(testCount, viewModel.MessageCount);
        }
    }
}
