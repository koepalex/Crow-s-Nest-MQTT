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


// Define the JsonSerializerContext for SettingsViewModel and SettingsData
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(SettingsViewModel))]
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.SettingsData))]
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.AuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.AnonymousAuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.UsernamePasswordAuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes))]
[JsonSerializable(typeof(Nullable<CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes>))]
[JsonSerializable(typeof(CrowsNestMqtt.UI.ViewModels.SettingsViewModel.AuthModeSelection))] // Added for enum
internal partial class SettingsViewModelJsonContext : JsonSerializerContext
{
}

/// <summary>
/// ViewModel for MQTT connection settings.
/// </summary>
public class SettingsViewModel : ReactiveObject
{
    // Enum for UI selection of authentication mode
    public enum AuthModeSelection
    {
        Anonymous,
        UsernamePassword
    }

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

    private readonly ReadOnlyObservableCollection<AuthModeSelection> _availableAuthenticationModes;
    public ReadOnlyObservableCollection<AuthModeSelection> AvailableAuthenticationModes => _availableAuthenticationModes; 

#pragma warning disable IDE0044 // Add readonly modifier
    private bool _isLoading = false; // Flag to prevent saving during initial load
#pragma warning restore IDE0044 // Add readonly modifier

    public SettingsViewModel()
    {
        _isLoading = true; // Set flag before loading
        LoadSettings();
        _isLoading = false; // Clear flag after loading

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
                this.WhenAnyValue(x => x.SelectedAuthMode),
                this.WhenAnyValue(x => x.AuthUsername),
                this.WhenAnyValue(x => x.AuthPassword),
                (_, _, _, _, _, _, _, _, _, _, _) => Unit.Default) // Adjusted lambda parameters
            .Throttle(TimeSpan.FromMilliseconds(500)) // Wait 500ms after the last change
            .ObserveOn(RxApp.TaskpoolScheduler) // Perform save on a background thread
            .Subscribe(_ => SaveSettings());

        // Populate with enum values
        _availableExportTypes = new ReadOnlyObservableCollection<ExportTypes>(
            new ObservableCollection<ExportTypes>(Enum.GetValues(typeof(ExportTypes)).Cast<ExportTypes>()));
        _availableAuthenticationModes = new ReadOnlyObservableCollection<AuthModeSelection>(
            new ObservableCollection<AuthModeSelection>(Enum.GetValues(typeof(AuthModeSelection)).Cast<AuthModeSelection>()));
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

    // New properties for AuthMode selection
    private AuthModeSelection _selectedAuthMode = AuthModeSelection.Anonymous;
    public AuthModeSelection SelectedAuthMode
    {
        get => _selectedAuthMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedAuthMode, value);
            this.RaisePropertyChanged(nameof(IsUsernamePasswordSelected)); // Notify that the dependent property has changed
        }
    }

    // Property to control visibility of Username/Password fields in UI
    public bool IsUsernamePasswordSelected => SelectedAuthMode == AuthModeSelection.UsernamePassword;

    private string _authUsername = string.Empty;
    public string AuthUsername
    {
        get => _authUsername;
        set => this.RaiseAndSetIfChanged(ref _authUsername, value);
    }

    private string _authPassword = string.Empty;
    public string AuthPassword
    {
        get => _authPassword;
        set => this.RaiseAndSetIfChanged(ref _authPassword, value);
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
        AuthenticationMode authModeSetting;
        string? usernameSetting = null;
        string? passwordSetting = null;

        if (SelectedAuthMode == AuthModeSelection.UsernamePassword)
        {
            authModeSetting = new UsernamePasswordAuthenticationMode(AuthUsername, AuthPassword);
            usernameSetting = AuthUsername;
            passwordSetting = AuthPassword;
        }
        else
        {
            authModeSetting = new AnonymousAuthenticationMode();
            // usernameSetting and passwordSetting remain null for Anonymous mode
        }

        return new SettingsData(
            Hostname,
            Port,
            ClientId,
            KeepAliveIntervalSeconds,
            CleanSession,
            SessionExpiryIntervalSeconds,
            // usernameSetting, // Removed
            // passwordSetting, // Removed
            authModeSetting, 
            ExportFormat,
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
        ExportFormat = settingsData.ExportFormat;
        ExportPath = settingsData.ExportPath;

        // Handle AuthMode and credentials
        if (settingsData.AuthMode is UsernamePasswordAuthenticationMode userPassAuth)
        {
            SelectedAuthMode = AuthModeSelection.UsernamePassword;
            AuthUsername = userPassAuth.Username ?? string.Empty;
            AuthPassword = userPassAuth.Password ?? string.Empty;
        }
        else // Covers AnonymousAuthenticationMode and null (for older settings if AuthMode wasn't present)
        {
            SelectedAuthMode = AuthModeSelection.Anonymous;
            AuthUsername = string.Empty;
            AuthPassword = string.Empty;
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

            // Use the generated context for serialization
            string json = JsonSerializer.Serialize(this.Into(), SettingsViewModelJsonContext.Default.SettingsData);
            File.WriteAllText(_settingsFilePath, json);
            AppLogger.Information("Settings saved to {FilePath}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error saving settings to {FilePath}", _settingsFilePath);
        }
    }

    // Define a simple record to hold settings data for deserialization
    // This avoids recursive constructor calls during deserialization.
    

    private void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            AppLogger.Warning("Settings file not found at {FilePath}. Using defaults.", _settingsFilePath);
            return; // Use default values if file doesn't exist
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            // Deserialize into the temporary SettingsData record using the generated context
            var loadedData = JsonSerializer.Deserialize(json, SettingsViewModelJsonContext.Default.SettingsData);

            if (loadedData != null)
            {
                // Copy values from the loaded data to the current ViewModel instance
                From(loadedData);

                AppLogger.Information("Settings loaded from {FilePath}", _settingsFilePath);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error loading settings from {FilePath}", _settingsFilePath);
            // Keep default values if loading fails
        }
    }
}
