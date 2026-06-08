namespace CrowsNestMqtt.BusinessLogic.Services;

using CrowsNestMqtt.BusinessLogic.Configuration;

public interface IAutoLogService : IDisposable
{
    void UpdateConfiguration(string? exportPath, IEnumerable<AutoLogTopicRule> rules, long maxDatabaseSizeBytes);

    Task LogBatchAsync(IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs> batch, CancellationToken cancellationToken = default);

    bool IsEnabledForTopic(string topic);
}
