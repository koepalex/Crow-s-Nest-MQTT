namespace CrowsNestMqtt.AppHostTests;

using System.Text.Json;
using CrowsNestMqtt.TestDataSender;
using Xunit;

public sealed class LiveMessagePublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsExpectedMessagesAtConfiguredInterval()
    {
        var messages = new List<LiveMessage>();
        var delays = new List<TimeSpan>();

        await LiveMessagePublisher.PublishAsync(
            (message, _) =>
            {
                messages.Add(message);
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        Assert.Equal(2, messages.Count);
        Assert.All(messages, message =>
        {
            Assert.Equal("test/live/auto-follow", message.Topic);
            Assert.Equal("application/json", message.ContentType);
        });
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)], delays);

        using var firstPayload = JsonDocument.Parse(messages[0].Payload);
        using var secondPayload = JsonDocument.Parse(messages[1].Payload);
        Assert.Equal(1, firstPayload.RootElement.GetProperty("sequence").GetInt32());
        Assert.Equal(2, secondPayload.RootElement.GetProperty("sequence").GetInt32());
        Assert.Equal(2, secondPayload.RootElement.GetProperty("totalMessages").GetInt32());
        Assert.Equal("auto_follow_test", secondPayload.RootElement.GetProperty("messageType").GetString());
    }
}
