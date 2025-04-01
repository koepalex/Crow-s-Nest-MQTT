using ReactiveUI;
using System;

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for MQTT connection settings.
/// </summary>
public class SettingsViewModel : ReactiveObject
{
    private string _hostname = "localhost";
    public string Hostname
    {
        get => _hostname;
        set => this.RaiseAndSetIfChanged(ref _hostname, value);
    }

    private int _port = 1883;
    public int Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    private string? _clientId; // Null or empty means MQTTnet generates one
    public string? ClientId
    {
        get => _clientId;
        set => this.RaiseAndSetIfChanged(ref _clientId, value);
    }

    private TimeSpan _keepAliveInterval = TimeSpan.FromSeconds(60);
    public int KeepAliveIntervalSeconds // Use int for easier binding with NumericUpDown
    {
        get => (int)_keepAliveInterval.TotalSeconds;
        set => this.RaiseAndSetIfChanged(ref _keepAliveInterval, TimeSpan.FromSeconds(value));
    }
    public TimeSpan KeepAliveInterval => _keepAliveInterval; // Expose TimeSpan for engine

    private bool _cleanSession = true;
    public bool CleanSession
    {
        get => _cleanSession;
        set => this.RaiseAndSetIfChanged(ref _cleanSession, value);
    }

    private uint? _sessionExpiryInterval; // Null means session never expires (if CleanSession=false)
    public uint? SessionExpiryIntervalSeconds // Use uint? for binding
    {
        get => _sessionExpiryInterval;
        set => this.RaiseAndSetIfChanged(ref _sessionExpiryInterval, value);
    }
     public uint? SessionExpiryInterval => _sessionExpiryInterval; // Expose for engine
}