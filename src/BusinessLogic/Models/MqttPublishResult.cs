using MQTTnet;

namespace CrowsNestMqtt.BusinessLogic.Models;

/// <summary>
/// Result of an MQTT publish operation.
/// </summary>
public record MqttPublishResult
{
    /// <summary>
    /// Whether the publish operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The MQTT reason code returned by the broker.
    /// </summary>
    public MqttClientPublishReasonCode? ReasonCode { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The topic the message was published to.
    /// </summary>
    public string? Topic { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static MqttPublishResult Succeeded(string topic, MqttClientPublishReasonCode reasonCode) =>
        new() { Success = true, Topic = topic, ReasonCode = reasonCode };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static MqttPublishResult Failed(string topic, string errorMessage, MqttClientPublishReasonCode? reasonCode = null) =>
        new() { Success = false, Topic = topic, ErrorMessage = errorMessage, ReasonCode = reasonCode };
}
