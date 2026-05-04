using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Highlighting;
using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.Services;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using ReactiveUI;
using Serilog;

namespace CrowsNestMqtt.UI.ViewModels;

/// <summary>
/// Represents a user property key-value pair for the publish dialog.
/// </summary>
public class UserPropertyViewModel : ReactiveObject
{
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}

/// <summary>
/// ViewModel for the non-modal publish window.
/// Manages all publish fields, MQTT V5 properties, syntax highlighting, and publish history.
/// </summary>
public class PublishViewModel : ReactiveObject, IDisposable
{
    private readonly IMqttService? _mqttService;
    private readonly IPublishHistoryService? _publishHistoryService;
    private readonly IFileAutoCompleteService? _fileAutoCompleteService;
    private bool _disposed;

    // --- Topic ---
    private string _topic = string.Empty;
    public string Topic
    {
        get => _topic;
        set => this.RaiseAndSetIfChanged(ref _topic, value);
    }

    // --- Payload Editor ---
    private TextDocument _payloadDocument = new();
    public TextDocument PayloadDocument
    {
        get => _payloadDocument;
        set => this.RaiseAndSetIfChanged(ref _payloadDocument, value);
    }

    // --- QoS ---
    private int _selectedQoS = 1;
    public int SelectedQoS
    {
        get => _selectedQoS;
        set => this.RaiseAndSetIfChanged(ref _selectedQoS, value);
    }

    private static readonly int[] _qosLevels = [0, 1, 2];
    public static int[] QoSLevels => _qosLevels;

    // --- Retain ---
    private bool _retain;
    public bool Retain
    {
        get => _retain;
        set => this.RaiseAndSetIfChanged(ref _retain, value);
    }

    // --- Content Type ---
    private string _contentType = string.Empty;
    public string ContentType
    {
        get => _contentType;
        set => this.RaiseAndSetIfChanged(ref _contentType, value);
    }

    // --- Payload Format Indicator ---
    private int _payloadFormatIndicator; // 0 = Unspecified, 1 = CharacterData
    public int PayloadFormatIndicator
    {
        get => _payloadFormatIndicator;
        set => this.RaiseAndSetIfChanged(ref _payloadFormatIndicator, value);
    }

    public static string[] PayloadFormatOptions => ["Unspecified", "UTF-8 Character Data"];

    // --- Response Topic ---
    private string _responseTopic = string.Empty;
    public string ResponseTopic
    {
        get => _responseTopic;
        set => this.RaiseAndSetIfChanged(ref _responseTopic, value);
    }

    // --- Correlation Data ---
    private string _correlationData = string.Empty;
    public string CorrelationData
    {
        get => _correlationData;
        set => this.RaiseAndSetIfChanged(ref _correlationData, value);
    }

    // --- Message Expiry Interval ---
    private uint _messageExpiryInterval;
    public uint MessageExpiryInterval
    {
        get => _messageExpiryInterval;
        set => this.RaiseAndSetIfChanged(ref _messageExpiryInterval, value);
    }

    // --- User Properties ---
    public ObservableCollection<UserPropertyViewModel> UserProperties { get; } = new();

    // --- Syntax Highlighting ---
    private IHighlightingDefinition? _syntaxHighlighting;
    public IHighlightingDefinition? SyntaxHighlighting
    {
        get => _syntaxHighlighting;
        private set => this.RaiseAndSetIfChanged(ref _syntaxHighlighting, value);
    }

    // --- V5 Properties Panel visibility ---
    private bool _isV5PropertiesExpanded;
    public bool IsV5PropertiesExpanded
    {
        get => _isV5PropertiesExpanded;
        set => this.RaiseAndSetIfChanged(ref _isV5PropertiesExpanded, value);
    }

    // --- Publish History ---
    private ObservableCollection<PublishHistoryEntry> _historyEntries = new();
    public ObservableCollection<PublishHistoryEntry> HistoryEntries
    {
        get => _historyEntries;
        private set => this.RaiseAndSetIfChanged(ref _historyEntries, value);
    }

