namespace CrowsNestMqtt.BusinessLogic;

/// <summary>
/// Provides data for the ConnectionStateChanged event.
/// </summary>
public class MqttConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public Exception? Error { get; }
    
    public ConnectionStatusState ConnectionStatus { get; }

    public string? ReconnectInfo { get; }

    public MqttConnectionStateChangedEventArgs(bool isConnected, Exception? error, ConnectionStatusState status, string? reconnectInfo = null)
    {
        IsConnected = isConnected;
        Error = error;
        ConnectionStatus = status;
        ReconnectInfo = reconnectInfo;
    }
}
