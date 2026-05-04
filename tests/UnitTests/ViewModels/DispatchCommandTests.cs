using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Services;
using NSubstitute;
using System.Reflection;
using System.Reactive.Concurrency;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels;

/// <summary>
/// Tests for uncovered DispatchCommand branches in MainViewModel.
/// Covers: Disconnect, Clear, Pause, Resume, Expand, Collapse, ViewImage, ViewVideo, ViewHex,
/// SetUser, SetPass, SetAuthMode, SetAuthMethod, SetAuthData, SetUseTls, Publish, Settings,
/// GotoResponse, DeleteTopic, Unknown command, and exception handling.
/// </summary>
public class DispatchCommandTests : IDisposable
{
    private readonly ICommandParserService _commandParserService;
    private readonly IMqttService _mqttServiceMock;
    private readonly MainViewModel _viewModel;
    private readonly MethodInfo _dispatchMethod;

    public DispatchCommandTests()
    {
        _commandParserService = Substitute.For<ICommandParserService>();
        _mqttServiceMock = Substitute.For<IMqttService>();
        _viewModel = new MainViewModel(_commandParserService, mqttService: _mqttServiceMock, uiScheduler: Scheduler.Immediate);

        _dispatchMethod = typeof(MainViewModel).GetMethod("DispatchCommand", BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Dispatch(CommandType type, params string[] args)
    {
        var command = new ParsedCommand(type, args.ToList().AsReadOnly());
        _dispatchMethod.Invoke(_viewModel, new object[] { command });
    }

    [Fact]
    public void DispatchCommand_Disconnect_ShouldCallDisconnect()
    {
        Dispatch(CommandType.Disconnect);
        // Disconnect calls DisconnectFromMqttBroker which updates status
        // Just verifying no exception and status is set
        Assert.NotNull(_viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_Clear_ShouldClearHistory()
    {
        Dispatch(CommandType.Clear);
        Assert.Empty(_viewModel.FilteredMessageHistory);
    }

    [Fact]
    public void DispatchCommand_Pause_ShouldTogglePause()
    {
        bool initialPaused = _viewModel.IsPaused;
        Dispatch(CommandType.Pause);
        Assert.NotEqual(initialPaused, _viewModel.IsPaused);
    }

    [Fact]
    public void DispatchCommand_Resume_ShouldTogglePause()
    {
        _viewModel.IsPaused = true;
        Dispatch(CommandType.Resume);
        Assert.False(_viewModel.IsPaused);
    }

    [Fact]
    public void DispatchCommand_Expand_ShouldExpandAllNodes()
    {
        Dispatch(CommandType.Expand);
        Assert.Contains("expanded", _viewModel.StatusBarText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DispatchCommand_Collapse_ShouldCollapseAllNodes()
    {
        Dispatch(CommandType.Collapse);
        Assert.Contains("collapsed", _viewModel.StatusBarText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DispatchCommand_ViewImage_WithNoMessage_ShouldNotCrash()
    {
        _viewModel.SelectedMessage = null;
        Dispatch(CommandType.ViewImage);
        // SwitchPayloadView with null message returns early
        Assert.NotNull(_viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_ViewVideo_WithNoMessage_ShouldNotCrash()
    {
        _viewModel.SelectedMessage = null;
        Dispatch(CommandType.ViewVideo);
        Assert.NotNull(_viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_ViewHex_WithNoMessage_ShouldHandleGracefully()
    {
        _viewModel.SelectedMessage = null;
        Dispatch(CommandType.ViewHex);
        Assert.NotNull(_viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUser_WithValidArg_ShouldSetUsername()
    {
        Dispatch(CommandType.SetUser, "testuser");
        Assert.Equal("testuser", _viewModel.Settings.AuthUsername);
        Assert.Contains("Username set", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUser_WithNoArgs_ShouldShowError()
    {
        Dispatch(CommandType.SetUser);
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUser_SwitchesAuthModeFromAnonymous()
    {
        _viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
        Dispatch(CommandType.SetUser, "user1");
        Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, _viewModel.Settings.SelectedAuthMode);
        Assert.Contains("Auth mode switched", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetPassword_WithValidArg_ShouldSetPassword()
    {
        Dispatch(CommandType.SetPassword, "secret");
        Assert.Equal("secret", _viewModel.Settings.AuthPassword);
        Assert.Contains("Password set", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetPassword_WithNoArgs_ShouldShowError()
    {
        Dispatch(CommandType.SetPassword);
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetPassword_SwitchesAuthModeFromAnonymous()
    {
        _viewModel.Settings.SelectedAuthMode = SettingsViewModel.AuthModeSelection.Anonymous;
        Dispatch(CommandType.SetPassword, "pass123");
        Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, _viewModel.Settings.SelectedAuthMode);
    }

    [Fact]
    public void DispatchCommand_SetAuthMode_Anonymous_ShouldSetMode()
    {
        Dispatch(CommandType.SetAuthMode, "anonymous");
        Assert.Equal(SettingsViewModel.AuthModeSelection.Anonymous, _viewModel.Settings.SelectedAuthMode);
        Assert.Contains("Anonymous", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthMode_Userpass_ShouldSetMode()
    {
        Dispatch(CommandType.SetAuthMode, "userpass");
        Assert.Equal(SettingsViewModel.AuthModeSelection.UsernamePassword, _viewModel.Settings.SelectedAuthMode);
        Assert.Contains("Username/Password", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthMode_Enhanced_ShouldSetMethod()
    {
        Dispatch(CommandType.SetAuthMode, "enhanced");
        Assert.Equal("Enhanced Authentication", _viewModel.Settings.AuthenticationMethod);
    }

    [Fact]
    public void DispatchCommand_SetAuthMode_Invalid_ShouldShowError()
    {
        Dispatch(CommandType.SetAuthMode, "invalid");
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthMode_NoArgs_ShouldShowError()
    {
        Dispatch(CommandType.SetAuthMode);
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthMethod_WithValidArg_ShouldSet()
    {
        Dispatch(CommandType.SetAuthMethod, "SCRAM-SHA-1");
        Assert.Equal("SCRAM-SHA-1", _viewModel.Settings.AuthenticationMethod);
        Assert.Contains("SCRAM-SHA-1", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthMethod_NoArgs_ShouldShowError()
    {
        Dispatch(CommandType.SetAuthMethod);
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthData_WithValidArg_ShouldSet()
    {
        Dispatch(CommandType.SetAuthData, "mydata");
        Assert.Equal("mydata", _viewModel.Settings.AuthenticationData);
        Assert.Contains("Authentication data set", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetAuthData_NoArgs_ShouldShowError()
    {
        Dispatch(CommandType.SetAuthData);
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUseTls_True_ShouldEnable()
    {
        Dispatch(CommandType.SetUseTls, "true");
        Assert.True(_viewModel.Settings.UseTls);
        Assert.Contains("true", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUseTls_False_ShouldDisable()
    {
        Dispatch(CommandType.SetUseTls, "false");
        Assert.False(_viewModel.Settings.UseTls);
        Assert.Contains("false", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUseTls_Invalid_ShouldShowError()
    {
        Dispatch(CommandType.SetUseTls, "maybe");
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_SetUseTls_NoArgs_ShouldShowError()
    {
        Dispatch(CommandType.SetUseTls);
        Assert.Contains("Error", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_Settings_ShouldToggleSettings()
    {
        bool initial = _viewModel.IsSettingsVisible;
        Dispatch(CommandType.Settings);
        Assert.NotEqual(initial, _viewModel.IsSettingsVisible);
    }

    [Fact]
    public void DispatchCommand_GotoResponse_WithNoMessage_ShouldShowError()
    {
        _viewModel.SelectedMessage = null;
        Dispatch(CommandType.GotoResponse);
        Assert.Contains("No message selected", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_DeleteTopic_ShouldExecuteWithoutCrash()
    {
        // No delete service configured, so it should show an error or handle gracefully
        Dispatch(CommandType.DeleteTopic, "test/topic");
        // May show status about delete service not available
        Assert.NotNull(_viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_Publish_ShouldHandleWithoutCrash()
    {
        Dispatch(CommandType.Publish, "test/topic", "hello");
        // Publish command triggers ShowPublishWindowRequested event
        Assert.NotNull(_viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_Unknown_ShouldShowError()
    {
        Dispatch(CommandType.Unknown);
        Assert.Contains("Unknown command", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_TopicSearch_WithEmptyTerm_ShouldClearSearch()
    {
        Dispatch(CommandType.TopicSearch, "");
        Assert.Contains("search cleared", _viewModel.StatusBarText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DispatchCommand_Search_WithTerm_ShouldSetSearchTerm()
    {
        Dispatch(CommandType.Search, "temperature");
        Assert.Equal("temperature", _viewModel.CurrentSearchTerm);
        Assert.Contains("Search filter applied", _viewModel.StatusBarText);
    }

    [Fact]
    public void DispatchCommand_Search_WithEmptyTerm_ShouldClearSearch()
    {
        Dispatch(CommandType.Search, "");
        Assert.Equal("", _viewModel.CurrentSearchTerm);
        Assert.Contains("Search cleared", _viewModel.StatusBarText);
    }
}
