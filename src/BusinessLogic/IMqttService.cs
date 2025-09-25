namespace CrowsNestMqtt.BusinessLogic;

using MQTTnet;
using MQTTnet.Protocol;


/// <summary>
/// Interface defining the contract for the MQTT interaction service.
/// </summary>
public interface IMqttService : IDisposable
{
    /// <summary>
    /// Event raised when a batch of MQTT messages is received for optimized UI processing.
    /// </summary>
    event EventHandler<IReadOnlyList<IdentifiedMqttApplicationMessageReceivedEventArgs>>? MessagesBatchReceived;

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
    /// Publishes an MQTT message to the specified topic with a byte array payload.
    /// </summary>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="payload">The message payload as a byte array.</param>
    /// <param name="retain">Whether this message should be retained by the broker.</param>
    /// <param name="qos">The Quality of Service level for the message.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    Task PublishAsync(string topic, byte[] payload, bool retain = false, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears a retained message from the specified topic by publishing an empty retained message.
    /// This is the proper way to delete retained messages in MQTT.
    /// </summary>
    /// <param name="topic">The topic to clear the retained message from.</param>
    /// <param name="qos">The Quality of Service level for the message.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    Task ClearRetainedMessageAsync(string topic, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all messages from all internal topic buffers.
    /// </summary>
    void ClearAllBuffers();

    /// <summary>
    /// Retrieves the buffered messages for a specific topic, including their IDs.
    /// </summary>
    /// <param name="topic">The topic to retrieve messages for.</param>
    /// <returns>A snapshot list of buffered messages with IDs for the topic, or null if none.</returns>
    IEnumerable<CrowsNestMqtt.Utils.BufferedMqttMessage>? GetBufferedMessagesForTopic(string topic);

    /// <summary>
    /// Retrieves the buffered messages for a specific topic, including their IDs.
    /// </summary>
    /// <param name="topic">The topic to retrieve messages for.</param>
    /// <returns>A snapshot list of buffered messages with IDs for the topic, or null if none.</returns>
    IEnumerable<CrowsNestMqtt.Utils.BufferedMqttMessage>? GetMessagesForTopic(string topic);

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
