using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using MQTTnet;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Factory for creating MessageViewModel instances from MQTT messages.
/// Handles preview generation, correlation tracking, and view model construction.
/// </summary>
public interface IMessageViewModelFactory
{
    /// <summary>
    /// Creates a MessageViewModel from an identified MQTT message.
    /// </summary>
    /// <param name="message">The MQTT message with identification</param>
    /// <param name="mqttService">The MQTT service for message operations</param>
    /// <param name="statusBarService">The status bar service for user feedback</param>
    /// <returns>A configured MessageViewModel</returns>
    MessageViewModel CreateMessageViewModel(
        IdentifiedMqttApplicationMessageReceivedEventArgs message,
        IMqttService mqttService,
        IStatusBarService statusBarService);

    /// <summary>
    /// Generates a preview string from an MQTT payload.
    /// </summary>
    /// <param name="payload">The message payload bytes</param>
    /// <param name="maxLength">Maximum preview length (default 100)</param>
    /// <returns>Preview string, potentially truncated</returns>
    string GeneratePayloadPreview(byte[] payload, int maxLength = 100);

    /// <summary>
    /// Registers request-response correlation for MQTT V5 messages.
    /// </summary>
    /// <param name="messageId">The message identifier</param>
    /// <param name="message">The MQTT application message</param>
    /// <param name="topic">The message topic</param>
    /// <returns>True if correlation was registered or linked successfully</returns>
    Task<bool> RegisterCorrelationAsync(Guid messageId, MqttApplicationMessage message, string topic);
}
