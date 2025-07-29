namespace CrowsNestMqtt.BusinessLogic;

/// <summary>
/// Represents the different states of the MQTT connection.
/// </summary>
public enum ConnectionStatusState
{
    /// <summary>
    /// The client is disconnected.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The client is attempting to connect.
    /// </summary>
    Connecting,

    /// <summary>
    /// The client is connected.
    /// </summary>
    Connected
}
