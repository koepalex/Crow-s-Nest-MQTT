namespace CrowsNestMqtt.UI.ViewModels;

using CrowsNestMqtt.BusinessLogic.Exporter;
using CrowsNestMqtt.BusinessLogic.Configuration;
using CrowsNestMqtt.BusinessLogic.Services;
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
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.AzureAuthenticationMode))] // Added for Azure Event Grid auth
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes))]
[JsonSerializable(typeof(Nullable<CrowsNestMqtt.BusinessLogic.Exporter.ExportTypes>))]
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.TransportProtocol))]
[JsonSerializable(typeof(ObservableCollection<TopicBufferLimitViewModel>))]
[JsonSerializable(typeof(TopicBufferLimitViewModel))]
[JsonSerializable(typeof(TopicBufferLimit))]
[JsonSerializable(typeof(IList<TopicBufferLimit>))]
[JsonSerializable(typeof(List<TopicBufferLimit>))] // For deserialization of SettingsData's property
[JsonSerializable(typeof(CrowsNestMqtt.UI.ViewModels.SettingsViewModel.AuthModeSelection))] // Added for enum
[JsonSerializable(typeof(CrowsNestMqtt.BusinessLogic.Configuration.AppTheme))]
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
        Enhanced,
        Azure
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

    private readonly ReadOnlyObservableCollection<AppTheme> _availableThemes;
    public ReadOnlyObservableCollection<AppTheme> AvailableThemes => _availableThemes;

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

    private TransportProtocol _selectedTransport = TransportProtocol.Tcp;
    public TransportProtocol SelectedTransport
    {
        get => _selectedTransport;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTransport, value);
            this.RaisePropertyChanged(nameof(IsWebSocketSelected));
        }
    }

    /// <summary>
    /// Whether WebSocket transport is currently selected (for UI visibility binding).
    /// </summary>
    public bool IsWebSocketSelected => SelectedTransport == TransportProtocol.WebSocket;

    private readonly ReadOnlyObservableCollection<TransportProtocol> _availableTransports;
    public ReadOnlyObservableCollection<TransportProtocol> AvailableTransports => _availableTransports;

    private string? _webSocketPath;
    /// <summary>
    /// WebSocket path (e.g., "/mqtt"). Only used when transport is WebSocket.
    /// </summary>
    public string? WebSocketPath
    {
        get => _webSocketPath;
        set => this.RaiseAndSetIfChanged(ref _webSocketPath, value);
    }

    private string? _webSocketProxyAddress;
    public string? WebSocketProxyAddress
    {
        get => _webSocketProxyAddress;
        set => this.RaiseAndSetIfChanged(ref _webSocketProxyAddress, value);
    }

    private string? _webSocketProxyUsername;
    public string? WebSocketProxyUsername
    {
        get => _webSocketProxyUsername;
        set => this.RaiseAndSetIfChanged(ref _webSocketProxyUsername, value);
    }

    private string? _webSocketProxyPassword;
    public string? WebSocketProxyPassword
    {
        get => _webSocketProxyPassword;
        set => this.RaiseAndSetIfChanged(ref _webSocketProxyPassword, value);
    }

    private int _subscriptionQoS = 1;
    /// <summary>
    /// QoS level for the wildcard subscription (0, 1, or 2). Default: 1 (AtLeastOnce).
    /// Set to 2 to receive QoS 2 messages without downgrade. Higher QoS reduces throughput.
    /// </summary>
    public int SubscriptionQoS
    {
        get => _subscriptionQoS;
        set => this.RaiseAndSetIfChanged(ref _subscriptionQoS, Math.Clamp(value, 0, 2));
    }

private bool _showConnectionDialogOnLaunch = true;
/// <summary>
/// Whether to show the connection dialog when the application starts. Default: true.
/// </summary>
public bool ShowConnectionDialogOnLaunch
{
    get => _showConnectionDialogOnLaunch;
    set => this.RaiseAndSetIfChanged(ref _showConnectionDialogOnLaunch, value);
}

private AppTheme _theme = AppTheme.System;
public AppTheme Theme
{
    get => _theme;
    set => this.RaiseAndSetIfChanged(ref _theme, value);
}

