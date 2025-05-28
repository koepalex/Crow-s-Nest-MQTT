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
    public AuthenticationMode AuthMode { get; set; } = new AnonymousAuthenticationMode();
}