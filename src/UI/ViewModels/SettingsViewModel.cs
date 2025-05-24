namespace CrowsNestMqtt.UI.ViewModels;

using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.BusinessLogic.Configuration;
using ReactiveUI;
using CrowsNestMqtt.Utils; // For AppLogger
using System;
using System.Collections.ObjectModel;
using System.IO; // For Path, File, Directory
using System.Reactive; // For Unit
using System.Reactive.Linq; // For Observable operators like Throttle
using System.Text.Json; // For JSON serialization
using System.Text.Json.Serialization; // For JsonIgnore
using System.Collections.Generic; // For List<T>
using System.Linq; // For .Select


// Define the JsonSerializerContext for SettingsViewModel and SettingsData
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsViewModel))] // Though we save SettingsData, this might be used elsewhere or for future flexibility
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.SettingsData))]
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes))]
[JsonSerializable(typeof(Nullable<CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes>))]
[JsonSerializable(typeof(ObservableCollection<TopicBufferLimitViewModel>))]
[JsonSerializable(typeof(TopicBufferLimitViewModel))]
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.TopicBufferLimit))]
[JsonSerializable(typeof(IList<CrowsNestMqtt.BusinessLogic.Configuration.TopicBufferLimit>))]
[JsonSerializable(typeof(List<CrowsNestMqtt.BusinessLogic.Configuration.TopicBufferLimit>))] // For deserialization of SettingsData's property
internal partial class SettingsViewModelJsonContext : JsonSerializerContext
{
}

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

    public ObservableCollection<TopicBufferLimitViewModel> TopicSpecificLimits { get; } = new();

#pragma warning disable IDE0044 // Add readonly modifier
    private bool _isLoading = false; // Flag to prevent saving during initial load
#pragma warning restore IDE0044 // Add readonly modifier

    public SettingsViewModel()
    {
        _isLoading = true; // Set flag before loading
        LoadSettings(); // This calls From() which populates TopicSpecificLimits
        _isLoading = false; // Clear flag after loading

        // Auto-save settings when properties change (with throttling)
        var basicPropertiesChanged = Observable.CombineLatest(
            this.WhenAnyValue(x => x.Hostname),
            this.WhenAnyValue(x => x.Port),
            this.WhenAnyValue(x => x.ClientId),
            this.WhenAnyValue(x => x.KeepAliveIntervalSeconds),
            this.WhenAnyValue(x => x.CleanSession),
            this.WhenAnyValue(x => x.SessionExpiryIntervalSeconds),
            this.WhenAnyValue(x => x.ExportFormat),
            this.WhenAnyValue(x => x.ExportPath),
            (_, _, _, _, _, _, _, _) => Unit.Default);

        // Watch for changes in the collection itself (add/remove)
        var collectionChanged = this.WhenAnyValue(x => x.TopicSpecificLimits)
            .Select(_ => Unit.Default); // We just need a trigger

        // Watch for changes within any item in the collection
        var itemPropertiesChanged = this.WhenAnyObservable(x => x.TopicSpecificLimits.ItemChanged)
            .Select(_ => Unit.Default); // We just need a trigger

        Observable.Merge(basicPropertiesChanged, collectionChanged, itemPropertiesChanged)
            .Throttle(TimeSpan.FromMilliseconds(500)) // Wait 500ms after the last change
            .ObserveOn(RxApp.TaskpoolScheduler) // Perform save on a background thread
            .Subscribe(_ => SaveSettings());

        // Populate with enum values
        _availableExportTypes = new ReadOnlyObservableCollection<ExportTypes>(
            new ObservableCollection<ExportTypes>(Enum.GetValues(typeof(ExportTypes)).Cast<ExportTypes>()));
        
        // Set default export path if not loaded
        if (string.IsNullOrEmpty(ExportPath))
        {
            ExportPath = _exportFolderPath;
        }
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
        var topicLimits = TopicSpecificLimits
            .Select(vm => new TopicBufferLimit { TopicFilter = vm.TopicFilter, MaxSizeBytes = vm.MaxSizeBytes })
            .ToList();

        return new SettingsData(
            Hostname: Hostname,
            Port: Port,
            ClientId: ClientId,
            KeepAliveIntervalSeconds: KeepAliveIntervalSeconds,
            CleanSession: CleanSession,
            SessionExpiryIntervalSeconds: SessionExpiryIntervalSeconds,
            ExportFormat: ExportFormat,
            ExportPath: ExportPath
        ) // Call to primary constructor
        { 
            // Use object initializer for the new property
            TopicSpecificBufferLimits = topicLimits 
        };
    }

    public void From(SettingsData settingsData)
    {
        Hostname = settingsData.Hostname;
        Port = settingsData.Port;
        ClientId = settingsData.ClientId;
        KeepAliveIntervalSeconds = settingsData.KeepAliveIntervalSeconds;
        CleanSession = settingsData.CleanSession;
        SessionExpiryIntervalSeconds = settingsData.SessionExpiryIntervalSeconds;
        ExportFormat = settingsData.ExportFormat; 
        ExportPath = settingsData.ExportPath;
        
        TopicSpecificLimits.Clear();
        if (settingsData.TopicSpecificBufferLimits != null)
        {
            foreach (var limitModel in settingsData.TopicSpecificBufferLimits)
            {
                TopicSpecificLimits.Add(new TopicBufferLimitViewModel(limitModel));
            }
        }
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
                AppLogger.Information("Created settings directory: {Directory}", directory);
            }

            // Get the SettingsData model from the ViewModel
            SettingsData dataToSave = this.Into();
            
            // Use the generated context for serializing SettingsData
            string json = JsonSerializer.Serialize(dataToSave, SettingsViewModelJsonContext.Default.SettingsData);
            File.WriteAllText(_settingsFilePath, json);
            AppLogger.Information("Settings saved to {FilePath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error saving settings to {FilePath}", _settingsFilePath);
        }
    }
    
    private void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            AppLogger.Warning("Settings file not found at {FilePath}. Using defaults.", _settingsFilePath);
            // Ensure default export path is set if settings file doesn't exist
            if (string.IsNullOrEmpty(ExportPath)) ExportPath = _exportFolderPath;
            return; // Use default values if file doesn't exist
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            var loadedData = JsonSerializer.Deserialize(json, SettingsViewModelJsonContext.Default.SettingsData);

            if (loadedData != null)
            {
                From(loadedData); // This now also populates TopicSpecificLimits
                AppLogger.Information("Settings loaded from {FilePath}", _settingsFilePath);
            }
            else
            {
                 AppLogger.Warning("Failed to deserialize settings from {FilePath}. Using defaults.", _settingsFilePath);
                 if (string.IsNullOrEmpty(ExportPath)) ExportPath = _exportFolderPath;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error loading settings from {FilePath}", _settingsFilePath);
            // Keep default values if loading fails, ensure default export path
            if (string.IsNullOrEmpty(ExportPath)) ExportPath = _exportFolderPath;
        }
    }
}