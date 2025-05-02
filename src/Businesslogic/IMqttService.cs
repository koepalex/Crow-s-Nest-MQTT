namespace CrowsNestMqtt.BusinessLogic;

using MQTTnet;
using MQTTnet.Protocol;


/// <summary>
/// Interface defining the contract for the MQTT interaction service.
/// </summary>
public interface IMqttService : IDisposable
{
    /// <summary>
    /// Event raised when a new MQTT message is received, including a unique identifier.
    /// </summary>
    event EventHandler<IdentifiedMqttApplicationMessageReceivedEventArgs>? MessageReceived;

    /// <summary>
    /// Event raised when the connection state to the MQTT broker changes.
    /// </summary>
    event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>
    /// Event raised for logging internal messages or errors from the service.
    /// </summary>
    event EventHandler<string>? LogMessage;

    /// <summary>
    /// Attempts to retrieve a specific message by its topic and unique identifier.
    /// </summary>
    /// <param name="topic">The topic of the message.</param>
    /// <param name="messageId">The unique identifier of the message.</param>
    /// <param name="message">The retrieved message, or null if not found.</param>
    /// <returns>True if the message was found, false otherwise.</returns>
    bool TryGetMessage(string topic, Guid messageId, out MqttApplicationMessage? message);

    /// <summary>
    /// Connects to the MQTT broker using the configured settings.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the MQTT broker.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="retain">Whether the message should be retained.</param>
    /// <param name="qos">The Quality of Service level.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PublishAsync(string topic, string payload, bool retain = false, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all messages from all internal topic buffers.
    /// </summary>
    void ClearAllBuffers();

    /// <summary>
    /// Gets a list of all topics currently held in buffers.
    /// </summary>
    IEnumerable<string> GetBufferedTopics();

    /// <summary>
    /// Updates the connection settings used by the service for subsequent connection attempts.
    /// Note: May require a disconnect/reconnect cycle to apply to an active connection.
    /// </summary>
    /// <param name="newSettings">The new settings to use.</param>
    void UpdateSettings(MqttConnectionSettings newSettings);

    // Potentially add SubscribeAsync/UnsubscribeAsync if needed by ViewModels directly
    // Task<MqttClientSubscribeResult> SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, CancellationToken cancellationToken = default);
    // Task<MqttClientUnsubscribeResult> UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default);
}