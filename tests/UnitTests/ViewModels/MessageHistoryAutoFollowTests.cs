using System;
using System.Collections.Generic;
using System.Text;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using MQTTnet;
using NSubstitute;
using Xunit;

namespace CrowsNestMqtt.UnitTests.ViewModels;

public class MessageHistoryAutoFollowTests
{
    private readonly ICommandParserService _commandParser = Substitute.For<ICommandParserService>();
    private readonly IMqttService _mqttService = Substitute.For<IMqttService>();

    [Fact]
    public void IncomingMessage_WhenAutoFollowEnabled_SelectsNewestMessage()
    {
        using var vm = CreateViewModel();
        vm.Settings.AutoFollowLatestMessage = true;
        vm.SelectedNode = new NodeViewModel("temperature", null) { FullPath = "sensors/temperature" };

        var first = BuildEvent("sensors/temperature", "21.5");
        RaiseBatch(first);
        var second = BuildEvent("sensors/temperature", "21.6");

        RaiseBatch(second);

        Assert.Equal(second.MessageId, vm.SelectedMessage?.MessageId);
    }

    [Fact]
    public void IncomingMessage_WhenAutoFollowDisabled_PreservesCurrentSelection()
    {
        using var vm = CreateViewModel();
        vm.Settings.AutoFollowLatestMessage = false;
        vm.SelectedNode = new NodeViewModel("temperature", null) { FullPath = "sensors/temperature" };

        var first = BuildEvent("sensors/temperature", "21.5");
        RaiseBatch(first);
        var second = BuildEvent("sensors/temperature", "21.6");

        RaiseBatch(second);

        Assert.Equal(first.MessageId, vm.SelectedMessage?.MessageId);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(
            _commandParser,
            _mqttService,
            environmentOverrides: new EnvironmentSettingsOverrides { IsAspireEnvironment = true },
            uiScheduler: System.Reactive.Concurrency.ImmediateScheduler.Instance);
    }

    private void RaiseBatch(IdentifiedMqttApplicationMessageReceivedEventArgs message)
    {
        _mqttService.MessagesBatchReceived += Raise.Event<EventHandler<IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs>>>(
            _mqttService,
            new List<IdentifiedMqttApplicationMessageReceivedEventArgs> { message });
    }

    private static IdentifiedMqttApplicationMessageReceivedEventArgs BuildEvent(string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .Build();

        return new IdentifiedMqttApplicationMessageReceivedEventArgs(Guid.NewGuid(), message, "test-client");
    }
}
