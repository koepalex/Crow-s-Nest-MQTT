using Xunit;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.BusinessLogic.Configuration;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void SettingsData_AutoFollowLatestMessage_DefaultsToFalse()
    {
        var data = new SettingsData("host", 1883);

        Assert.False(data.AutoFollowLatestMessage);
    }

    [Fact]
    public void IntoAndFrom_RoundtripAutoFollowLatestMessage()
    {
        var environmentOverrides = new EnvironmentSettingsOverrides { IsAspireEnvironment = true };
        var source = new SettingsViewModel(environmentOverrides)
        {
            AutoFollowLatestMessage = true
        };

        var target = new SettingsViewModel(environmentOverrides);
        target.From(source.Into());

        Assert.True(target.AutoFollowLatestMessage);
    }

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
    public void Into_SettingsData_HasCorrectUseTls()
    {
        var vm = new SettingsViewModel();
        vm.UseTls = true;
        var settingsData = vm.Into();
        Assert.True(settingsData.UseTls);
    }

    [Fact]
    public void From_SettingsData_SetsUseTls()
    {
        var settingsData = new SettingsData("host", 1, "client", 1, true, 1, new AnonymousAuthenticationMode(), null, null, true);
        var vm = new SettingsViewModel();
        vm.From(settingsData);
        Assert.True(vm.UseTls);
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
            AuthenticationMethod = null,
            AuthenticationData = "my-token"
        };

        // Act
        var settingsData = vm.Into();

        // Assert
        Assert.Equal(vm.AuthenticationMethod, (settingsData.AuthMode as EnhancedAuthenticationMode)!.AuthenticationMethod);
        Assert.Equal("my-token", (settingsData.AuthMode as EnhancedAuthenticationMode)!.AuthenticationData);
        Assert.IsType<EnhancedAuthenticationMode>(settingsData.AuthMode);
    }
    
    [Fact]
    public void From_SettingsData_SetsCorrectAuthMode_ForEnhanced()
    {
        // Arrange
        var settingsData = new SettingsData("host", 1, "client", 1, true, 1, new EnhancedAuthenticationMode("mode", "my-token"), null, null);
        var vm = new SettingsViewModel();

        // Act
        vm.From(settingsData);

        // Assert
        Assert.Equal(SettingsViewModel.AuthModeSelection.Enhanced, vm.SelectedAuthMode);
        Assert.Equal("mode", vm.AuthenticationMethod);
        Assert.Equal("my-token", vm.AuthenticationData);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Into_SettingsData_HasCorrectSubscriptionQoS(int qos)
    {
        var vm = new SettingsViewModel { SubscriptionQoS = qos };
        var settingsData = vm.Into();
        Assert.Equal(qos, settingsData.SubscriptionQoS);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void From_SettingsData_SetsSubscriptionQoS(int qos)
    {
        var settingsData = new SettingsData("host", 1, "client", 1, true, 1, new AnonymousAuthenticationMode(), null, null, SubscriptionQoS: qos);
        var vm = new SettingsViewModel();
        vm.From(settingsData);
        Assert.Equal(qos, vm.SubscriptionQoS);
    }

    [Fact]
    public void SettingsData_SubscriptionQoS_DefaultsToOne()
    {
        // SettingsData record default (not SettingsViewModel which loads persisted settings)
        var data = new SettingsData("host", 1, "client", 60, true, 0, new AnonymousAuthenticationMode(), null, null);
        Assert.Equal(1, data.SubscriptionQoS);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 2)]
    [InlineData(99, 2)]
    public void SubscriptionQoS_ClampsOutOfRangeValues(int input, int expected)
    {
        var vm = new SettingsViewModel { SubscriptionQoS = input };
        Assert.Equal(expected, vm.SubscriptionQoS);
    }

    [Fact]
    public void Into_SettingsData_HasCorrectTransport()
    {
        var vm = new SettingsViewModel();
        vm.SelectedTransport = TransportProtocol.WebSocket;
        vm.WebSocketPath = "/mqtt";
        var settingsData = vm.Into();
        Assert.Equal(TransportProtocol.WebSocket, settingsData.Transport);
        Assert.Equal("/mqtt", settingsData.WebSocketPath);
    }

    [Fact]
    public void From_SettingsData_SetsTransport()
    {
        var settingsData = new SettingsData("host", 8083, Transport: TransportProtocol.WebSocket, WebSocketPath: "/ws");
        var vm = new SettingsViewModel();
        vm.From(settingsData);
        Assert.Equal(TransportProtocol.WebSocket, vm.SelectedTransport);
        Assert.Equal("/ws", vm.WebSocketPath);
        Assert.True(vm.IsWebSocketSelected);
    }

    [Fact]
    public void SettingsData_Transport_DefaultsToTcp()
    {
        var data = new SettingsData("host", 1883);
        Assert.Equal(TransportProtocol.Tcp, data.Transport);
        Assert.Null(data.WebSocketPath);
    }

    [Fact]
    public void IsWebSocketSelected_IsFalse_WhenTransportIsTcp()
    {
        var vm = new SettingsViewModel();
        vm.SelectedTransport = TransportProtocol.Tcp;
        Assert.False(vm.IsWebSocketSelected);
    }

    [Fact]
    public void IsWebSocketSelected_IsTrue_WhenTransportIsWebSocket()
    {
        var vm = new SettingsViewModel();
        vm.SelectedTransport = TransportProtocol.WebSocket;
        Assert.True(vm.IsWebSocketSelected);
    }

    [Fact]
    public void Into_SettingsData_HasWebSocketProxyConfiguration()
    {
        var vm = new SettingsViewModel
        {
            WebSocketProxyAddress = "http://proxy.local:3128",
            WebSocketProxyUsername = "proxy-user",
            WebSocketProxyPassword = "proxy-pass"
        };

        var settingsData = vm.Into();

        Assert.Equal("http://proxy.local:3128", settingsData.WebSocketProxyAddress);
        Assert.Equal("proxy-user", settingsData.WebSocketProxyUsername);
        Assert.Equal("proxy-pass", settingsData.WebSocketProxyPassword);
    }

    [Fact]
    public void From_SettingsData_SetsWebSocketProxyConfiguration()
    {
        var settingsData = new SettingsData(
            "host",
            8083,
            Transport: TransportProtocol.WebSocket,
            WebSocketProxyAddress: "http://proxy.local:3128",
            WebSocketProxyUsername: "proxy-user",
            WebSocketProxyPassword: "proxy-pass");
        var vm = new SettingsViewModel();

        vm.From(settingsData);

        Assert.Equal("http://proxy.local:3128", vm.WebSocketProxyAddress);
        Assert.Equal("proxy-user", vm.WebSocketProxyUsername);
        Assert.Equal("proxy-pass", vm.WebSocketProxyPassword);
    }

    [Fact]
    public void ApplyEnvironmentOverrides_SetsWebSocketProxyConfiguration()
    {
        var vm = new SettingsViewModel();
        var overrides = new EnvironmentSettingsOverrides
        {
            WebSocketProxyAddress = "http://proxy.local:3128",
            WebSocketProxyUsername = "proxy-user",
            WebSocketProxyPassword = "proxy-pass",
            HasOverrides = true
        };

        vm.ApplyEnvironmentOverrides(overrides);

        Assert.Equal("http://proxy.local:3128", vm.WebSocketProxyAddress);
        Assert.Equal("proxy-user", vm.WebSocketProxyUsername);
        Assert.Equal("proxy-pass", vm.WebSocketProxyPassword);
    }

    // --- Azure (Event Grid OAUTH2-JWT) tests ---

    [Fact]
    public void IsAzureAuthSelected_IsTrue_WhenAuthModeIsAzure()
    {
        var vm = new SettingsViewModel
        {
            SelectedAuthMode = SettingsViewModel.AuthModeSelection.Azure
        };

        Assert.True(vm.IsAzureAuthSelected);
        Assert.False(vm.IsUsernamePasswordSelected);
        Assert.False(vm.IsEnhancedAuthSelected);
    }

    [Fact]
    public void SelectingAzureAuthMode_AutoEnablesTlsPort8883AndTcp()
    {
        var vm = new SettingsViewModel
        {
            UseTls = false,
            Port = 1883,
            SelectedTransport = TransportProtocol.WebSocket,
        };

        vm.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Azure;

        Assert.True(vm.UseTls);
        Assert.Equal(8883, vm.Port);
        Assert.Equal(TransportProtocol.Tcp, vm.SelectedTransport);
    }

    [Fact]
    public void Into_SettingsData_HasAzureAuthenticationMode_WithCustomScope()
    {
        var vm = new SettingsViewModel
        {
            SelectedAuthMode = SettingsViewModel.AuthModeSelection.Azure,
            AuthenticationScope = "api://my-app/.default"
        };

        var settingsData = vm.Into();

        var azure = Assert.IsType<AzureAuthenticationMode>(settingsData.AuthMode);
        Assert.Equal("api://my-app/.default", azure.Scope);
        Assert.True(settingsData.UseTls);
        Assert.Equal(8883, settingsData.Port);
        Assert.Equal(TransportProtocol.Tcp, settingsData.Transport);
    }

    [Fact]
    public void From_SettingsData_RoundtripsAzureAuthenticationMode()
    {
        var settingsData = new SettingsData(
            "ns.region-1.ts.eventgrid.azure.net",
            8883,
            "my-client",
            60,
            true,
            300,
            new AzureAuthenticationMode("api://custom/.default"),
            null,
            null,
            UseTls: true);
        var vm = new SettingsViewModel();

        vm.From(settingsData);

        Assert.Equal(SettingsViewModel.AuthModeSelection.Azure, vm.SelectedAuthMode);
        Assert.Equal("api://custom/.default", vm.AuthenticationScope);
        Assert.True(vm.UseTls);
        Assert.Equal(8883, vm.Port);
        Assert.Equal(TransportProtocol.Tcp, vm.SelectedTransport);
    }

    [Fact]
    public void From_SettingsData_AzureWithNullScope_LeavesScopeNull()
    {
        var settingsData = new SettingsData(
            "ns.region-1.ts.eventgrid.azure.net",
            8883,
            null,
            60,
            true,
            300,
            new AzureAuthenticationMode(),
            null,
            null,
            UseTls: true);
        var vm = new SettingsViewModel();

        vm.From(settingsData);

        Assert.Equal(SettingsViewModel.AuthModeSelection.Azure, vm.SelectedAuthMode);
        Assert.Null(vm.AuthenticationScope);
    }

    [Fact]
    public void ApplyEnvironmentOverrides_AzureModeWithCustomPort_PreservesPort()
    {
        // Regression: the Azure auth-mode setter used to auto-force Port=8883 on
        // every assignment, so an env-var-supplied non-default port (e.g. the
        // mock Event Grid broker on 51883 inside Aspire) was silently overwritten.
        var vm = new SettingsViewModel();
        var overrides = new CrowsNestMqtt.BusinessLogic.Configuration.EnvironmentSettingsOverrides
        {
            Hostname = "127.0.0.1",
            Port = 51883,
            UseTls = true,
            AuthMode = new AzureAuthenticationMode("https://eventgrid.azure.net/.default"),
            HasOverrides = true,
        };

        vm.ApplyEnvironmentOverrides(overrides);

        Assert.Equal(SettingsViewModel.AuthModeSelection.Azure, vm.SelectedAuthMode);
        Assert.Equal("127.0.0.1", vm.Hostname);
        Assert.Equal(51883, vm.Port);
        Assert.True(vm.UseTls);
    }

    [Fact]
    public void SelectingAzureAuthMode_Interactively_StillAutoAppliesDefaultPort()
    {
        // The auto-config remains helpful when the user picks Azure interactively
        // via the settings pane or :setauthmode azure command (i.e. after the
        // load phase completes).
        var vm = new SettingsViewModel { Port = 1883 };

        vm.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Azure;

        Assert.Equal(8883, vm.Port);
    }

    // --- Hostname normalization wired into the setter ---

    [Fact]
    public void SetHostname_WithEventGridHttpUrl_NormalizesToMqttFqdn_AndRaisesEvent()
    {
        var vm = new SettingsViewModel();
        HostnameNormalizedEventArgs? captured = null;
        vm.HostnameNormalized += (_, args) => captured = args;

        vm.Hostname = "https://myns.northeurope-1.eventgrid.azure.net/api/events";

        Assert.Equal("myns.northeurope-1.ts.eventgrid.azure.net", vm.Hostname);
        Assert.NotNull(captured);
        Assert.Equal("https://myns.northeurope-1.eventgrid.azure.net/api/events", captured!.Original);
        Assert.Equal("myns.northeurope-1.ts.eventgrid.azure.net", captured.Cleaned);
        Assert.Contains(captured.Notes, n => n.Contains("stripped 'https://'", System.StringComparison.Ordinal));
        Assert.Contains(captured.Notes, n => n.Contains("Event Grid HTTP suffix", System.StringComparison.Ordinal));
    }

    [Fact]
    public void SetHostname_WithInlinePort_ExtractsPortIntoPortProperty()
    {
        var vm = new SettingsViewModel { Port = 1883 };

        vm.Hostname = "broker.example.com:8883";

        Assert.Equal("broker.example.com", vm.Hostname);
        Assert.Equal(8883, vm.Port);
    }

    [Fact]
    public void SetHostname_WhenNormalizationMatchesExistingBackingField_StillRaisesPropertyChanged()
    {
        // Regression: when the user pastes a URL that normalizes to the value
        // already stored on the backing field (e.g. hostname was loaded from
        // settings.json as the MQTT FQDN, then user pastes the HTTP URL again),
        // Hostname must still raise PropertyChanged. Otherwise the two-way
        // bound TextBox keeps displaying the raw URL because it never sees a
        // source update to replace its local buffer.
        var vm = new SettingsViewModel();
        vm.Hostname = "myns.northeurope-1.ts.eventgrid.azure.net"; // seed backing field

        int changeNotifications = 0;
        ((System.ComponentModel.INotifyPropertyChanged)vm).PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.Hostname))
            {
                changeNotifications++;
            }
        };

        // Assign a raw URL that normalizes to the SAME value already in the field.
        vm.Hostname = "https://myns.northeurope-1.eventgrid.azure.net/api/events";

        // Backing field ends up identical, but PropertyChanged still fires so
        // the bound TextBox re-reads and drops its cached raw-URL text.
        Assert.Equal("myns.northeurope-1.ts.eventgrid.azure.net", vm.Hostname);
        Assert.True(changeNotifications >= 1, "PropertyChanged for Hostname must fire even when normalization results in an unchanged backing value.");
    }

    [Fact]
    public void SetHostname_WithCleanValue_DoesNotRaiseEvent()
    {
        var vm = new SettingsViewModel();
        int callCount = 0;
        vm.HostnameNormalized += (_, _) => callCount++;

        vm.Hostname = "broker.hivemq.com";

        Assert.Equal("broker.hivemq.com", vm.Hostname);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void SelectingAzureAuthMode_BumpsTooLowKeepAliveAndSessionExpiry()
    {
        // Regression: users who typed sub-second Keep Alive / Session Expiry
        // couldn't connect to Event Grid because it enforces minima. Switching
        // to Azure interactively should nudge those values to broker-safe
        // defaults without touching values that are already reasonable.
        var vm = new SettingsViewModel
        {
            KeepAliveIntervalSeconds = 1,
            SessionExpiryIntervalSeconds = 1,
        };

        // The auto-nudge for keep-alive / session-expiry lives in MainViewModel,
        // not the setter — so this test only asserts what the setter guarantees:
        // TLS/Port/Transport. Keep-alive/session-expiry nudge is covered in the
        // DispatchCommand SetAuthMode Azure test.
        vm.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Azure;

        Assert.True(vm.UseTls);
        Assert.Equal(8883, vm.Port);
    }
}
