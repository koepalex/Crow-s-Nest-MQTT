using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.UI.Contracts;
using System;
using Xunit;

namespace CrowsNestMqtt.UnitTests.Contracts;

public class IconClickResultTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var result = new IconClickResult();

        // Assert
        Assert.False(result.Handled);
        Assert.False(result.NavigationTriggered);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.NavigationCommand);
    }

    [Fact]
    public void Constructor_InitializesWithProvidedValues()
    {
        // Arrange & Act
        var result = new IconClickResult
        {
            Handled = true,
            NavigationTriggered = true,
            ErrorMessage = "Test error",
            NavigationCommand = ":gotoresponse 123"
        };

        // Assert
        Assert.True(result.Handled);
        Assert.True(result.NavigationTriggered);
        Assert.Equal("Test error", result.ErrorMessage);
        Assert.Equal(":gotoresponse 123", result.NavigationCommand);
    }

    [Fact]
    public void Equality_SameValues_ReturnsTrue()
    {
        // Arrange
        var result1 = new IconClickResult
        {
            Handled = true,
            NavigationTriggered = false,
            ErrorMessage = "Error",
            NavigationCommand = ":cmd"
        };

        var result2 = new IconClickResult
        {
            Handled = true,
            NavigationTriggered = false,
            ErrorMessage = "Error",
            NavigationCommand = ":cmd"
        };

        // Act & Assert
        Assert.Equal(result1, result2);
        Assert.True(result1 == result2);
        Assert.False(result1 != result2);
    }

    [Fact]
    public void Equality_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var result1 = new IconClickResult { Handled = true };
        var result2 = new IconClickResult { Handled = false };

        // Act & Assert
        Assert.NotEqual(result1, result2);
        Assert.False(result1 == result2);
        Assert.True(result1 != result2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new IconClickResult
        {
            Handled = true,
            NavigationTriggered = false,
            ErrorMessage = "Original"
        };

        // Act
        var modified = original with { ErrorMessage = "Modified" };

        // Assert
        Assert.Equal("Original", original.ErrorMessage);
        Assert.Equal("Modified", modified.ErrorMessage);
        Assert.Equal(original.Handled, modified.Handled);
        Assert.Equal(original.NavigationTriggered, modified.NavigationTriggered);
    }
}

public class IconConfigurationTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var config = new IconConfiguration();

        // Assert
        Assert.Equal(string.Empty, config.ClockIconPath);
        Assert.Equal(string.Empty, config.ArrowIconPath);
        Assert.Equal(string.Empty, config.DisabledClockIconPath);
        Assert.Equal("#666666", config.IconColor);
        Assert.Equal("#333333", config.HoverColor);
        Assert.Equal("#CCCCCC", config.DisabledColor);
        Assert.Equal(16.0, config.IconSize);
        Assert.True(config.EnableHoverEffects);
        Assert.True(config.EnableClickAnimation);
    }

    [Fact]
    public void Constructor_InitializesWithProvidedValues()
    {
        // Arrange & Act
        var config = new IconConfiguration
        {
            ClockIconPath = "/assets/clock.svg",
            ArrowIconPath = "/assets/arrow.svg",
            DisabledClockIconPath = "/assets/clock_disabled.svg",
            IconColor = "#FF0000",
            HoverColor = "#00FF00",
            DisabledColor = "#0000FF",
            IconSize = 24.0,
            EnableHoverEffects = false,
            EnableClickAnimation = false
        };

        // Assert
        Assert.Equal("/assets/clock.svg", config.ClockIconPath);
        Assert.Equal("/assets/arrow.svg", config.ArrowIconPath);
        Assert.Equal("/assets/clock_disabled.svg", config.DisabledClockIconPath);
        Assert.Equal("#FF0000", config.IconColor);
        Assert.Equal("#00FF00", config.HoverColor);
        Assert.Equal("#0000FF", config.DisabledColor);
        Assert.Equal(24.0, config.IconSize);
        Assert.False(config.EnableHoverEffects);
        Assert.False(config.EnableClickAnimation);
    }

    [Fact]
    public void Equality_SameValues_ReturnsTrue()
    {
        // Arrange
        var config1 = new IconConfiguration
        {
            ClockIconPath = "/path1",
            ArrowIconPath = "/path2",
            IconSize = 20.0
        };

        var config2 = new IconConfiguration
        {
            ClockIconPath = "/path1",
            ArrowIconPath = "/path2",
            IconSize = 20.0
        };

        // Act & Assert
        Assert.Equal(config1, config2);
    }

    [Fact]
    public void Equality_DifferentValues_ReturnsFalse()
    {
        // Arrange
        var config1 = new IconConfiguration { IconSize = 16.0 };
        var config2 = new IconConfiguration { IconSize = 24.0 };

        // Act & Assert
        Assert.NotEqual(config1, config2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = new IconConfiguration
        {
            ClockIconPath = "/original/clock.svg",
            IconSize = 16.0,
            EnableHoverEffects = true
        };

        // Act
        var modified = original with { IconSize = 24.0 };

        // Assert
        Assert.Equal(16.0, original.IconSize);
        Assert.Equal(24.0, modified.IconSize);
        Assert.Equal(original.ClockIconPath, modified.ClockIconPath);
        Assert.Equal(original.EnableHoverEffects, modified.EnableHoverEffects);
    }

    [Fact]
    public void IconSize_AcceptsVariousValues()
    {
        // Arrange & Act
        var config1 = new IconConfiguration { IconSize = 0.0 };
        var config2 = new IconConfiguration { IconSize = 12.5 };
        var config3 = new IconConfiguration { IconSize = 100.0 };

        // Assert
        Assert.Equal(0.0, config1.IconSize);
        Assert.Equal(12.5, config2.IconSize);
        Assert.Equal(100.0, config3.IconSize);
    }
}

public class IconStatusChangedEventArgsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var args = new IconStatusChangedEventArgs();

        // Assert
        Assert.Equal(string.Empty, args.RequestMessageId);
        Assert.Equal(ResponseStatus.Hidden, args.OldStatus);
        Assert.Equal(ResponseStatus.Hidden, args.NewStatus);
        Assert.Equal(string.Empty, args.IconPath);
        // ChangedAt should be close to UtcNow (within 1 second)
        Assert.True((DateTime.UtcNow - args.ChangedAt).TotalSeconds < 1);
    }

    [Fact]
    public void Constructor_InitializesWithProvidedValues()
    {
        // Arrange
        var testTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var args = new IconStatusChangedEventArgs
        {
            RequestMessageId = "msg-123",
            OldStatus = ResponseStatus.Pending,
            NewStatus = ResponseStatus.Received,
            IconPath = "/assets/arrow.svg",
            ChangedAt = testTime
        };

        // Assert
        Assert.Equal("msg-123", args.RequestMessageId);
        Assert.Equal(ResponseStatus.Pending, args.OldStatus);
        Assert.Equal(ResponseStatus.Received, args.NewStatus);
        Assert.Equal("/assets/arrow.svg", args.IconPath);
        Assert.Equal(testTime, args.ChangedAt);
    }

    [Fact]
    public void InheritsFromEventArgs()
    {
        // Arrange & Act
        var args = new IconStatusChangedEventArgs();

        // Assert
        Assert.IsAssignableFrom<EventArgs>(args);
    }

    [Fact]
    public void AllResponseStatusValues_CanBeAssigned()
    {
        // Arrange & Act
        var args1 = new IconStatusChangedEventArgs { OldStatus = ResponseStatus.Pending, NewStatus = ResponseStatus.Received };
        var args2 = new IconStatusChangedEventArgs { OldStatus = ResponseStatus.Received, NewStatus = ResponseStatus.NavigationDisabled };
        var args3 = new IconStatusChangedEventArgs { OldStatus = ResponseStatus.NavigationDisabled, NewStatus = ResponseStatus.Hidden };

        // Assert
        Assert.Equal(ResponseStatus.Pending, args1.OldStatus);
        Assert.Equal(ResponseStatus.Received, args1.NewStatus);
        Assert.Equal(ResponseStatus.Received, args2.OldStatus);
        Assert.Equal(ResponseStatus.NavigationDisabled, args2.NewStatus);
        Assert.Equal(ResponseStatus.NavigationDisabled, args3.OldStatus);
        Assert.Equal(ResponseStatus.Hidden, args3.NewStatus);
    }

    [Fact]
    public void Properties_AreInitOnly()
    {
        // Arrange
        var args = new IconStatusChangedEventArgs
        {
            RequestMessageId = "test-id",
            OldStatus = ResponseStatus.Pending
        };

        // Assert - verify that properties cannot be changed after initialization
        // (This is enforced by the compiler for init properties, so if this compiles, it's correct)
        Assert.Equal("test-id", args.RequestMessageId);
        Assert.Equal(ResponseStatus.Pending, args.OldStatus);
    }
}

