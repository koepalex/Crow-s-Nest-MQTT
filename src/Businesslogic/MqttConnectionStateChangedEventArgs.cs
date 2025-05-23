namespace CrowsNestMqtt.BusinessLogic;

/// <summary>
/// Provides data for the ConnectionStateChanged event.
/// </summary>
public class MqttConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public Exception? Error { get; }

    public MqttConnectionStateChangedEventArgs(bool isConnected, Exception? error)
    {
        IsConnected = isConnected;
        Error = error;
    }
}