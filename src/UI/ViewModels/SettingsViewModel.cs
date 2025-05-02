namespace CrowsNestMqtt.UI.ViewModels;

using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.BusinessLogic.Configuration;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.IO; // For Path, File, Directory
using System.Reactive; // For Unit
using System.Reactive.Linq; // For Observable operators like Throttle
using System.Text.Json; // For JSON serialization
using System.Text.Json.Serialization; // For JsonIgnore


/// <summary>
/// ViewModel for MQTT connection settings.
/// </summary>
public class SettingsViewModel : ReactiveObject
{
    private static readonly string _settingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrowsNestMqtt", 
        "settings.json");

    private static readonly string _exportFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CrowsNestMqtt", 
        "exports");

    // Renamed _formatEncodings to _availableExportTypes for clarity
    private readonly ReadOnlyObservableCollection<ExportTypes> _availableExportTypes;
    public ReadOnlyObservableCollection<ExportTypes> AvailableExportTypes => _availableExportTypes; // Changed type and name

#pragma warning disable IDE0044 // Add readonly modifier
    private bool _isLoading = false; // Flag to prevent saving during initial load
#pragma warning restore IDE0044 // Add readonly modifier

    public SettingsViewModel()
    {
        _isLoading = true; // Set flag before loading
        LoadSettings();
        _isLoading = false; // Clear flag after loading

        // Auto-save settings when properties change (with throttling)
        // Auto-save settings when properties change (with throttling)
        // Use CombineLatest for robustness with multiple properties
        Observable.CombineLatest(
                this.WhenAnyValue(x => x.Hostname),
                this.WhenAnyValue(x => x.Port),
                this.WhenAnyValue(x => x.ClientId),
                this.WhenAnyValue(x => x.KeepAliveIntervalSeconds),
                this.WhenAnyValue(x => x.CleanSession),
                this.WhenAnyValue(x => x.SessionExpiryIntervalSeconds),
                this.WhenAnyValue(x => x.ExportFormat),
                this.WhenAnyValue(x => x.ExportPath),
                (_, _, _, _, _, _, _, _) => Unit.Default) // Combine results, we only care about the trigger
            .Throttle(TimeSpan.FromMilliseconds(500)) // Wait 500ms after the last change
            .ObserveOn(RxApp.TaskpoolScheduler) // Perform save on a background thread
            .Subscribe(_ => SaveSettings());

        // Populate with enum values
        _availableExportTypes = new ReadOnlyObservableCollection<ExportTypes>(
            new ObservableCollection<ExportTypes>(Enum.GetValues(typeof(ExportTypes)).Cast<ExportTypes>()));
        ExportPath = _exportFolderPath;
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

   private ExportTypes? _exportFormat = ExportTypes.json; // Changed type to ExportTypes? and set default
   public ExportTypes? ExportFormat
   {
       get => _exportFormat;
       set => this.RaiseAndSetIfChanged(ref _exportFormat, value);
   }

   private string? _exportPath;
   public string? ExportPath
   {
       get => _exportPath;
       set => this.RaiseAndSetIfChanged(ref _exportPath, value);
   }
    public SettingsData Into()
    {
        return new SettingsData(
            Hostname,
            Port,
            ClientId,
            KeepAliveIntervalSeconds,
            CleanSession,
            SessionExpiryIntervalSeconds,
            ExportFormat, // Type is now ExportTypes?
            ExportPath    
        );
    }

    public void From(SettingsData settingsData)
    {
        Hostname = settingsData.Hostname;
        Port = settingsData.Port;
        ClientId = settingsData.ClientId;
        KeepAliveIntervalSeconds = settingsData.KeepAliveIntervalSeconds;
        CleanSession = settingsData.CleanSession;
        SessionExpiryIntervalSeconds = settingsData.SessionExpiryIntervalSeconds;
        ExportFormat = settingsData.ExportFormat; // Type is now ExportTypes?
        ExportPath = settingsData.ExportPath;       // Added
    }

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
                Log.Information("Created settings directory: {Directory}", directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(_settingsFilePath, json);
            Log.Information("Settings saved to {FilePath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving settings to {FilePath}", _settingsFilePath);
        }
    }

    // Define a simple record to hold settings data for deserialization
    // This avoids recursive constructor calls during deserialization.
    

    private void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            Log.Warning("Settings file not found at {FilePath}. Using defaults.", _settingsFilePath);
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
                From(loadedData);

                Log.Information("Settings loaded from {FilePath}", _settingsFilePath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading settings from {FilePath}", _settingsFilePath);
            // Keep default values if loading fails
        }
    }
}