    private PublishHistoryEntry? _selectedHistoryEntry;
    public PublishHistoryEntry? SelectedHistoryEntry
    {
        get => _selectedHistoryEntry;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedHistoryEntry, value);
            if (value != null)
                LoadFromHistoryEntry(value);
        }
    }

    // --- File Autocomplete ---
    private ObservableCollection<FileAutoCompleteSuggestion> _fileSuggestions = new();
    public ObservableCollection<FileAutoCompleteSuggestion> FileSuggestions
    {
        get => _fileSuggestions;
        private set => this.RaiseAndSetIfChanged(ref _fileSuggestions, value);
    }

    // --- Status ---
    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    // --- File reference ---
    private string? _loadedFilePath;
    /// <summary>
    /// When set, the publish will send raw file bytes instead of editor text.
    /// </summary>
    public string? LoadedFilePath
    {
        get => _loadedFilePath;
        set => this.RaiseAndSetIfChanged(ref _loadedFilePath, value);
    }

    private bool _isPayloadReadOnly;
    /// <summary>
    /// True when a file is loaded — editor shows file info overlay and is read-only.
    /// </summary>
    public bool IsPayloadReadOnly
    {
        get => _isPayloadReadOnly;
        set => this.RaiseAndSetIfChanged(ref _isPayloadReadOnly, value);
    }

    private string _fileInfoDisplay = string.Empty;
    /// <summary>
    /// Summary text shown when a file is loaded (e.g., "Sending file: path\nSize: 1.2 MB").
    /// Displayed instead of the text editor when IsPayloadReadOnly is true.
    /// </summary>
    public string FileInfoDisplay
    {
        get => _fileInfoDisplay;
        set => this.RaiseAndSetIfChanged(ref _fileInfoDisplay, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    // --- Commands ---
    public ReactiveCommand<Unit, Unit> PublishCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadFileCommand { get; }
    public ReactiveCommand<Unit, Unit> AddUserPropertyCommand { get; }
    public ReactiveCommand<UserPropertyViewModel, Unit> RemoveUserPropertyCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleV5PropertiesCommand { get; }

    public PublishViewModel(
        IMqttService? mqttService = null,
        IPublishHistoryService? publishHistoryService = null,
        IFileAutoCompleteService? fileAutoCompleteService = null)
    {
        _mqttService = mqttService;
        _publishHistoryService = publishHistoryService;
        _fileAutoCompleteService = fileAutoCompleteService;

        // Publish enabled when connected and topic is non-empty
        var canPublish = this.WhenAnyValue(
            x => x.IsConnected,
            x => x.Topic,
            (connected, topic) => connected && !string.IsNullOrWhiteSpace(topic));

        PublishCommand = ReactiveCommand.CreateFromTask(ExecutePublishAsync, canPublish);
        ClearCommand = ReactiveCommand.Create(ExecuteClear);
        LoadFileCommand = ReactiveCommand.CreateFromTask(ExecuteLoadFileAsync);
        AddUserPropertyCommand = ReactiveCommand.Create(ExecuteAddUserProperty);
        RemoveUserPropertyCommand = ReactiveCommand.Create<UserPropertyViewModel>(ExecuteRemoveUserProperty);
        ToggleV5PropertiesCommand = ReactiveCommand.Create(() => { IsV5PropertiesExpanded = !IsV5PropertiesExpanded; });

        // Update syntax highlighting when ContentType changes
        this.WhenAnyValue(x => x.ContentType)
            .Throttle(TimeSpan.FromMilliseconds(300))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(ct => UpdateSyntaxHighlighting(ct));

        // Load history on init
        _ = LoadHistoryAsync();
    }

    private async Task ExecutePublishAsync()
    {
        if (_mqttService == null)
        {
            StatusText = "Error: MQTT service not available.";
            return;
        }

        try
        {
            // If a file is loaded, read its bytes asynchronously (off UI thread)
            byte[]? filePayload = null;
            if (!string.IsNullOrEmpty(LoadedFilePath))
            {
                if (!File.Exists(LoadedFilePath))
                {
                    StatusText = $"File no longer exists: {LoadedFilePath}";
                    return;
                }

                StatusText = $"Reading file '{Path.GetFileName(LoadedFilePath)}'...";
                filePayload = await File.ReadAllBytesAsync(LoadedFilePath).ConfigureAwait(false);
            }

            var request = BuildPublishRequest(filePayload);
            StatusText = $"Publishing to '{request.Topic}'...";

            var result = await _mqttService.PublishAsync(request).ConfigureAwait(false);

            if (result.Success)
            {
                StatusText = $"Published to '{result.Topic}' successfully.";
                _publishHistoryService?.AddEntry(request, LoadedFilePath);
                if (_publishHistoryService != null)
                    await _publishHistoryService.SaveAsync().ConfigureAwait(false);
                await RefreshHistoryAsync().ConfigureAwait(false);
            }
            else
            {
                StatusText = $"Publish failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Publish error: {ex.Message}";
            Log.Error(ex, "Error publishing message");
        }
    }

    internal MqttPublishRequest BuildPublishRequest(byte[]? filePayload = null)
    {
        var userProps = UserProperties
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .Select(p => new MqttUserProperty(p.Name, System.Text.Encoding.UTF8.GetBytes(p.Value ?? string.Empty)))
            .ToList();

        byte[]? correlationBytes = null;
        if (!string.IsNullOrWhiteSpace(CorrelationData))
        {
            try
            {
                correlationBytes = Convert.FromHexString(CorrelationData.Replace(" ", ""));
            }
            catch
            {
                correlationBytes = Encoding.UTF8.GetBytes(CorrelationData);
            }
        }

        // When file payload is provided, use it directly (binary-safe)
        byte[]? payload = filePayload;
        string? payloadText = filePayload == null ? PayloadDocument.Text : null;

        return new MqttPublishRequest
        {
            Topic = Topic.Trim(),
            Payload = payload,
            PayloadText = payloadText,
            QoS = (MqttQualityOfServiceLevel)SelectedQoS,
            Retain = Retain,
            ContentType = string.IsNullOrWhiteSpace(ContentType) ? null : ContentType.Trim(),
            PayloadFormatIndicator = (MqttPayloadFormatIndicator)PayloadFormatIndicator,
            ResponseTopic = string.IsNullOrWhiteSpace(ResponseTopic) ? null : ResponseTopic.Trim(),
            CorrelationData = correlationBytes,
            MessageExpiryInterval = MessageExpiryInterval,
            UserProperties = userProps
        };
    }

    private void ExecuteClear()
    {
        LoadedFilePath = null;
        IsPayloadReadOnly = false;
        FileInfoDisplay = string.Empty;
        Topic = string.Empty;
        PayloadDocument = new TextDocument();
        SelectedQoS = 1;
        Retain = false;
        ContentType = string.Empty;
        PayloadFormatIndicator = 0;
        ResponseTopic = string.Empty;
        CorrelationData = string.Empty;
        MessageExpiryInterval = 0;
        UserProperties.Clear();
        StatusText = "Cleared.";
    }

    private async Task ExecuteLoadFileAsync()
    {
        // Triggered by the View which handles the file dialog
        StatusText = "Use the file dialog to select a file.";
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Loads a file reference for publishing. Does NOT load content into the editor.
    /// Sets the editor to read-only with a summary, and auto-detects content-type
    /// and payload-format-indicator from the file extension.
    /// </summary>
    public Task LoadFileContentAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                StatusText = $"File not found: {filePath}";
                return Task.CompletedTask;
            }

            var fileInfo = new FileInfo(filePath);
            const long mqttMaxPayloadBytes = 268_435_455; // MQTT 5.0 max: 256MB - 1
            if (fileInfo.Length > mqttMaxPayloadBytes)
            {
                StatusText = $"File too large ({fileInfo.Length / (1024 * 1024)}MB). MQTT maximum payload is 256MB.";
                return Task.CompletedTask;
            }

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var (detectedContentType, isBinary) = DetectContentType(ext);

            LoadedFilePath = filePath;
            IsPayloadReadOnly = true;

            var sizeDisplay = fileInfo.Length < 1024
                ? $"{fileInfo.Length} bytes"
                : fileInfo.Length < 1024 * 1024
                    ? $"{fileInfo.Length / 1024.0:F1} KB"
                    : $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB";

            FileInfoDisplay = $"Sending file: {filePath}\nSize: {sizeDisplay}\nType: {(isBinary ? "Binary" : "Text")}";

            ContentType = detectedContentType;
            PayloadFormatIndicator = 0;

            StatusText = $"File selected: {Path.GetFileName(filePath)} ({sizeDisplay})";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading file: {ex.Message}";
            Log.Warning(ex, "Failed to load file {FilePath}", filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the loaded file reference and restores editor editability.
    /// </summary>
    public void ClearFileReference()
    {
        LoadedFilePath = null;
        IsPayloadReadOnly = false;
        FileInfoDisplay = string.Empty;
    }

    /// <summary>
    /// Detects MIME content-type and whether the file is binary based on extension.
    /// Returns (contentType, isBinary).
    /// </summary>
    internal static (string ContentType, bool IsBinary) DetectContentType(string extension) => extension switch
    {
        // Text formats
        ".json" => ("application/json", false),
        ".xml" => ("application/xml", false),
        ".html" or ".htm" => ("text/html", false),
        ".txt" or ".log" or ".md" or ".rst" => ("text/plain", false),
        ".csv" => ("text/csv", false),
        ".yaml" or ".yml" => ("application/yaml", false),
        ".toml" => ("application/toml", false),
        ".ini" or ".cfg" or ".conf" => ("text/plain", false),
        ".js" => ("application/javascript", false),
        ".ts" => ("application/typescript", false),
        ".css" => ("text/css", false),
        ".sql" => ("application/sql", false),
        ".graphql" or ".gql" => ("application/graphql", false),

        // Image formats (binary)
        ".png" => ("image/png", true),
        ".jpg" or ".jpeg" => ("image/jpeg", true),
        ".gif" => ("image/gif", true),
        ".bmp" => ("image/bmp", true),
        ".webp" => ("image/webp", true),
        ".svg" => ("image/svg+xml", false), // SVG is text-based
        ".ico" => ("image/x-icon", true),
        ".tiff" or ".tif" => ("image/tiff", true),

        // Audio formats (binary)
        ".mp3" => ("audio/mpeg", true),
        ".wav" => ("audio/wav", true),
        ".ogg" => ("audio/ogg", true),
        ".flac" => ("audio/flac", true),
        ".aac" => ("audio/aac", true),

        // Video formats (binary)
        ".mp4" => ("video/mp4", true),
        ".webm" => ("video/webm", true),
        ".avi" => ("video/x-msvideo", true),
        ".mkv" => ("video/x-matroska", true),
        ".mov" => ("video/quicktime", true),

        // Serialization formats (binary)
        ".protobuf" or ".proto" or ".pb" => ("application/protobuf", true),
        ".msgpack" or ".mp" => ("application/msgpack", true),
        ".avro" => ("application/avro", true),
        ".cbor" => ("application/cbor", true),
        ".bson" => ("application/bson", true),
        ".thrift" => ("application/x-thrift", true),

        // Archive/compressed (binary)
        ".zip" => ("application/zip", true),
        ".gz" or ".gzip" => ("application/gzip", true),
        ".tar" => ("application/x-tar", true),
        ".7z" => ("application/x-7z-compressed", true),

        // Document formats (binary)
        ".pdf" => ("application/pdf", true),
        ".doc" or ".docx" => ("application/msword", true),
        ".xls" or ".xlsx" => ("application/vnd.ms-excel", true),

        // Executable/binary
        ".bin" or ".dat" or ".raw" => ("application/octet-stream", true),
        ".exe" or ".dll" => ("application/octet-stream", true),
        ".wasm" => ("application/wasm", true),

        // Default: unknown binary
        _ => ("application/octet-stream", true),
    };

    /// <summary>
    /// Gets file autocomplete suggestions for the @ syntax.
    /// </summary>
    public void UpdateFileSuggestions(string partialPath)
    {
        if (_fileAutoCompleteService == null) return;

        var suggestions = _fileAutoCompleteService.GetSuggestions(partialPath, 15);
        FileSuggestions.Clear();
        foreach (var s in suggestions)
            FileSuggestions.Add(s);
    }

    private void ExecuteAddUserProperty()
    {
        UserProperties.Add(new UserPropertyViewModel());
    }

    private void ExecuteRemoveUserProperty(UserPropertyViewModel property)
    {
        UserProperties.Remove(property);
    }

    internal void UpdateSyntaxHighlighting(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            SyntaxHighlighting = null;
            return;
        }

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("Json");
        else if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
        else if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("HTML");
        else if (contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase))
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        else
            SyntaxHighlighting = null;
    }

    private void LoadFromHistoryEntry(PublishHistoryEntry entry)
    {
        Topic = entry.Topic;
        SelectedQoS = entry.QoS;
        Retain = entry.Retain;
        ContentType = entry.ContentType ?? string.Empty;
        PayloadFormatIndicator = entry.PayloadFormatIndicator;
        ResponseTopic = entry.ResponseTopic ?? string.Empty;
        CorrelationData = entry.CorrelationDataHex ?? string.Empty;
        MessageExpiryInterval = entry.MessageExpiryInterval;

        UserProperties.Clear();
        foreach (var prop in entry.UserProperties)
        {
            UserProperties.Add(new UserPropertyViewModel { Name = prop.Key, Value = prop.Value });
        }

        // If history entry was a file publish, re-load the file reference if it still exists
        if (!string.IsNullOrEmpty(entry.FilePath) && File.Exists(entry.FilePath))
        {
            LoadedFilePath = entry.FilePath;
            IsPayloadReadOnly = true;
            var fileInfo = new FileInfo(entry.FilePath);
            var sizeDisplay = fileInfo.Length < 1024
                ? $"{fileInfo.Length} bytes"
                : fileInfo.Length < 1024 * 1024
                    ? $"{fileInfo.Length / 1024.0:F1} KB"
                    : $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB";
            FileInfoDisplay = $"Sending file: {entry.FilePath}\nSize: {sizeDisplay}";
            StatusText = $"Loaded from history (file): {Path.GetFileName(entry.FilePath)}";
        }
        else
        {
            LoadedFilePath = null;
            IsPayloadReadOnly = false;
            FileInfoDisplay = string.Empty;
            PayloadDocument = new TextDocument(entry.PayloadText ?? string.Empty);
            StatusText = $"Loaded from history: {entry.Topic} ({entry.Timestamp:g})";
        }
    }

    private async Task LoadHistoryAsync()
    {
        if (_publishHistoryService == null) return;

        try
        {
            await _publishHistoryService.LoadAsync().ConfigureAwait(false);
            await RefreshHistoryAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load publish history");
        }
    }

    private Task RefreshHistoryAsync()
    {
        if (_publishHistoryService == null) return Task.CompletedTask;

        var history = _publishHistoryService.GetHistory();
        HistoryEntries.Clear();
        foreach (var entry in history)
            HistoryEntries.Add(entry);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PublishCommand.Dispose();
        ClearCommand.Dispose();
        LoadFileCommand.Dispose();
        AddUserPropertyCommand.Dispose();
        RemoveUserPropertyCommand.Dispose();
        ToggleV5PropertiesCommand.Dispose();
    }
}
