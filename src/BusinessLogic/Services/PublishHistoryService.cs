using System.Text;
using System.Text.Json;
using CrowsNestMqtt.BusinessLogic.Models;
using MQTTnet.Packets;
using Serilog;

namespace CrowsNestMqtt.BusinessLogic.Services;

/// <summary>
/// Manages publish message history with JSON file persistence.
/// Stores the last 50 published messages for re-use.
/// </summary>
public class PublishHistoryService : IPublishHistoryService
{
    private const int MaxHistoryEntries = 50;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _historyFilePath;
    private readonly List<PublishHistoryEntry> _history = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new PublishHistoryService.
    /// </summary>
    /// <param name="historyFilePath">Path to the history JSON file. Defaults to %LocalAppData%\CrowsNestMqtt\publish-history.json.</param>
    public PublishHistoryService(string? historyFilePath = null)
    {
        _historyFilePath = historyFilePath ?? GetDefaultHistoryFilePath();
    }

    /// <inheritdoc />
    public void AddEntry(MqttPublishRequest request)
    {
        var entry = new PublishHistoryEntry
        {
            Topic = request.Topic,
            PayloadText = request.PayloadText,
            PayloadBase64 = request.Payload != null ? Convert.ToBase64String(request.Payload) : null,
            QoS = (int)request.QoS,
            Retain = request.Retain,
            ContentType = request.ContentType,
            PayloadFormatIndicator = (int)request.PayloadFormatIndicator,
            ResponseTopic = request.ResponseTopic,
            CorrelationDataHex = request.CorrelationData != null ? Convert.ToHexString(request.CorrelationData) : null,
            MessageExpiryInterval = request.MessageExpiryInterval,
            UserProperties = request.UserProperties
                .ToDictionary(p => p.Name, p => p.ReadValueAsString()),
            Timestamp = DateTime.UtcNow
        };

        lock (_lock)
        {
            _history.Insert(0, entry);
            if (_history.Count > MaxHistoryEntries)
            {
                _history.RemoveRange(MaxHistoryEntries, _history.Count - MaxHistoryEntries);
            }
        }

        // Fire-and-forget save
        _ = SaveAsync();
    }

    /// <inheritdoc />
    public IReadOnlyList<PublishHistoryEntry> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList().AsReadOnly();
        }
    }

    /// <inheritdoc />
    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
        }

        _ = SaveAsync();
    }

    /// <inheritdoc />
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                Log.Debug("No publish history file found at {Path}", _historyFilePath);
                return;
            }

            var json = await File.ReadAllTextAsync(_historyFilePath).ConfigureAwait(false);
            var entries = JsonSerializer.Deserialize<List<PublishHistoryEntry>>(json, JsonOptions);

            if (entries != null)
            {
                lock (_lock)
                {
                    _history.Clear();
                    _history.AddRange(entries.Take(MaxHistoryEntries));
                }
                Log.Information("Loaded {Count} publish history entries", entries.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load publish history from {Path}", _historyFilePath);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            List<PublishHistoryEntry> snapshot;
            lock (_lock)
            {
                snapshot = _history.ToList();
            }

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            // Atomic write: write to temp file, then rename
            var tempPath = _historyFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8).ConfigureAwait(false);
            File.Move(tempPath, _historyFilePath, overwrite: true);

            Log.Debug("Saved {Count} publish history entries to {Path}", snapshot.Count, _historyFilePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save publish history to {Path}", _historyFilePath);
        }
    }

    private static string GetDefaultHistoryFilePath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrowsNestMqtt");
        return Path.Combine(appDataPath, "publish-history.json");
    }
}
