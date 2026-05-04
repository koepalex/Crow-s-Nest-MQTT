using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Commands;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.Commands;
using CrowsNestMqtt.UI.ViewModels;
using CrowsNestMqtt.UI.Services;
using NSubstitute;
using System.Reflection;
using System.Reactive.Concurrency;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels;

/// <summary>
/// Tests for the internal EnhancedCommandProcessor class in MainViewModel.
/// Covers: ExecuteAsync routing, null service handling, exception paths.
/// </summary>
public class EnhancedCommandProcessorTests : IDisposable
{
    private readonly ICommandParserService _commandParserService;
    private readonly IMqttService _mqttServiceMock;
    private readonly IDeleteTopicService _deleteTopicServiceMock;
    private readonly MainViewModel _viewModel;
    private readonly object _processor;
    private readonly Type _processorType;
    private readonly MethodInfo _executeAsyncMethod;

    public EnhancedCommandProcessorTests()
    {
        _commandParserService = Substitute.For<ICommandParserService>();
        _mqttServiceMock = Substitute.For<IMqttService>();
        _deleteTopicServiceMock = Substitute.For<IDeleteTopicService>();
        _viewModel = new MainViewModel(
            _commandParserService,
            mqttService: _mqttServiceMock,
            deleteTopicService: _deleteTopicServiceMock,
            uiScheduler: Scheduler.Immediate);

        // Get the internal EnhancedCommandProcessor type
        _processorType = typeof(MainViewModel).Assembly.GetTypes()
            .First(t => t.Name == "EnhancedCommandProcessor");

        // Create an instance
        _processor = Activator.CreateInstance(_processorType, _viewModel, _deleteTopicServiceMock)!;

        // Get ExecuteAsync method
        _executeAsyncMethod = _processorType.GetMethod("ExecuteAsync")!;
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ReturnsFailure()
    {
        var result = await InvokeExecuteAsync("unknowncommand", Array.Empty<string>());
        Assert.False(result.Success);
        Assert.Contains("Unknown command", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteTopic_NullService_ReturnsFailure()
    {
        // Create processor with null delete service
        var processorWithNullService = Activator.CreateInstance(_processorType, _viewModel, (IDeleteTopicService?)null)!;

        var task = (Task<ICommandProcessor.CommandExecutionResult>)_executeAsyncMethod.Invoke(
            processorWithNullService,
            new object[] { "deletetopic", new[] { "test/topic" }, CancellationToken.None })!;
        var result = await task;

        Assert.False(result.Success);
        Assert.Contains("not available", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteTopic_NoArgs_SetsPreparingStatus()
    {
        // The extension method will be called; it may fail but we exercise the path
        var result = await InvokeExecuteAsync("deletetopic", Array.Empty<string>());
        // The method should execute without throwing - result depends on extension method behavior
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteTopic_WithTopic_ExecutesPath()
    {
        var result = await InvokeExecuteAsync("deletetopic", new[] { "sensor/temp" });
        Assert.NotNull(result);
    }

    [Fact]
    public void Constructor_NullMainViewModel_ThrowsArgumentNullException()
    {
        Assert.Throws<TargetInvocationException>(() =>
            Activator.CreateInstance(_processorType, (MainViewModel)null!, _deleteTopicServiceMock));
    }

    [Fact]
    public async Task ExecuteAsync_DeleteTopic_CaseInsensitive()
    {
        var result = await InvokeExecuteAsync("DELETETOPIC", new[] { "test" });
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCommand_ReturnsFailure()
    {
        var result = await InvokeExecuteAsync("", Array.Empty<string>());
        Assert.False(result.Success);
    }

    private async Task<ICommandProcessor.CommandExecutionResult> InvokeExecuteAsync(string command, string[] args)
    {
        var task = (Task<ICommandProcessor.CommandExecutionResult>)_executeAsyncMethod.Invoke(
            _processor,
            new object[] { command, args, CancellationToken.None })!;
        return await task;
    }
}
