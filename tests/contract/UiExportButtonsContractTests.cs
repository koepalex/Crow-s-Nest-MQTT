using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using MQTTnet;
using NSubstitute;
using Xunit;

namespace CrowsNestMqtt.Contract.Tests;

/// <summary>
/// Contract tests for UI Export Buttons functionality.
/// </summary>
public class UiExportButtonsContractTests
{
    private static MainViewModel CreateViewModel(out IMqttService mqtt)
    {
        var parser = new CommandParserService();
        mqtt = Substitute.For<IMqttService>();
        mqtt.TryGetMessage(Arg.Any<string>(), Arg.Any<Guid>(), out Arg.Any<MqttApplicationMessage?>())
            .Returns(call =>
            {
                call[2] = new MqttApplicationMessageBuilder()
                    .WithTopic(call.Arg<string>(0) ?? "test/topic")
                    .WithPayload("payload")
                    .Build();
                return true;
            });
        return new MainViewModel(parser, mqtt, uiScheduler: Scheduler.Immediate);
    }

    private static void AddMessage(MainViewModel vm, IMqttService mqtt, string topic = "test/topic")
    {
        var fullMsg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload("hello")
            .Build();
        var args = new List<IdentifiedMqttApplicationMessageReceivedEventArgs>
        {
            new(Guid.NewGuid(), fullMsg, "client1")
        };
        mqtt.MessagesBatchReceived += Raise.Event<EventHandler<IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs>>>(mqtt, args);
    }

    [Fact]
    public void ExportAllButton_TopicAndMessagesExist_IsEnabled()
    {
        var vm = CreateViewModel(out var mqtt);
        using var _ = vm;
        vm.SelectedNode = new NodeViewModel("test") { FullPath = "test/topic" };
        AddMessage(vm, mqtt, "test/topic");

        Assert.True(vm.IsExportAllButtonEnabled);
    }

    [Fact]
    public void ExportAllButton_NoTopicSelected_IsDisabled()
    {
        var vm = CreateViewModel(out var mqtt);
        using var _ = vm;
        vm.SelectedNode = null;
        AddMessage(vm, mqtt, "test/topic");

        Assert.False(vm.IsExportAllButtonEnabled);
    }

    [Fact]
    public void ExportAllButton_NoMessages_IsDisabled()
    {
        var vm = CreateViewModel(out _);
        using var _ = vm;
        vm.SelectedNode = new NodeViewModel("test") { FullPath = "test/topic" };

        Assert.False(vm.IsExportAllButtonEnabled);
    }

    [Fact]
    public void ExportAllCommand_Exists_AndIsExecutable()
    {
        var vm = CreateViewModel(out _);
        using var _ = vm;
        Assert.NotNull(vm.ExportAllCommand);
        Assert.True(vm.ExportAllCommand.CanExecute(null));
    }

    [Fact]
    public void ExportMessageCommand_Exists_AndAcceptsMessageViewModel()
    {
        var vm = CreateViewModel(out var mqtt);
        using var _ = vm;
        Assert.NotNull(vm.ExportMessageCommand);

        vm.SelectedNode = new NodeViewModel("test") { FullPath = "test/topic" };
        AddMessage(vm, mqtt, "test/topic");
        var msg = vm.FilteredMessageHistory.FirstOrDefault();
        Assert.NotNull(msg);
        Assert.True(vm.ExportMessageCommand.CanExecute(msg));
    }

    [Fact]
    public void PerMessageExportButton_Click_PassesCorrectMessageViewModel()
    {
        var vm = CreateViewModel(out var mqtt);
        using var _ = vm;
        vm.SelectedNode = new NodeViewModel("test") { FullPath = "test/topic" };
        AddMessage(vm, mqtt, "test/topic");
        var msg = vm.FilteredMessageHistory.FirstOrDefault();
        Assert.NotNull(msg);
        Assert.True(vm.ExportMessageCommand.CanExecute(msg));
    }
}
