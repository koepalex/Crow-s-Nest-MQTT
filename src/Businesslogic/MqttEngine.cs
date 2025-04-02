using MQTTnet;
// using MQTTnet.Client; // Removed incorrect namespace
using MQTTnet.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowsNestMqtt.Utils;
// Removed incorrect using: using CrowsNestMqtt.UI.ViewModels;

namespace CrowsNestMqtt.BusinessLogic;

public class MqttEngine
{
    private readonly IMqttClient _client;
    private MqttConnectionSettings _settings; // Store settings (removed readonly)
    private MqttClientOptions? _currentOptions; // Store the options used for the current/last connection attempt
    private readonly ConcurrentDictionary<string, TopicRingBuffer> _topicBuffers; // Added buffer storage
    private const long DefaultMaxTopicBufferSize = 10 * 1024 * 1024; // 10 MB - Added default size

    public event EventHandler<MqttApplicationMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged; // Renamed/Refined event
    public event EventHandler<string>? LogMessage; // Added for logging internal info/errors

    public MqttEngine(MqttConnectionSettings settings) // Accept MqttConnectionSettings
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings)); // Store settings
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
        _topicBuffers = new ConcurrentDictionary<string, TopicRingBuffer>();

        _client.ApplicationMessageReceivedAsync += HandleIncomingMessageAsync; // Subscribe the handler method

        // Removed ConnectingFailedAsync handler. Initial connection failures
        // are caught in the ConnectAsync method's try-catch block.

        _client.DisconnectedAsync += (async e =>
        {
            LogMessage?.Invoke(this, $"Disconnected: {e.ReasonString}. Reconnecting...");
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, e.Exception));

            // Optional: Wait a bit before starting reconnect attempts
            await Task.Delay(TimeSpan.FromSeconds(5));
            // Start reconnect attempts (fire and forget or manage the task)
            if (e.Reason != MqttClientDisconnectReason.NormalDisconnection)
            {
                _ = Task.Run(ReconnectAsync); // Use Task.Run to avoid blocking the handler
            }
        });

        _client.ConnectedAsync += args =>
        {
            LogMessage?.Invoke(this, "Connected successfully.");
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(true, null));
            // Resubscribe after connection
             _ = Task.Run(SubscribeToTopicsAsync); // Fire and forget subscription task
            return Task.CompletedTask;
        };

    }

    /// <summary>
    /// Updates the connection settings used by the engine for subsequent connection attempts.
    /// </summary>
    /// <param name="newSettings">The new settings to use.</param>
    public void UpdateSettings(MqttConnectionSettings newSettings)
    {
        _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
        LogMessage?.Invoke(this, "MqttEngine settings updated.");
        // Note: This doesn't automatically reconnect or apply settings to an *active* connection.
        // A disconnect/reconnect cycle is needed for changes to take effect.
    }

    // Helper method for initial subscription and resubscription
    private async Task SubscribeToTopicsAsync()
    {
        try
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f =>
                    f.WithTopic("#") // Subscribe to all topics
                     .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)) // QoS 1
                .Build();

            var result = await _client.SubscribeAsync(subscribeOptions, CancellationToken.None);

            foreach (var subResult in result.Items)
            {
                if (subResult.ResultCode > MqttClientSubscribeResultCode.GrantedQoS2)
                {
                     LogMessage?.Invoke(this, $"Failed to subscribe to topic '{subResult.TopicFilter.Topic}'. Result: {subResult.ResultCode}");
                }
                else
                {
                    LogMessage?.Invoke(this, $"Successfully subscribed to topic '{subResult.TopicFilter.Topic}' with QoS {(int)subResult.ResultCode}.");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Subscription failed: {ex.Message}");
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, ex));
        }
    }

    // Builds MqttClientOptions based on current settings
    private MqttClientOptions BuildMqttOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.Hostname, _settings.Port)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithKeepAlivePeriod(_settings.KeepAliveInterval); // Use KeepAliveInterval

        // Client ID: Use provided or let MQTTnet generate
        if (!string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            builder.WithClientId(_settings.ClientId);
        }

        // Clean Session vs Session Expiry (MQTT v5 logic)
        if (_settings.CleanSession)
        {
            // For MQTT v5, CleanSession=true implies SessionExpiryInterval=0
            builder.WithCleanSession(true);
            builder.WithSessionExpiryInterval(0);
        }
        else
        {
            builder.WithCleanSession(false);
            // If SessionExpiryInterval is null, session lasts indefinitely (MQTT default)
            // Otherwise, use the provided value.
            if (_settings.SessionExpiryInterval.HasValue)
            {
                 builder.WithSessionExpiryInterval(_settings.SessionExpiryInterval.Value);
            }
            // If null, MQTTnet handles the default behavior (session never expires)
        }

        // Add other options from settings as needed (TLS, Credentials, etc.)

        return builder.Build();
    }

    public async Task ConnectAsync()
    {
        if (_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Already connected.");
            return;
        }

        try
        {
            _currentOptions = BuildMqttOptions(); // Build options just before connecting
            LogMessage?.Invoke(this, $"Attempting to connect to {_currentOptions.ChannelOptions} with ClientId '{_currentOptions.ClientId ?? "<generated>"}'. CleanSession={_currentOptions.CleanSession}, SessionExpiry={_currentOptions.SessionExpiryInterval}");
            await _client.ConnectAsync(_currentOptions, CancellationToken.None);
            // Subscription is handled by the ConnectedAsync handler
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Connection attempt failed: {ex.Message}");
            _currentOptions = null; // Clear options on failure
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, ex));
            // Reconnect logic will be triggered by DisconnectedAsync if the connection drops later
            // Or by ConnectingFailedAsync if the initial connection fails.
        }
    }

    public async Task DisconnectAsync()
    {
        if (!_client.IsConnected)
        {
             LogMessage?.Invoke(this, "Already disconnected.");
             return;
        }
        // Use default disconnect options (ReasonCode=NormalDisconnection, no UserProperties)
        await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build());
        LogMessage?.Invoke(this, "Disconnect requested by user.");
        _currentOptions = null; // Clear options on disconnect
        // State change is handled by the DisconnectedAsync handler
    }

    /// <summary>
    /// Retrieves the buffered messages for a specific topic.
    /// </summary>
    /// <param name="topic">The topic to retrieve messages for.</param>
    /// <returns>An enumerable collection of messages, or null if the topic is not buffered.</returns>
    public IEnumerable<MqttApplicationMessage>? GetMessagesForTopic(string topic)
    {
        if (_topicBuffers.TryGetValue(topic, out var buffer))
        {
            // Return a snapshot to avoid concurrency issues during enumeration
            return buffer.GetMessages().ToList();
        }
        return null; // Or return Enumerable.Empty<MqttApplicationMessage>()
    }

     /// <summary>
    /// Gets a list of all topics currently held in buffers.
    /// </summary>
    public IEnumerable<string> GetBufferedTopics()
    {
        return _topicBuffers.Keys.ToList();
    }

    /// <summary>
    /// Clears all messages from all topic buffers.
    /// </summary>
    public void ClearAllBuffers()
    {
        foreach (var buffer in _topicBuffers.Values)
        {
            buffer.Clear();
        }
        _topicBuffers.Clear(); // Clear the dictionary itself
        LogMessage?.Invoke(this, "All topic buffers cleared.");
    }

    private async Task ReconnectAsync()
    {
        // This method runs in a background task triggered by DisconnectedAsync
        LogMessage?.Invoke(this, "Starting reconnection attempts...");
        while (true) // Keep trying indefinitely (or add a max attempt limit)
        {
             // Check if connection exists and is NOT connected. Exit if connected.
             // Important: Check _client existence in case DisposeAsync was called.
            if (_client == null || _client.IsConnected)
            {
                LogMessage?.Invoke(this, "Reconnect unnecessary or client disposed.");
                break;
            }

            try
            {
                LogMessage?.Invoke(this, "Attempting to reconnect...");
                // ConnectAsync now handles its own logging and state changes
                await ConnectAsync();

                // If ConnectAsync succeeds, the Connected event fires and this loop should eventually exit.
                // Add a small delay after a successful connection attempt check to prevent tight loop if IsConnected flag is slow.
                await Task.Delay(TimeSpan.FromMilliseconds(100));

            }
            catch (Exception ex)
            {
                // ConnectAsync should ideally handle its exceptions and raise events,
                // but catch here as a fallback.
                LogMessage?.Invoke(this, $"Reconnect attempt failed: {ex.Message}");
                // ConnectionStateChanged is handled within ConnectAsync or ConnectingFailedAsync
            }

            // Wait before the next attempt
            await Task.Delay(TimeSpan.FromSeconds(5)); // Wait 5 seconds between attempts
        }
        LogMessage?.Invoke(this, "Reconnection attempts stopped.");
    }


    // Extracted message handling logic
    private Task HandleIncomingMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        // Store message in buffer
        var topic = e.ApplicationMessage.Topic;
        var buffer = _topicBuffers.GetOrAdd(topic, _ => new TopicRingBuffer(DefaultMaxTopicBufferSize));
        buffer.AddMessage(e.ApplicationMessage);

        // Notify external subscribers (e.g., UI)
        MessageReceived?.Invoke(this, e);

        // MQTTnet expects a Task return, Task.CompletedTask is appropriate for sync handling
        return Task.CompletedTask;
    }

    // Optional: Implement IDisposable or IAsyncDisposable if needed
    // public async ValueTask DisposeAsync()
    // {
    //     if (_client != null)
    //     {
    //         _client.ApplicationMessageReceivedAsync -= ... // Unsubscribe events
    //         _client.DisconnectedAsync -= ...
    //         _client.ConnectedAsync -= ...
    //         _client.ConnectingFailedAsync -= ...
    //         if (_client.IsConnected)
    //         {
    //             await DisconnectAsync();
    //         }
    //         _client.Dispose();
    //     }
    //     // Clear buffers?
    //     _topicBuffers.Clear();
    // }
}

/// <summary>
/// Provides data for the ConnectionStateChanged event.
/// </summary>
public class MqttConnectionStateChangedEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public Exception? Error { get; }

    public MqttConnectionStateChangedEventArgs(bool isConnected, Exception? error)
    {
        IsConnected = isConnected;
        Error = error;
    }
}