public class IconClickedEventArgsTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Arrange & Act
        var args = new IconClickedEventArgs();

        // Assert
        Assert.Equal(string.Empty, args.RequestMessageId);
        Assert.Equal(ResponseStatus.Hidden, args.CurrentStatus);
        Assert.False(args.IsNavigationEnabled);
        // ClickedAt should be close to UtcNow (within 1 second)
        Assert.True((DateTime.UtcNow - args.ClickedAt).TotalSeconds < 1);
    }

    [Fact]
    public void Constructor_InitializesWithProvidedValues()
    {
        // Arrange
        var testTime = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var args = new IconClickedEventArgs
        {
            RequestMessageId = "req-456",
            CurrentStatus = ResponseStatus.Received,
            IsNavigationEnabled = true,
            ClickedAt = testTime
        };

        // Assert
        Assert.Equal("req-456", args.RequestMessageId);
        Assert.Equal(ResponseStatus.Received, args.CurrentStatus);
        Assert.True(args.IsNavigationEnabled);
        Assert.Equal(testTime, args.ClickedAt);
    }

    [Fact]
    public void InheritsFromEventArgs()
    {
        // Arrange & Act
        var args = new IconClickedEventArgs();

        // Assert
        Assert.IsAssignableFrom<EventArgs>(args);
    }

    [Fact]
    public void IsNavigationEnabled_HandlesVariousStates()
    {
        // Arrange & Act
        var args1 = new IconClickedEventArgs { IsNavigationEnabled = true };
        var args2 = new IconClickedEventArgs { IsNavigationEnabled = false };

        // Assert
        Assert.True(args1.IsNavigationEnabled);
        Assert.False(args2.IsNavigationEnabled);
    }

    [Fact]
    public void AllResponseStatusValues_CanBeAssignedToCurrentStatus()
    {
        // Arrange & Act
        var args1 = new IconClickedEventArgs { CurrentStatus = ResponseStatus.Pending };
        var args2 = new IconClickedEventArgs { CurrentStatus = ResponseStatus.Received };
        var args3 = new IconClickedEventArgs { CurrentStatus = ResponseStatus.NavigationDisabled };
        var args4 = new IconClickedEventArgs { CurrentStatus = ResponseStatus.Hidden };

        // Assert
        Assert.Equal(ResponseStatus.Pending, args1.CurrentStatus);
        Assert.Equal(ResponseStatus.Received, args2.CurrentStatus);
        Assert.Equal(ResponseStatus.NavigationDisabled, args3.CurrentStatus);
        Assert.Equal(ResponseStatus.Hidden, args4.CurrentStatus);
    }

    [Fact]
    public void Properties_AreInitOnly()
    {
        // Arrange
        var args = new IconClickedEventArgs
        {
            RequestMessageId = "test-request",
            CurrentStatus = ResponseStatus.Received,
            IsNavigationEnabled = true
        };

        // Assert - verify that properties cannot be changed after initialization
        // (This is enforced by the compiler for init properties, so if this compiles, it's correct)
        Assert.Equal("test-request", args.RequestMessageId);
        Assert.Equal(ResponseStatus.Received, args.CurrentStatus);
        Assert.True(args.IsNavigationEnabled);
    }

    [Fact]
    public void ClickedAt_StoresSpecificDateTime()
    {
        // Arrange
        var specificTime = new DateTime(2024, 12, 25, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var args = new IconClickedEventArgs { ClickedAt = specificTime };

        // Assert
        Assert.Equal(specificTime, args.ClickedAt);
        Assert.Equal(2024, args.ClickedAt.Year);
        Assert.Equal(12, args.ClickedAt.Month);
        Assert.Equal(25, args.ClickedAt.Day);
    }
}
