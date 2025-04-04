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

public class MqttEngine : IDisposable
{
    private readonly IMqttClient _client;
    private MqttConnectionSettings _settings; // Store settings (removed readonly)
    private bool _isDisposing; // Flag to indicate disposal process
    private MqttClientOptions? _currentOptions; // Store the options used for the current/last connection attempt
    private bool _connecting;
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

        // Subscribe using named handlers
        _client.ApplicationMessageReceivedAsync += HandleIncomingMessageAsync;
        _client.ConnectedAsync += OnClientConnected;
        _client.DisconnectedAsync += OnClientDisconnected;

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

    // Helper method for initial subscription and resubscription, now accepts CancellationToken
    private async Task SubscribeToTopicsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f =>
                    f.WithTopic("#") // Subscribe to all topics
                     .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)) // QoS 1
                .Build();

            var result = await _client.SubscribeAsync(subscribeOptions, cancellationToken); // Pass the token

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

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Already connected.");
            return;
        }

        if (_connecting)
        {
            LogMessage?.Invoke(this, "Connecting already in progress.");
            return;
        }

        try
        {
            Interlocked.Exchange(ref _connecting, true);
            _currentOptions = BuildMqttOptions(); // Build options just before connecting
            LogMessage?.Invoke(this, $"Attempting to connect to {_currentOptions.ChannelOptions} with ClientId '{_currentOptions.ClientId ?? "<generated>"}'. CleanSession={_currentOptions.CleanSession}, SessionExpiry={_currentOptions.SessionExpiryInterval}");
            var connectionResult = await _client.ConnectAsync(_currentOptions, cancellationToken);
            LogMessage?.Invoke(this, $"connection result: {connectionResult.ReasonString}:{connectionResult.ResultCode}");
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
        finally
        {
            Interlocked.Exchange(ref _connecting, false);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
             LogMessage?.Invoke(this, "Already disconnected.");
             return;
        }
        // Use default disconnect options (ReasonCode=NormalDisconnection, no UserProperties)
        await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
        LogMessage?.Invoke(this, "Disconnect requested by user.");
        _currentOptions = null; // Clear options on disconnect
        // State change is handled by the DisconnectedAsync handler
    }

    /// <summary>
    /// Publishes a message to the specified topic.
    /// </summary>
    /// <param name="topic">The topic to publish to.</param>
    /// <param name="payload">The message payload.</param>
    /// <param name="retain">Whether the message should be retained.</param>
    /// <param name="qos">The Quality of Service level.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PublishAsync(string topic, string payload, bool retain = false, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Cannot publish: Client is not connected.");
            // Optionally throw an exception or return a specific result
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        try
        {
            var result = await _client.PublishAsync(message, cancellationToken);
            if (result.ReasonCode != MqttClientPublishReasonCode.Success)
            {
                 LogMessage?.Invoke(this, $"Failed to publish to '{topic}'. Reason: {result.ReasonCode}");
                 // Optionally throw or handle specific reason codes
            }
            else
            {
                LogMessage?.Invoke(this, $"Successfully published to '{topic}'.");
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Error publishing to '{topic}': {ex.Message}");
            // Rethrow or handle exception as needed
            throw;
        }
    }

    /// <summary>
    /// Subscribes the client to the specified topic filter.
    /// </summary>
    /// <param name="topicFilter">The topic filter to subscribe to.</param>
    /// <param name="qos">The desired Quality of Service level.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the subscribe result.</returns>
    public async Task<MqttClientSubscribeResult> SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Cannot subscribe: Client is not connected.");
            // TODO: Store requested subscription to apply on connect?
            throw new InvalidOperationException("Client is not connected.");
        }

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topicFilter).WithQualityOfServiceLevel(qos))
            .Build();

        try
        {
            var result = await _client.SubscribeAsync(subscribeOptions, cancellationToken);
            LogMessage?.Invoke(this, $"Subscription request sent for '{topicFilter}'.");
            // TODO: Handle result codes (Granted, Denied etc.) more granularly if needed
            return result;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Error subscribing to '{topicFilter}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Unsubscribes the client from the specified topic filter.
    /// </summary>
    /// <param name="topicFilter">The topic filter to unsubscribe from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the unsubscribe result.</returns>
    public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Cannot unsubscribe: Client is not connected.");
            // TODO: Remove from stored requested subscriptions?
            throw new InvalidOperationException("Client is not connected.");
        }

        var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder()
            .WithTopicFilter(topicFilter)
            .Build();

        try
        {
            var result = await _client.UnsubscribeAsync(unsubscribeOptions, cancellationToken);
            LogMessage?.Invoke(this, $"Unsubscription request sent for '{topicFilter}'.");
            return result;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Error unsubscribing from '{topicFilter}': {ex.Message}");
            throw;
        }
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
        int reconnectCount = 1;
        while (!_isDisposing && !_client.IsConnected) // Keep trying until disposed or connected
        {
            // Removed explicit check for _client.IsConnected as it's in the while condition

            try
            {
                LogMessage?.Invoke(this, "Attempting to reconnect...");
                // ConnectAsync now handles its own logging and state changes
                // Pass CancellationToken.None here, as reconnect shouldn't be cancelled by the main shutdown token directly.
                // The loop condition handles disposal cancellation.
                await ConnectAsync(CancellationToken.None);

                // If ConnectAsync succeeds, the Connected event fires and this loop should eventually exit.
                // Add a small delay after a successful connection attempt check to prevent tight loop if IsConnected flag is slow.
                await Task.Delay(TimeSpan.FromMilliseconds(250 * reconnectCount));
                reconnectCount++;

            }
            catch (Exception ex)
            {
                // ConnectAsync should ideally handle its exceptions and raise events,
                // but catch here as a fallback.
                LogMessage?.Invoke(this, $"Reconnect attempt failed: {ex.Message}: reconnect attempt: {reconnectCount}");
                // ConnectionStateChanged is handled within ConnectAsync or ConnectingFailedAsync
            }

            // Wait before the next attempt
            // Wait before the next attempt, checking for disposal during the delay
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 5 * reconnectCount)), CancellationToken.None); // Use CancellationToken.None for delay itself, loop condition handles disposal
            } catch (TaskCanceledException)
            {
                 LogMessage?.Invoke(this, "Reconnect delay cancelled.");
                 break; // Exit loop if delay is cancelled (though unlikely with CancellationToken.None)
            }
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
    // --- IDisposable Implementation ---

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposing) return; // Already disposed

        if (disposing)
        {
            LogMessage?.Invoke(this, "Disposing MqttEngine...");
            _isDisposing = true; // Set flag early

            // Unsubscribe from events to prevent issues during disposal
            _client.ApplicationMessageReceivedAsync -= HandleIncomingMessageAsync;
            _client.DisconnectedAsync -= OnClientDisconnected;
            _client.ConnectedAsync -= OnClientConnected;

            // Attempt graceful disconnect (synchronous wait)
            if (_client.IsConnected)
            {
                try
                {
                    LogMessage?.Invoke(this, "Attempting final disconnect...");
                    // Use a short timeout for the final disconnect attempt
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cts.Token)
                           .ConfigureAwait(false).GetAwaiter().GetResult();
                    LogMessage?.Invoke(this, "Final disconnect completed.");
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Error during final disconnect: {ex.Message}");
                }
            }

            // Dispose the MQTT client itself
            _client?.Dispose();
            LogMessage?.Invoke(this, "MqttEngine disposed.");
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // Renamed event handlers to avoid conflicts with local variables if needed later
    private Task OnClientConnected(MqttClientConnectedEventArgs args)
    {
        LogMessage?.Invoke(this, "Connected successfully.");
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(true, null));
        // Resubscribe after connection (use CancellationToken.None for internal task)
        // Correct Task.Run syntax using lambda
        _ = Task.Run(() => SubscribeToTopicsAsync(CancellationToken.None), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task OnClientDisconnected(MqttClientDisconnectedEventArgs e)
    {
        LogMessage?.Invoke(this, $"Disconnected: {e.ReasonString}.");
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, e.Exception));

        // Only attempt reconnect if not disposing and it wasn't a normal disconnect
        if (!_isDisposing && e.Reason != MqttClientDisconnectReason.NormalDisconnection)
        {
             LogMessage?.Invoke(this, "Will attempt reconnection shortly...");
             // Optional: Wait a bit before starting reconnect attempts
             await Task.Delay(TimeSpan.FromSeconds(5));
             // Start reconnect attempts if still not disposing
             if (!_isDisposing)
             {
                 _ = Task.Run(ReconnectAsync); // Use Task.Run to avoid blocking the handler
             }
        }
    }
} // End of MqttEngine class
