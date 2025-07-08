using Xunit;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Configuration;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void IsUsernamePasswordSelected_IsTrue_WhenAuthModeIsUsernamePassword()
    {
        // Arrange
        var vm = new SettingsViewModel
        {
            // Act
            SelectedAuthMode = SettingsViewModel.AuthModeSelection.UsernamePassword
        };

        // Assert
        Assert.True(vm.IsUsernamePasswordSelected);
        Assert.False(vm.IsEnhancedAuthSelected);
    }

    [Fact]
    public void IsEnhancedAuthSelected_IsTrue_WhenAuthModeIsEnhanced()
    {
        // Arrange
        var vm = new SettingsViewModel
        {
            // Act
            SelectedAuthMode = SettingsViewModel.AuthModeSelection.Enhanced
        };

        // Assert
        Assert.False(vm.IsUsernamePasswordSelected);
        Assert.True(vm.IsEnhancedAuthSelected);
    }

    [Fact]
    public void IsUsernamePasswordSelected_And_IsEnhancedAuthSelected_AreFalse_WhenAuthModeIsAnonymous()
    {
        // Arrange
        var vm = new SettingsViewModel
        {
            // Act
            SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous
        };

        // Assert
        Assert.False(vm.IsUsernamePasswordSelected);
        Assert.False(vm.IsEnhancedAuthSelected);
    }

    [Fact]
    public void Into_SettingsData_HasCorrectAuthMode_ForEnhanced()
    {
        // Arrange
        var vm = new SettingsViewModel
        {
            SelectedAuthMode = SettingsViewModel.AuthModeSelection.Enhanced,
            AuthenticationData = "my-token"
        };

        // Act
        var settingsData = vm.Into();

        // Assert
        Assert.Equal("Enhanced Authentication", settingsData.AuthenticationMethod);
        Assert.Equal("my-token", settingsData.AuthenticationData);
        Assert.IsType<AnonymousAuthenticationMode>(settingsData.AuthMode);
    }
    
    [Fact]
    public void From_SettingsData_SetsCorrectAuthMode_ForEnhanced()
    {
        // Arrange
        var settingsData = new SettingsData("host", 1, "client", 1, true, 1, new AnonymousAuthenticationMode(), null, null, "Enhanced Authentication", "my-token");
        var vm = new SettingsViewModel();

        // Act
        vm.From(settingsData);

        // Assert
        Assert.Equal(SettingsViewModel.AuthModeSelection.Enhanced, vm.SelectedAuthMode);
        Assert.Equal("Enhanced Authentication", vm.AuthenticationMethod);
        Assert.Equal("my-token", vm.AuthenticationData);
    }
}
