using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.UI.ViewModels;
using System;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public class ResponseIconViewModelTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Arrange & Act
        var viewModel = new ResponseIconViewModel();

        // Assert
        Assert.Equal(string.Empty, viewModel.RequestMessageId);
        Assert.Equal(ResponseStatus.Hidden, viewModel.Status); // Default status
        Assert.Equal(string.Empty, viewModel.IconPath);
        Assert.Equal(string.Empty, viewModel.ToolTip);
        Assert.False(viewModel.IsClickable);
        Assert.True(viewModel.IsVisible);
        Assert.False(viewModel.IsResponseReceived);
    }

    [Fact]
    public void Status_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();
        var propertyChangedCount = 0;
        var isResponseReceivedChangedCount = 0;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResponseIconViewModel.Status))
                propertyChangedCount++;
            if (e.PropertyName == nameof(ResponseIconViewModel.IsResponseReceived))
                isResponseReceivedChangedCount++;
        };

        // Act
        viewModel.Status = ResponseStatus.Received;

        // Assert
        Assert.Equal(1, propertyChangedCount);
        Assert.Equal(1, isResponseReceivedChangedCount);
    }

    [Theory]
    [InlineData(ResponseStatus.Pending, false)]
    [InlineData(ResponseStatus.Received, true)]
    [InlineData(ResponseStatus.NavigationDisabled, false)]
    [InlineData(ResponseStatus.Hidden, false)]
    public void IsResponseReceived_ReturnsCorrectValue(ResponseStatus status, bool expectedResult)
    {
        // Arrange
        var viewModel = new ResponseIconViewModel { Status = status };

        // Act & Assert
        Assert.Equal(expectedResult, viewModel.IsResponseReceived);
    }

    [Fact]
    public void IconPath_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();
        var propertyChangedRaised = false;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResponseIconViewModel.IconPath))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IconPath = "/icons/clock.svg";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("/icons/clock.svg", viewModel.IconPath);
    }

    [Fact]
    public void ToolTip_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();
        var propertyChangedRaised = false;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResponseIconViewModel.ToolTip))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.ToolTip = "Waiting for response";

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.Equal("Waiting for response", viewModel.ToolTip);
    }

    [Fact]
    public void IsClickable_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();
        var propertyChangedRaised = false;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResponseIconViewModel.IsClickable))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsClickable = true;

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.True(viewModel.IsClickable);
    }

    [Fact]
    public void IsVisible_SetValue_RaisesPropertyChanged()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();
        var propertyChangedRaised = false;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResponseIconViewModel.IsVisible))
                propertyChangedRaised = true;
        };

        // Act
        viewModel.IsVisible = false;

        // Assert
        Assert.True(propertyChangedRaised);
        Assert.False(viewModel.IsVisible);
    }

    [Fact]
    public void RequestMessageId_CanBeInitialized()
    {
        // Arrange & Act
        var viewModel = new ResponseIconViewModel
        {
            RequestMessageId = "test-message-123"
        };

        // Assert
        Assert.Equal("test-message-123", viewModel.RequestMessageId);
    }

    [Fact]
    public void NavigationCommand_CanBeSetAndGet()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();

        // Act
        viewModel.NavigationCommand = ":gotoresponse test-id";

        // Assert
        Assert.Equal(":gotoresponse test-id", viewModel.NavigationCommand);
    }

    [Fact]
    public void LastUpdated_CanBeSetAndGet()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel();
        var timestamp = DateTime.UtcNow.AddMinutes(-5);

        // Act
        viewModel.LastUpdated = timestamp;

        // Assert
        Assert.Equal(timestamp, viewModel.LastUpdated);
    }

    [Fact]
    public void Status_SetSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel { Status = ResponseStatus.Pending };
        var propertyChangedCount = 0;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ResponseIconViewModel.Status))
                propertyChangedCount++;
        };

        // Act
        viewModel.Status = ResponseStatus.Pending; // Same value

        // Assert
        Assert.Equal(0, propertyChangedCount);
    }

    [Fact]
    public void FullWorkflow_PendingToReceived_UpdatesAllProperties()
    {
        // Arrange
        var viewModel = new ResponseIconViewModel
        {
            RequestMessageId = "req-123",
            Status = ResponseStatus.Pending,
            IconPath = "/icons/clock.svg",
            ToolTip = "Waiting...",
            IsClickable = false,
            IsVisible = true
        };

        // Act - Simulate receiving a response
        viewModel.Status = ResponseStatus.Received;
        viewModel.IconPath = "/icons/arrow.svg";
        viewModel.ToolTip = "Response received - Click to view";
        viewModel.IsClickable = true;
        viewModel.NavigationCommand = ":gotoresponse req-123";
        viewModel.LastUpdated = DateTime.UtcNow;

        // Assert
        Assert.Equal(ResponseStatus.Received, viewModel.Status);
        Assert.True(viewModel.IsResponseReceived);
        Assert.Equal("/icons/arrow.svg", viewModel.IconPath);
        Assert.Equal("Response received - Click to view", viewModel.ToolTip);
        Assert.True(viewModel.IsClickable);
        Assert.True(viewModel.IsVisible);
        Assert.Equal(":gotoresponse req-123", viewModel.NavigationCommand);
    }
}
