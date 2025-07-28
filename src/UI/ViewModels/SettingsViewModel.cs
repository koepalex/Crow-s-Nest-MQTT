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
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.AuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.AnonymousAuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.UsernamePasswordAuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.EnhancedAuthenticationMode))] // Added for AuthMode
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes))]
[JsonSerializable(typeof(Nullable<CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes>))]
[JsonSerializable(typeof(ObservableCollection<TopicBufferLimitViewModel>))]
[JsonSerializable(typeof(TopicBufferLimitViewModel))]
[JsonSerializable(typeof(TopicBufferLimit))]
[JsonSerializable(typeof(IList<TopicBufferLimit>))]
[JsonSerializable(typeof(List<TopicBufferLimit>))] // For deserialization of SettingsData's property
[JsonSerializable(typeof(CrowsNestMqtt.UI.ViewModels.SettingsViewModel.AuthModeSelection))] // Added for enum
public partial class SettingsViewModelJsonContext : JsonSerializerContext
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
        UsernamePassword,
        Enhanced
    }

    internal static string _settingsFilePath = Path.Combine(
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
    private readonly ReadOnlyObservableCollection<AuthModeSelection> _availableAuthenticationModes;
    public ReadOnlyObservableCollection<AuthModeSelection> AvailableAuthenticationModes => _availableAuthenticationModes;

    public ReactiveCommand<Unit, Unit> AddTopicLimitCommand { get; }
    public ReactiveCommand<TopicBufferLimitViewModel, Unit> RemoveTopicLimitCommand { get; }

#pragma warning disable IDE0044 // Add readonly modifier
    private bool _isLoading = false; // Flag to prevent saving during initial load
#pragma warning restore IDE0044 // Add readonly modifier

    private bool _useTls = false;
    public bool UseTls
    {
        get => _useTls;
        set => this.RaiseAndSetIfChanged(ref _useTls, value);
    }

    public SettingsViewModel()
    {
        ExportPath = _exportFolderPath; // Set default before loading
        _isLoading = true; // Set flag before loading
        LoadSettings(); // This calls From() which populates TopicSpecificLimits

        // Ensure a default topic limit if the list is empty
        if (!TopicSpecificLimits.Any())
        {
            TopicSpecificLimits.Add(new TopicBufferLimitViewModel { TopicFilter = "#", MaxSizeBytes = 1024 * 1024 }); // 1MB default
        }
        _isLoading = false; // Clear flag after loading

        AddTopicLimitCommand = ReactiveCommand.Create(() =>
        {
            TopicSpecificLimits.Add(new TopicBufferLimitViewModel { TopicFilter = "new/topic/filter", MaxSizeBytes = 1024 * 1024 });
        });

        RemoveTopicLimitCommand = ReactiveCommand.Create<TopicBufferLimitViewModel>(limit =>
        {
            TopicSpecificLimits.Remove(limit);
        });

        // Observable for simple property changes
        var simplePropertiesChanged = Observable.CombineLatest(
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
            this.WhenAnyValue(x => x.AuthenticationMethod),
            this.WhenAnyValue(x => x.AuthenticationData),
            this.WhenAnyValue(x => x.UseTls),
            (_, _, _, _, _, _, _, _, _, _, _, _, _, _) => Unit.Default);

        // Observable for changes within the TopicSpecificLimits collection (add/remove)
        var collectionChanged = Observable.FromEventPattern<System.Collections.Specialized.NotifyCollectionChangedEventHandler, System.Collections.Specialized.NotifyCollectionChangedEventArgs>(
            h => TopicSpecificLimits.CollectionChanged += h,
            h => TopicSpecificLimits.CollectionChanged -= h)
            .Select(_ => Unit.Default);

        // Observable for changes to properties of items within TopicSpecificLimits
        var itemPropertiesChanged = Observable
            .FromEventPattern<System.Collections.Specialized.NotifyCollectionChangedEventHandler, System.Collections.Specialized.NotifyCollectionChangedEventArgs>(
                h => TopicSpecificLimits.CollectionChanged += h,
                h => TopicSpecificLimits.CollectionChanged -= h)
            .Select(pattern => pattern.EventArgs) // We use the event firing as a trigger
            .StartWith((System.Collections.Specialized.NotifyCollectionChangedEventArgs?)null) // Trigger initially for current items
            .Select(_ => // Invoked when collection changes or initially
            {
                if (!TopicSpecificLimits.Any())
                {
                    return Observable.Empty<Unit>(); // No items, no properties to observe
                }
                // For all items currently in the collection, create an observable that fires when their properties change.
                // Merge these observables.
                return TopicSpecificLimits
                    .Select(item => item.WhenAnyValue(i => i.TopicFilter, i => i.MaxSizeBytes)
                                        .Select(__ => Unit.Default)) // Signal a change
                    .Merge(); // Merge all item property change observables
            })
            .Switch(); // Always use the latest set of merged item observables


        // Merge all change signals
        Observable.Merge(
                simplePropertiesChanged,
                collectionChanged,
                itemPropertiesChanged.StartWith(Unit.Default) // StartWith to ensure initial state is considered if items exist
            )
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(_ => SaveSettings());


        // Populate with enum values
        _availableExportTypes = new ReadOnlyObservableCollection<ExportTypes>(
            new ObservableCollection<ExportTypes>(Enum.GetValues(typeof(ExportTypes)).Cast<ExportTypes>()));
        
        // Set default export path if not loaded
        if (string.IsNullOrEmpty(ExportPath))
        {
            ExportPath = _exportFolderPath;
        }
        _availableAuthenticationModes = new ReadOnlyObservableCollection<AuthModeSelection>(
            new ObservableCollection<AuthModeSelection>
            {
                AuthModeSelection.Anonymous,
                AuthModeSelection.UsernamePassword,
                AuthModeSelection.Enhanced
            });
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
            this.RaisePropertyChanged(nameof(IsUsernamePasswordSelected));
            this.RaisePropertyChanged(nameof(IsEnhancedAuthSelected));
        }
    }

    // Property to control visibility of Username/Password fields in UI
    public bool IsUsernamePasswordSelected => SelectedAuthMode == AuthModeSelection.UsernamePassword;

    public bool IsEnhancedAuthSelected => SelectedAuthMode == AuthModeSelection.Enhanced;

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

    private string? _authenticationMethod;
    public string? AuthenticationMethod
    {
        get => _authenticationMethod;
        set => this.RaiseAndSetIfChanged(ref _authenticationMethod, value);
    }

    private string? _authenticationData;
    public string? AuthenticationData
    {
        get => _authenticationData;
        set => this.RaiseAndSetIfChanged(ref _authenticationData, value);
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
            .Select(vm => new TopicBufferLimit(vm.TopicFilter, vm.MaxSizeBytes))
            .ToList();

        AuthenticationMode authModeSetting;
        string? usernameSetting = null;
        string? passwordSetting = null;

        if (SelectedAuthMode == AuthModeSelection.UsernamePassword)
        {
            authModeSetting = new UsernamePasswordAuthenticationMode(AuthUsername, AuthPassword);
            usernameSetting = AuthUsername;
            passwordSetting = AuthPassword;
        }
        else if (SelectedAuthMode == AuthModeSelection.Enhanced)
        {
            authModeSetting = new EnhancedAuthenticationMode(AuthenticationMethod, AuthenticationData);
        }
        else
        {
            authModeSetting = new AnonymousAuthenticationMode();
        }

        return new SettingsData(
            Hostname,
            Port,
            ClientId,
            KeepAliveIntervalSeconds,
            CleanSession,
            SessionExpiryIntervalSeconds,
            authModeSetting,
            ExportFormat,
            ExportPath,
            UseTls
        )
        {
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
        UseTls = settingsData.UseTls;
        TopicSpecificLimits.Clear();
        if (settingsData.TopicSpecificBufferLimits != null)
        {
            foreach (var limitModel in settingsData.TopicSpecificBufferLimits)
            {
                TopicSpecificLimits.Add(new TopicBufferLimitViewModel(limitModel));
            }
        }

        // Handle AuthMode and credentials
        if (settingsData.AuthMode is EnhancedAuthenticationMode enhancedAuth)
        {
            SelectedAuthMode = AuthModeSelection.Enhanced;
            AuthenticationMethod = enhancedAuth.AuthenticationMethod;
            AuthenticationData = enhancedAuth.AuthenticationData;
            AuthUsername = string.Empty;
            AuthPassword = string.Empty;
        }
        else if (settingsData.AuthMode is UsernamePasswordAuthenticationMode userPassAuth)
        {
            SelectedAuthMode = AuthModeSelection.UsernamePassword;
            AuthUsername = userPassAuth.Username ?? string.Empty;
            AuthPassword = userPassAuth.Password ?? string.Empty;
            AuthenticationMethod = null;
            AuthenticationData = null;
        }
        else // Covers AnonymousAuthenticationMode and null (for older settings if AuthMode wasn't present)
        {
            SelectedAuthMode = AuthModeSelection.Anonymous;
            AuthUsername = string.Empty;
            AuthPassword = string.Empty;
            AuthenticationMethod = null;
            AuthenticationData = null;
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