private string _subscriptionTopic = "#";
    private readonly bool _isAspireEnvironment;
    /// <summary>
    /// MQTT topic filter used for the initial subscription. Defaults to <c>#</c>
    /// (all topics), which works for permissive brokers like EMQX/Mosquitto.
    /// Azure Event Grid namespaces reject <c>#</c> — set this to a filter that
    /// matches your Topic Space template (e.g. <c>sensors/#</c>).
    /// </summary>
    public string SubscriptionTopic
    {
        get => _subscriptionTopic;
        set => this.RaiseAndSetIfChanged(
            ref _subscriptionTopic,
            string.IsNullOrWhiteSpace(value) ? "#" : value);
    }

    public SettingsViewModel(EnvironmentSettingsOverrides? environmentOverrides = null)
    {
        _isAspireEnvironment = environmentOverrides?.IsAspireEnvironment == true;
        ExportPath = _exportFolderPath; // Set default before loading
        _isLoading = true; // Set flag before loading
        if (_isAspireEnvironment)
        {
            EnsureDefaultTopicLimit();
        }
        else
        {
            LoadSettings(); // This calls From() which populates TopicSpecificLimits
        }

        // Apply environment variable overrides after loading from file
        if (environmentOverrides?.HasOverrides == true)
        {
            ApplyEnvironmentOverrides(environmentOverrides);
        }

        _isLoading = false; // Clear flag after loading

        AddTopicLimitCommand = ReactiveCommand.Create(() =>
        {
            TopicSpecificLimits.Add(new TopicBufferLimitViewModel { TopicFilter = "new/topic/filter", MaxSizeBytes = 1024 * 1024 });
        });

        RemoveTopicLimitCommand = ReactiveCommand.Create<TopicBufferLimitViewModel>(limit =>
        {
            // Prevent removal of the default '#' limit
            if (limit.CanBeRemoved)
            {
                TopicSpecificLimits.Remove(limit);
            }
        });

        // Observable for simple property changes
        var simplePropertiesChanged = Observable.Merge(
            this.WhenAnyValue(x => x.Hostname).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.Port).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.ClientId).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.KeepAliveIntervalSeconds).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.CleanSession).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.SessionExpiryIntervalSeconds).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.ExportFormat).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.ExportPath).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.SelectedAuthMode).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.AuthUsername).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.AuthPassword).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.AuthenticationMethod).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.AuthenticationData).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.UseTls).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.SubscriptionQoS).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.ShowConnectionDialogOnLaunch).Select(_ => Unit.Default),
            this.WhenAnyValue(x => x.Theme).Select(_ => Unit.Default));

        // Observable for transport-related property changes (and Azure scope /
        // subscription topic, kept here because the simplePropertiesChanged
        // CombineLatest already hit its 15-arg cap)
        var transportPropertiesChanged = Observable.CombineLatest(
            this.WhenAnyValue(x => x.SelectedTransport),
            this.WhenAnyValue(x => x.WebSocketPath),
            this.WhenAnyValue(x => x.WebSocketProxyAddress),
            this.WhenAnyValue(x => x.WebSocketProxyUsername),
            this.WhenAnyValue(x => x.WebSocketProxyPassword),
            this.WhenAnyValue(x => x.AuthenticationScope),
            this.WhenAnyValue(x => x.SubscriptionTopic),
            (_, _, _, _, _, _, _) => Unit.Default);

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
                transportPropertiesChanged,
                collectionChanged,
                itemPropertiesChanged.StartWith(Unit.Default) // StartWith to ensure initial state is considered if items exist
            )
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxSchedulers.TaskpoolScheduler)
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
                AuthModeSelection.Enhanced,
                AuthModeSelection.Azure
            });
        _availableThemes = new ReadOnlyObservableCollection<AppTheme>(
            new ObservableCollection<AppTheme>(Enum.GetValues(typeof(AppTheme)).Cast<AppTheme>()));

        _availableTransports = new ReadOnlyObservableCollection<TransportProtocol>(
            new ObservableCollection<TransportProtocol>(Enum.GetValues(typeof(TransportProtocol)).Cast<TransportProtocol>()));
    }

    /// <summary>
    /// Fired when the hostname setter normalizes user input in a non-trivial
    /// way (e.g. strips a scheme, extracts a port, rewrites the Event Grid
    /// suffix). Consumers surface the notes to the user via the status bar.
    /// The event is not raised during load-from-file / env-override phases.
    /// </summary>
    public event EventHandler<HostnameNormalizedEventArgs>? HostnameNormalized;

    private string _hostname = "localhost";
    public string Hostname
    {
        get => _hostname;
        set
        {
            // Coerce URL-shaped inputs (https://…/api/events), extract inline
            // ports, and rewrite the Event Grid HTTP suffix to the MQTT topic-
            // space suffix. This runs every time the property is assigned —
            // including when the user types into the textbox — so the value in
            // the ViewModel (and therefore the bound control) always reflects
            // what MQTTnet actually needs.
            var normalized = MqttHostnameNormalizer.Normalize(value);

            var backingChanged = !string.Equals(_hostname, normalized.Hostname, StringComparison.Ordinal);
            var rawInputDiffered = normalized.WasChanged;

            _hostname = normalized.Hostname;

            // ALWAYS raise PropertyChanged when the raw user input differed from
            // the normalized result, even if the backing field didn't actually
            // change (e.g. the user pasted the same URL twice). Otherwise the
            // bound TextBox keeps displaying the raw input because the two-way
            // binding never sees a source update to overwrite its local buffer.
            if (backingChanged || rawInputDiffered)
            {
                this.RaisePropertyChanged(nameof(Hostname));
            }

            if (normalized.Port is int extractedPort)
            {
                Port = extractedPort;
            }

            if (!_isLoading && rawInputDiffered)
            {
                HostnameNormalized?.Invoke(this, new HostnameNormalizedEventArgs(
                    original: value ?? string.Empty,
                    cleaned: normalized.Hostname,
                    extractedPort: normalized.Port,
                    notes: normalized.Notes));
            }
        }
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
            this.RaisePropertyChanged(nameof(IsAzureAuthSelected));

            // Azure Event Grid namespaces require TLS on TCP port 8883 with MQTT v5.
            // Auto-apply those values so a user picking Azure interactively doesn't
            // need to remember the prerequisites. Skipped during LoadSettings /
            // From / ApplyEnvironmentOverrides so file- or env-supplied Port and
            // Transport values aren't clobbered.
            if (value == AuthModeSelection.Azure && !_isLoading)
            {
                UseTls = true;
                Port = 8883;
                SelectedTransport = TransportProtocol.Tcp;
            }
        }
    }

    // Property to control visibility of Username/Password fields in UI
    public bool IsUsernamePasswordSelected => SelectedAuthMode == AuthModeSelection.UsernamePassword;

    public bool IsEnhancedAuthSelected => SelectedAuthMode == AuthModeSelection.Enhanced;

    /// <summary>
    /// Whether Azure (DefaultAzureCredential / OAUTH2-JWT) auth mode is currently selected.
    /// Used by UI bindings to show Azure-specific configuration rows.
    /// </summary>
    public bool IsAzureAuthSelected => SelectedAuthMode == AuthModeSelection.Azure;

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

    private string? _authenticationScope;
    /// <summary>
    /// OAuth scope used when Azure authentication mode is active. When null/empty, the
    /// engine falls back to <see cref="AzureAuthenticationMode.DefaultScope"/>.
    /// </summary>
    public string? AuthenticationScope
    {
        get => _authenticationScope;
        set => this.RaiseAndSetIfChanged(ref _authenticationScope, value);
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
        else if (SelectedAuthMode == AuthModeSelection.Azure)
        {
            authModeSetting = new AzureAuthenticationMode(AuthenticationScope);
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
            UseTls,
            SubscriptionQoS: SubscriptionQoS,
            Transport: SelectedTransport,
            WebSocketPath: WebSocketPath,
            SubscriptionTopic: SubscriptionTopic,
            WebSocketProxyAddress: WebSocketProxyAddress,
            WebSocketProxyUsername: WebSocketProxyUsername,
            WebSocketProxyPassword: WebSocketProxyPassword,
            ShowConnectionDialogOnLaunch: ShowConnectionDialogOnLaunch,
            Theme: Theme
        )
        {
            TopicSpecificBufferLimits = topicLimits
        };
    }

    public void From(SettingsData settingsData)
    {
        // Suppress the Azure auth-mode setter's auto-config here — settingsData
        // may legitimately carry a non-default Port/Transport (e.g. from a
        // persisted Aspire-provisioned configuration) that must not be
        // overwritten with the Event Grid 8883/Tcp defaults.
        var wasLoading = _isLoading;
        _isLoading = true;
        try
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
            SubscriptionQoS = settingsData.SubscriptionQoS;
            SubscriptionTopic = settingsData.SubscriptionTopic;
            SelectedTransport = settingsData.Transport;
            WebSocketPath = settingsData.WebSocketPath;
            WebSocketProxyAddress = settingsData.WebSocketProxyAddress;
            WebSocketProxyUsername = settingsData.WebSocketProxyUsername;
            WebSocketProxyPassword = settingsData.WebSocketProxyPassword;
            ShowConnectionDialogOnLaunch = settingsData.ShowConnectionDialogOnLaunch;
            Theme = settingsData.Theme;
            TopicSpecificLimits.Clear();

            // Ensure we always have the default '#' limit
            bool hasDefaultLimit = false;
            if (settingsData.TopicSpecificBufferLimits != null)
            {
                foreach (var limitModel in settingsData.TopicSpecificBufferLimits)
                {
                    TopicSpecificLimits.Add(new TopicBufferLimitViewModel(limitModel));
                    if (limitModel.TopicFilter == "#")
                    {
                        hasDefaultLimit = true;
                    }
                }
            }

            // Add default '#' limit if not present (1MB = 1024*1024 bytes)
            if (!hasDefaultLimit)
            {
                TopicSpecificLimits.Insert(0, new TopicBufferLimitViewModel(new TopicBufferLimit("#", 1024 * 1024)));
            }

            // Handle AuthMode and credentials
            if (settingsData.AuthMode is EnhancedAuthenticationMode enhancedAuth)
            {
                SelectedAuthMode = AuthModeSelection.Enhanced;
                AuthenticationMethod = enhancedAuth.AuthenticationMethod;
                AuthenticationData = enhancedAuth.AuthenticationData;
                AuthUsername = string.Empty;
                AuthPassword = string.Empty;
                AuthenticationScope = null;
            }
            else if (settingsData.AuthMode is UsernamePasswordAuthenticationMode userPassAuth)
            {
                SelectedAuthMode = AuthModeSelection.UsernamePassword;
                AuthUsername = userPassAuth.Username ?? string.Empty;
                AuthPassword = userPassAuth.Password ?? string.Empty;
                AuthenticationMethod = null;
                AuthenticationData = null;
                AuthenticationScope = null;
            }
            else if (settingsData.AuthMode is AzureAuthenticationMode azureAuth)
            {
                AuthenticationScope = azureAuth.Scope;
                SelectedAuthMode = AuthModeSelection.Azure;
                AuthUsername = string.Empty;
                AuthPassword = string.Empty;
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
                AuthenticationScope = null;
            }
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    /// <summary>
    /// Applies environment variable overrides on top of file-based settings.
    /// Only non-null properties in the overrides are applied. Any Azure
    /// auto-configuration triggered by <see cref="SelectedAuthMode"/> is
    /// suppressed for the duration of this call so an explicit
    /// <c>CROWSNEST__PORT</c>/<c>CROWSNEST__USE_TLS</c>/<c>CROWSNEST__TRANSPORT</c>
    /// override is preserved rather than being reset to the Event Grid defaults
    /// of 8883/true/Tcp.
    /// </summary>
    internal void ApplyEnvironmentOverrides(EnvironmentSettingsOverrides overrides)
    {
        var wasLoading = _isLoading;
        _isLoading = true;
        try
        {
            if (overrides.Hostname != null) Hostname = overrides.Hostname;
            if (overrides.Port.HasValue) Port = overrides.Port.Value;
            if (overrides.ClientId != null) ClientId = overrides.ClientId;
            if (overrides.KeepAliveIntervalSeconds.HasValue) KeepAliveIntervalSeconds = overrides.KeepAliveIntervalSeconds.Value;
            if (overrides.CleanSession.HasValue) CleanSession = overrides.CleanSession.Value;
            if (overrides.SessionExpiryIntervalSeconds.HasValue) SessionExpiryIntervalSeconds = overrides.SessionExpiryIntervalSeconds.Value;
            if (overrides.UseTls.HasValue) UseTls = overrides.UseTls.Value;
            if (overrides.SubscriptionQoS.HasValue) SubscriptionQoS = overrides.SubscriptionQoS.Value;
            if (overrides.ExportFormat.HasValue) ExportFormat = overrides.ExportFormat.Value;
            if (overrides.ExportPath != null) ExportPath = overrides.ExportPath;
            if (overrides.Transport.HasValue) SelectedTransport = overrides.Transport.Value;
            if (overrides.WebSocketPath != null) WebSocketPath = overrides.WebSocketPath;
            if (overrides.WebSocketProxyAddress != null) WebSocketProxyAddress = overrides.WebSocketProxyAddress;
            if (overrides.WebSocketProxyUsername != null) WebSocketProxyUsername = overrides.WebSocketProxyUsername;
            if (overrides.WebSocketProxyPassword != null) WebSocketProxyPassword = overrides.WebSocketProxyPassword;
            if (overrides.ShowConnectionDialogOnLaunch.HasValue) ShowConnectionDialogOnLaunch = overrides.ShowConnectionDialogOnLaunch.Value;

            if (overrides.AuthMode != null)
            {
                if (overrides.AuthMode is EnhancedAuthenticationMode enhanced)
                {
                    SelectedAuthMode = AuthModeSelection.Enhanced;
                    AuthenticationMethod = enhanced.AuthenticationMethod;
                    AuthenticationData = enhanced.AuthenticationData;
                    AuthUsername = string.Empty;
                    AuthPassword = string.Empty;
                    AuthenticationScope = null;
                }
                else if (overrides.AuthMode is UsernamePasswordAuthenticationMode userPass)
                {
                    SelectedAuthMode = AuthModeSelection.UsernamePassword;
                    AuthUsername = userPass.Username;
                    AuthPassword = userPass.Password;
                    AuthenticationMethod = null;
                    AuthenticationData = null;
                    AuthenticationScope = null;
                }
                else if (overrides.AuthMode is AzureAuthenticationMode azure)
                {
                    AuthenticationScope = azure.Scope;
                    SelectedAuthMode = AuthModeSelection.Azure;
                    AuthUsername = string.Empty;
                    AuthPassword = string.Empty;
                    AuthenticationMethod = null;
                    AuthenticationData = null;
                }
                else
                {
                    SelectedAuthMode = AuthModeSelection.Anonymous;
                    AuthUsername = string.Empty;
                    AuthPassword = string.Empty;
                    AuthenticationMethod = null;
                    AuthenticationData = null;
                    AuthenticationScope = null;
                }
            }

            if (overrides.TopicSpecificBufferLimits != null)
            {
                TopicSpecificLimits.Clear();
                foreach (var limit in overrides.TopicSpecificBufferLimits)
                {
                    TopicSpecificLimits.Add(new TopicBufferLimitViewModel(limit));
                }
                EnsureDefaultTopicLimit();
            }
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    // --- Persistence Methods ---

    private void SaveSettings()
    {
        if (_isLoading || _isAspireEnvironment) return;

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
            // Add default '#' limit when no settings file exists
            EnsureDefaultTopicLimit();
            return; // Use default values if file doesn't exist
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            var loadedData = JsonSerializer.Deserialize(json, SettingsViewModelJsonContext.Default.SettingsData);

            if (loadedData != null)
            {
                From(loadedData); // This now also populates TopicSpecificLimits and ensures default limit
                AppLogger.Information("Settings loaded from {FilePath}", _settingsFilePath);
            }
            else
            {
                 AppLogger.Warning("Failed to deserialize settings from {FilePath}. Using defaults.", _settingsFilePath);
                 if (string.IsNullOrEmpty(ExportPath)) ExportPath = _exportFolderPath;
                 EnsureDefaultTopicLimit();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Error loading settings from {FilePath}", _settingsFilePath);
            // Keep default values if loading fails, ensure default export path
            if (string.IsNullOrEmpty(ExportPath)) ExportPath = _exportFolderPath;
            EnsureDefaultTopicLimit();
        }
    }

    /// <summary>
    /// Ensures the default '#' topic limit is present in the TopicSpecificLimits collection.
    /// </summary>
    private void EnsureDefaultTopicLimit()
    {
        if (!TopicSpecificLimits.Any(limit => limit.TopicFilter == "#"))
        {
            TopicSpecificLimits.Insert(0, new TopicBufferLimitViewModel(new TopicBufferLimit("#", 1024 * 1024)));
        }
    }
}
