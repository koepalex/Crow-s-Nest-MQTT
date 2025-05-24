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
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(60);
    public bool CleanSession { get; set; } = true;
    public uint? SessionExpiryInterval { get; set; } = 0;// Null means session never expires
    public IList<TopicBufferLimit> TopicSpecificBufferLimits { get; set; } = new List<TopicBufferLimit>();

    // Consider adding properties for TLS, Credentials, etc. later
}