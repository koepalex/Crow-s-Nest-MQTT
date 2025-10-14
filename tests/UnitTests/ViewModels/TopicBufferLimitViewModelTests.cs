using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.UI.ViewModels;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public class TopicBufferLimitViewModelTests
{
    [Fact]
    public void Constructor_Default_InitializesWithEmptyValues()
    {
        // Arrange & Act
        var viewModel = new TopicBufferLimitViewModel();

        // Assert
        Assert.Equal("", viewModel.TopicFilter);
        Assert.Equal(0, viewModel.MaxSizeBytes);
        Assert.True(viewModel.CanBeRemoved); // Empty string is not "#", so it can be removed
    }

    [Fact]
    public void Constructor_WithModel_InitializesFromModel()
    {
        // Arrange
        var model = new TopicBufferLimit("sensor/+/temperature", 1048576); // 1MB

        // Act
        var viewModel = new TopicBufferLimitViewModel(model);

        // Assert
        Assert.Equal("sensor/+/temperature", viewModel.TopicFilter);
        Assert.Equal(1048576, viewModel.MaxSizeBytes);
        Assert.True(viewModel.CanBeRemoved);
    }

    [Fact]
    public void TopicFilter_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TopicBufferLimitViewModel.TopicFilter))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.TopicFilter = "test/topic";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("test/topic", viewModel.TopicFilter);
    }

    [Fact]
    public void MaxSizeBytes_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel();
        var propertyChangedRaised = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TopicBufferLimitViewModel.MaxSizeBytes))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.MaxSizeBytes = 2097152; // 2MB

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal(2097152, viewModel.MaxSizeBytes);
    }

    [Fact]
    public void CanBeRemoved_DefaultTopic_ReturnsFalse()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            TopicFilter = "#"
        };

        // Act & Assert
        Assert.False(viewModel.CanBeRemoved);
    }

    [Fact]
    public void CanBeRemoved_CustomTopic_ReturnsTrue()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            TopicFilter = "custom/topic"
        };

        // Act & Assert
        Assert.True(viewModel.CanBeRemoved);
    }

    [Fact]
    public void CanBeRemoved_EmptyTopic_ReturnsTrue()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            TopicFilter = ""
        };

        // Act & Assert
        Assert.True(viewModel.CanBeRemoved);
    }

    [Theory]
    [InlineData("sensor/#", true)]
    [InlineData("sensor/+/temp", true)]
    [InlineData("#", false)]
    [InlineData("device/123", true)]
    [InlineData("", true)]
    public void CanBeRemoved_VariousTopics_ReturnsExpectedResult(string topicFilter, bool expectedCanBeRemoved)
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            TopicFilter = topicFilter
        };

        // Act & Assert
        Assert.Equal(expectedCanBeRemoved, viewModel.CanBeRemoved);
    }

    [Fact]
    public void TopicFilter_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            TopicFilter = "test/topic"
        };

        var propertyChangedCount = 0;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TopicBufferLimitViewModel.TopicFilter))
                propertyChangedCount++;
        };

        // Act
        viewModel.TopicFilter = "test/topic"; // Same value

        // Assert
        Assert.Equal(0, propertyChangedCount);
    }

    [Fact]
    public void MaxSizeBytes_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            MaxSizeBytes = 1024
        };

        var propertyChangedCount = 0;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TopicBufferLimitViewModel.MaxSizeBytes))
                propertyChangedCount++;
        };

        // Act
        viewModel.MaxSizeBytes = 1024; // Same value

        // Assert
        Assert.Equal(0, propertyChangedCount);
    }

    [Fact]
    public void Constructor_WithModelHavingDefaultTopic_SetsCanBeRemovedToFalse()
    {
        // Arrange
        var model = new TopicBufferLimit("#", 10485760); // 10MB

        // Act
        var viewModel = new TopicBufferLimitViewModel(model);

        // Assert
        Assert.False(viewModel.CanBeRemoved);
    }

    [Fact]
    public void MaxSizeBytes_SetNegativeValue_Accepts()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel();

        // Act
        viewModel.MaxSizeBytes = -1; // Negative value (validation would be in business logic)

        // Assert
        Assert.Equal(-1, viewModel.MaxSizeBytes);
    }

    [Fact]
    public void MaxSizeBytes_SetLargeValue_Accepts()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel();

        // Act
        viewModel.MaxSizeBytes = long.MaxValue;

        // Assert
        Assert.Equal(long.MaxValue, viewModel.MaxSizeBytes);
    }

    [Fact]
    public void TopicFilter_SetNull_AcceptsNull()
    {
        // Arrange
        var viewModel = new TopicBufferLimitViewModel
        {
            TopicFilter = "test"
        };

        // Act
        viewModel.TopicFilter = null!;

        // Assert
        Assert.Null(viewModel.TopicFilter);
    }
}
