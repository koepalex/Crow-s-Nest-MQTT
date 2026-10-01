namespace CrowsNestMqtt.TestDataSender;

using System.Text.Json;

internal sealed record LiveMessage(string Topic, string ContentType, byte[] Payload);

internal static class LiveMessagePublisher
{
    public const string DefaultTopic = "test/live/auto-follow";

    public static async Task PublishAsync(
        Func<LiveMessage, CancellationToken, Task> publishAsync,
        TimeSpan interval,
        TimeSpan duration,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        string topic = DefaultTopic,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publishAsync);
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "The publish interval must be positive.");
        }

        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "The stream duration must be positive.");
        }

        delayAsync ??= static (delay, token) => Task.Delay(delay, token);
        var messageCount = (int)Math.Ceiling(duration.TotalSeconds / interval.TotalSeconds);

        for (var sequence = 1; sequence <= messageCount; sequence++)
        {
            await delayAsync(interval, cancellationToken).ConfigureAwait(false);

            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                messageType = "auto_follow_test",
                sequence,
                totalMessages = messageCount,
                timestamp = DateTimeOffset.UtcNow,
            });

            await publishAsync(
                    new LiveMessage(topic, "application/json", payload),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
