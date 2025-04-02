using ReactiveUI;
using System;
using System.IO; // For Path, File, Directory
using System.Reactive.Linq; // For Observable operators like Throttle
using System.Text.Json; // For JSON serialization
using System.Text.Json.Serialization; // For JsonIgnore

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// ViewModel for MQTT connection settings.
/// </summary>
public class SettingsViewModel : ReactiveObject
{
    private static readonly string _settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrowsNestMqtt", // Application-specific folder
        "settings.json");

    private bool _isLoading = false; // Flag to prevent saving during initial load

    public SettingsViewModel()
    {
        _isLoading = true; // Set flag before loading
        LoadSettings();
        _isLoading = false; // Clear flag after loading

        // Auto-save settings when properties change (with throttling)
        this.WhenAnyValue(
                x => x.Hostname,
                x => x.Port,
                x => x.ClientId,
                x => x.KeepAliveIntervalSeconds,
                x => x.CleanSession,
                x => x.SessionExpiryIntervalSeconds)
            .Throttle(TimeSpan.FromMilliseconds(500)) // Wait 500ms after the last change
            .ObserveOn(RxApp.TaskpoolScheduler) // Perform save on a background thread
            .Subscribe(_ => SaveSettings());
    }
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
    [JsonIgnore] // Don't serialize the derived TimeSpan property
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
     [JsonIgnore] // Don't serialize the derived uint? property
     public uint? SessionExpiryInterval => _sessionExpiryInterval; // Expose for engine

    // --- Persistence Methods ---

    private void SaveSettings()
    {
        if (_isLoading) return; // Don't save while initially loading

        try
        {
            string? directory = Path.GetDirectoryName(_settingsFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Console.WriteLine($"Created settings directory: {directory}");
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(_settingsFilePath, json);
            Console.WriteLine($"Settings saved to {_settingsFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
            // TODO: Log this error more formally
        }
    }

    // Define a simple record to hold settings data for deserialization
    // This avoids recursive constructor calls during deserialization.
    private record SettingsData(
        string Hostname,
        int Port,
        string? ClientId,
        int KeepAliveIntervalSeconds,
        bool CleanSession,
        uint? SessionExpiryIntervalSeconds
    );

    private void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            Console.WriteLine($"Settings file not found at {_settingsFilePath}. Using defaults.");
            return; // Use default values if file doesn't exist
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            // Deserialize into the temporary SettingsData record
            var loadedData = JsonSerializer.Deserialize<SettingsData>(json);

            if (loadedData != null)
            {
                // Copy values from the loaded data to the current ViewModel instance
                this.Hostname = loadedData.Hostname;
                this.Port = loadedData.Port;
                this.ClientId = loadedData.ClientId;
                this.KeepAliveIntervalSeconds = loadedData.KeepAliveIntervalSeconds;
                this.CleanSession = loadedData.CleanSession;
                this.SessionExpiryIntervalSeconds = loadedData.SessionExpiryIntervalSeconds;

                Console.WriteLine($"Settings loaded from {_settingsFilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            // TODO: Log this error more formally
            // Keep default values if loading fails
        }
    }
}