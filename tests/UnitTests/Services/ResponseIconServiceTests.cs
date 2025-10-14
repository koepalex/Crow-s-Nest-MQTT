using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.UI.Contracts;
using CrowsNestMqtt.UI.Services;
using NSubstitute;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Services;

public class ResponseIconServiceTests
{
    private readonly IMessageCorrelationService _mockCorrelationService;
    private readonly IResponseNavigationService _mockNavigationService;

    public ResponseIconServiceTests()
    {
        _mockCorrelationService = Substitute.For<IMessageCorrelationService>();
        _mockNavigationService = Substitute.For<IResponseNavigationService>();

        // Set up default return value for navigation service
        _mockNavigationService.NavigateToResponseAsync(Arg.Any<string>())
            .Returns(Task.FromResult(new NavigationResult { Success = false, ErrorMessage = "Default error" }));
    }

    [Fact]
    public void Constructor_WithNullCorrelationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ResponseIconService(null!, _mockNavigationService));
    }

    [Fact]
    public void Constructor_WithNullNavigationService_Succeeds()
    {
        // Act
        var service = new ResponseIconService(_mockCorrelationService, null);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithBothServices_Succeeds()
    {
        // Act
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public async Task CreateIconViewModelAsync_WithValidParameters_CreatesViewModel()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";

        // Act
        var result = await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(messageId, result.RequestMessageId);
        Assert.Equal(ResponseStatus.Pending, result.Status);
        Assert.True(result.IsVisible);
    }

    [Fact]
    public async Task CreateIconViewModelAsync_WithEmptyMessageId_ReturnsNull()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.CreateIconViewModelAsync("", hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateIconViewModelAsync_WithNullMessageId_ReturnsNull()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.CreateIconViewModelAsync(null!, hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateIconViewModelAsync_WithNoResponseTopic_ReturnsNull()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.CreateIconViewModelAsync("msg-123", hasResponseTopic: false, isResponseTopicSubscribed: true);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateIconViewModelAsync_WithUnsubscribedResponseTopic_ReturnsNull()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.CreateIconViewModelAsync("msg-123", hasResponseTopic: true, isResponseTopicSubscribed: false);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateIconViewModelAsync_StoresViewModelForRetrieval()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";

        // Act
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);
        var retrieved = await service.GetIconViewModelAsync(messageId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(messageId, retrieved.RequestMessageId);
    }

    [Fact]
    public async Task UpdateIconStatusAsync_WithValidMessageId_UpdatesStatus()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Act
        var result = await service.UpdateIconStatusAsync(messageId, ResponseStatus.Received);

        // Assert
        Assert.True(result);
        var viewModel = await service.GetIconViewModelAsync(messageId);
        Assert.NotNull(viewModel);
        Assert.Equal(ResponseStatus.Received, viewModel.Status);
    }

    [Fact]
    public async Task UpdateIconStatusAsync_WithEmptyMessageId_ReturnsFalse()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.UpdateIconStatusAsync("", ResponseStatus.Received);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateIconStatusAsync_WithNonExistentMessageId_ReturnsFalse()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.UpdateIconStatusAsync("non-existent", ResponseStatus.Received);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateIconStatusAsync_WithSameStatus_ReturnsFalse()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Act - try to update to same status (Pending)
        var result = await service.UpdateIconStatusAsync(messageId, ResponseStatus.Pending);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateIconStatusAsync_RaisesIconStatusChangedEvent()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        IconStatusChangedEventArgs? eventArgs = null;
        service.IconStatusChanged += (sender, args) => eventArgs = args;

        // Act
        await service.UpdateIconStatusAsync(messageId, ResponseStatus.Received);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(messageId, eventArgs.RequestMessageId);
        Assert.Equal(ResponseStatus.Pending, eventArgs.OldStatus);
        Assert.Equal(ResponseStatus.Received, eventArgs.NewStatus);
    }

    [Fact]
    public async Task HandleIconClickAsync_WithEmptyMessageId_ReturnsErrorResult()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.HandleIconClickAsync("");

        // Assert
        Assert.False(result.Handled);
        Assert.False(result.NavigationTriggered);
        Assert.Equal("Invalid request message ID", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleIconClickAsync_WithNonExistentMessageId_ReturnsErrorResult()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.HandleIconClickAsync("non-existent");

        // Assert
        Assert.False(result.Handled);
        Assert.False(result.NavigationTriggered);
        Assert.Equal("Icon view model not found", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleIconClickAsync_RaisesIconClickedEvent()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        IconClickedEventArgs? eventArgs = null;
        service.IconClicked += (sender, args) => eventArgs = args;

        // Act
        await service.HandleIconClickAsync(messageId);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(messageId, eventArgs.RequestMessageId);
    }

    [Fact]
    public async Task HandleIconClickAsync_WithNotClickableIcon_ReturnsHandledWithoutNavigation()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);
        // Update to NavigationDisabled status which is NOT clickable
        await service.UpdateIconStatusAsync(messageId, ResponseStatus.NavigationDisabled);

        // Act
        var result = await service.HandleIconClickAsync(messageId);

        // Assert
        Assert.True(result.Handled);
        Assert.False(result.NavigationTriggered);
        Assert.Equal("Navigation is disabled for this message", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleIconClickAsync_WithClickableIconAndNoNavigationService_ReturnsCommand()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, null); // No navigation service
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);
        await service.UpdateIconStatusAsync(messageId, ResponseStatus.Received); // Make it clickable

        // Act
        var result = await service.HandleIconClickAsync(messageId);

        // Assert
        Assert.True(result.Handled);
        Assert.False(result.NavigationTriggered);
        Assert.Equal("Navigation service not available", result.ErrorMessage);
        Assert.Equal($":gotoresponse {messageId}", result.NavigationCommand);
    }

    [Fact]
    public async Task HandleIconClickAsync_WithClickableIconAndSuccessfulNavigation_ReturnsSuccess()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);
        await service.UpdateIconStatusAsync(messageId, ResponseStatus.Received); // Make it clickable

        _mockNavigationService.NavigateToResponseAsync(messageId)
            .Returns(Task.FromResult(new NavigationResult { Success = true }));

        // Act
        var result = await service.HandleIconClickAsync(messageId);

        // Assert
        Assert.True(result.Handled);
        Assert.True(result.NavigationTriggered);
        Assert.Null(result.ErrorMessage);
        Assert.Equal($":gotoresponse {messageId}", result.NavigationCommand);
    }

    [Fact]
    public async Task HandleIconClickAsync_WithClickableIconAndFailedNavigation_ReturnsError()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);
        await service.UpdateIconStatusAsync(messageId, ResponseStatus.Received); // Make it clickable

        // Override the default mock return value with specific error
        _mockNavigationService.NavigateToResponseAsync(messageId)
            .Returns(Task.FromResult(new NavigationResult
            {
                Success = false,
                ErrorMessage = "Response not found"
            }));

        // Act
        var result = await service.HandleIconClickAsync(messageId);

        // Assert
        Assert.True(result.Handled);
        Assert.False(result.NavigationTriggered);
        Assert.Equal("Response not found", result.ErrorMessage);
        Assert.Null(result.NavigationCommand);
    }

    [Fact]
    public async Task GetIconViewModelAsync_WithEmptyMessageId_ReturnsNull()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.GetIconViewModelAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetIconViewModelAsync_WithNonExistentMessageId_ReturnsNull()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.GetIconViewModelAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveIconAsync_WithValidMessageId_RemovesIcon()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Act
        var result = await service.RemoveIconAsync(messageId);

        // Assert
        Assert.True(result);
        var retrieved = await service.GetIconViewModelAsync(messageId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task RemoveIconAsync_WithEmptyMessageId_ReturnsFalse()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.RemoveIconAsync("");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveIconAsync_WithNonExistentMessageId_ReturnsFalse()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.RemoveIconAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetIconConfigurationAsync_ReturnsDefaultConfiguration()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var config = await service.GetIconConfigurationAsync();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("avares://CrowsNestMqtt/Assets/Icons/clock.svg", config.ClockIconPath);
        Assert.Equal("avares://CrowsNestMqtt/Assets/Icons/arrow.svg", config.ArrowIconPath);
        Assert.Equal(16.0, config.IconSize);
    }

    [Fact]
    public async Task UpdateIconConfigurationAsync_WithValidConfiguration_UpdatesConfiguration()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var newConfig = new IconConfiguration
        {
            ClockIconPath = "/custom/clock.svg",
            ArrowIconPath = "/custom/arrow.svg",
            IconSize = 24.0
        };

        // Act
        var result = await service.UpdateIconConfigurationAsync(newConfig);

        // Assert
        Assert.True(result);
        var config = await service.GetIconConfigurationAsync();
        Assert.Equal("/custom/clock.svg", config.ClockIconPath);
        Assert.Equal(24.0, config.IconSize);
    }

    [Fact]
    public async Task UpdateIconConfigurationAsync_WithNullConfiguration_ReturnsFalse()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act
        var result = await service.UpdateIconConfigurationAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateIconConfigurationAsync_UpdatesExistingIconViewModels()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        var originalViewModel = await service.GetIconViewModelAsync(messageId);
        var originalIconPath = originalViewModel!.IconPath;

        var newConfig = new IconConfiguration
        {
            ClockIconPath = "/new/clock.svg",
            ArrowIconPath = "/new/arrow.svg"
        };

        // Act
        await service.UpdateIconConfigurationAsync(newConfig);

        // Assert
        var updatedViewModel = await service.GetIconViewModelAsync(messageId);
        Assert.Equal("/new/clock.svg", updatedViewModel!.IconPath); // Should be updated from config
    }

    [Fact]
    public async Task MultipleIcons_CanBeCreatedAndManagedIndependently()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);

        // Act - create multiple icons
        await service.CreateIconViewModelAsync("msg-1", hasResponseTopic: true, isResponseTopicSubscribed: true);
        await service.CreateIconViewModelAsync("msg-2", hasResponseTopic: true, isResponseTopicSubscribed: true);
        await service.CreateIconViewModelAsync("msg-3", hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Update one
        await service.UpdateIconStatusAsync("msg-2", ResponseStatus.Received);

        // Remove one
        await service.RemoveIconAsync("msg-3");

        // Assert
        var icon1 = await service.GetIconViewModelAsync("msg-1");
        var icon2 = await service.GetIconViewModelAsync("msg-2");
        var icon3 = await service.GetIconViewModelAsync("msg-3");

        Assert.NotNull(icon1);
        Assert.Equal(ResponseStatus.Pending, icon1.Status);

        Assert.NotNull(icon2);
        Assert.Equal(ResponseStatus.Received, icon2.Status);

        Assert.Null(icon3);
    }

    [Fact]
    public async Task StatusTransitions_UpdateIconPathAndProperties()
    {
        // Arrange
        var service = new ResponseIconService(_mockCorrelationService, _mockNavigationService);
        var messageId = "msg-123";
        await service.CreateIconViewModelAsync(messageId, hasResponseTopic: true, isResponseTopicSubscribed: true);

        // Act - transition from Pending to Received
        await service.UpdateIconStatusAsync(messageId, ResponseStatus.Received);
        var viewModel = await service.GetIconViewModelAsync(messageId);

        // Assert
        Assert.NotNull(viewModel);
        Assert.Equal(ResponseStatus.Received, viewModel.Status);
        Assert.Contains("arrow.svg", viewModel.IconPath); // Should use arrow icon for Received status
        Assert.True(viewModel.IsClickable); // Received status should be clickable
    }
}
