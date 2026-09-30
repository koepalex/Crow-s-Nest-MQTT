namespace CrowsNestMqtt.BusinessLogic;

using System.Collections.Generic;
using CrowsNestMqtt.BusinessLogic.Configuration;


/// <summary>
/// Holds configuration settings for establishing an MQTT connection.
/// This class is independent of the UI layer.
/// </summary>
public class MqttConnectionSettings
{
    public string Hostname { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public string? ClientId { get; set; } // Null or empty means MQTTnet generates one
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(10);
    public bool CleanSession { get; set; } = true;
    public uint? SessionExpiryInterval { get; set; } = 3600;
    public IList<TopicBufferLimit> TopicSpecificBufferLimits { get; set; } = new List<TopicBufferLimit>();
    /// <summary>
    /// Default buffer size in bytes for topics that don't match any specific rules.
    /// If null, uses the system default (1 MB). This only applies when no "#" wildcard rule is configured.
    /// </summary>
    public long? DefaultTopicBufferSizeBytes { get; set; }
    public AuthenticationMode AuthMode { get; set; } = new AnonymousAuthenticationMode();
    public bool UseTls { get; set; }
    /// <summary>
    /// Transport protocol to use for the MQTT connection. Default is TCP.
    /// </summary>
    public TransportProtocol Transport { get; set; } = TransportProtocol.Tcp;
    /// <summary>
    /// WebSocket path when using WebSocket transport. Defaults to "/mqtt" if not specified.
    /// Only used when <see cref="Transport"/> is <see cref="TransportProtocol.WebSocket"/>.
    /// </summary>
    public string? WebSocketPath { get; set; }
    /// <summary>
    /// HTTP or HTTPS forward proxy used for WebSocket connections.
    /// </summary>
    public string? WebSocketProxyAddress { get; set; }
    public string? WebSocketProxyUsername { get; set; }
    public string? WebSocketProxyPassword { get; set; }
    /// <summary>
    /// QoS level for the wildcard subscription. Default is 1 (AtLeastOnce).
    /// Set to 2 (ExactlyOnce) if you need to receive QoS 2 messages without downgrade.
    /// Higher QoS reduces maximum message throughput.
    /// </summary>
    public int SubscriptionQoS { get; set; } = 1;
    /// <summary>
    /// MQTT topic filter used for the client's single startup subscription.
    /// Defaults to <c>#</c> (all topics) which works for permissive brokers like EMQX
    /// or Mosquitto. Azure Event Grid namespaces reject <c>#</c> — set this to a
    /// filter that fits inside your Topic Space template (e.g. <c>sensors/#</c>).
    /// </summary>
    public string SubscriptionTopic { get; set; } = "#";
}
