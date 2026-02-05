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
    
    /// <summary>
    /// A user-friendly error message to display to the customer.
    /// </summary>
    public string? ErrorMessage { get; }

    public MqttConnectionStateChangedEventArgs(bool isConnected, Exception? error, ConnectionStatusState status, string? reconnectInfo = null, string? errorMessage = null)
    {
        IsConnected = isConnected;
        Error = error;
        ConnectionStatus = status;
        ReconnectInfo = reconnectInfo;
        ErrorMessage = errorMessage;
    }
}
