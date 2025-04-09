using MQTTnet;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using CrowsNestMqtt.Utils;

namespace CrowsNestMqtt.BusinessLogic;

// New EventArgs class including MessageId and Topic
public class IdentifiedMqttApplicationMessageReceivedEventArgs : EventArgs // Not inheriting from MqttApplicationMessageReceivedEventArgs to avoid confusion
{
    public Guid MessageId { get; }
    public string Topic { get; }
    public MqttApplicationMessage ApplicationMessage { get; }
    // Include other relevant properties from MqttApplicationMessageReceivedEventArgs if needed
    public bool ProcessingFailed { get; set; } // Example property
    public string ClientId { get; } // Example property

    public IdentifiedMqttApplicationMessageReceivedEventArgs(Guid messageId, MqttApplicationMessage applicationMessage, string clientId)
    {
        MessageId = messageId;
        Topic = applicationMessage?.Topic ?? throw new ArgumentNullException(nameof(applicationMessage.Topic));
        ApplicationMessage = applicationMessage ?? throw new ArgumentNullException(nameof(applicationMessage));
        ClientId = clientId;
    }
}


public class MqttEngine : IMqttService // Implement the interface
{
    private readonly IMqttClient _client;
    private MqttConnectionSettings _settings;
    private bool _isDisposing;
    private MqttClientOptions? _currentOptions;
    private bool _connecting;
    private readonly ConcurrentDictionary<string, TopicRingBuffer> _topicBuffers;
    private const long DefaultMaxTopicBufferSize = 1 * 1024 * 1024;

    // Modified event signature
    public event EventHandler<IdentifiedMqttApplicationMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<string>? LogMessage;

    public MqttEngine(MqttConnectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
        _topicBuffers = new ConcurrentDictionary<string, TopicRingBuffer>();

        _client.ApplicationMessageReceivedAsync += HandleIncomingMessageAsync;
        _client.ConnectedAsync += OnClientConnected;
        _client.DisconnectedAsync += OnClientDisconnected;
    }

    // ... (UpdateSettings, SubscribeToTopicsAsync, BuildMqttOptions, ConnectAsync, DisconnectAsync, PublishAsync, SubscribeAsync, UnsubscribeAsync remain largely the same) ...
    // UpdateSettings method
    public void UpdateSettings(MqttConnectionSettings newSettings)
    {
        _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
        LogMessage?.Invoke(this, "MqttEngine settings updated.");
    }

    // Helper method for initial subscription and resubscription
    private async Task SubscribeToTopicsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(f =>
                    f.WithTopic("#")
                     .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                .Build();

            var result = await _client.SubscribeAsync(subscribeOptions, cancellationToken);

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
            .WithKeepAlivePeriod(_settings.KeepAliveInterval);

        if (!string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            builder.WithClientId(_settings.ClientId);
        }

        if (_settings.CleanSession)
        {
            builder.WithCleanSession(true);
            builder.WithSessionExpiryInterval(0);
        }
        else
        {
            builder.WithCleanSession(false);
            if (_settings.SessionExpiryInterval.HasValue)
            {
                 builder.WithSessionExpiryInterval(_settings.SessionExpiryInterval.Value);
            }
        }
        // Add other options like TLS, Credentials here if needed
        return builder.Build();
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client.IsConnected || _connecting)
        {
            LogMessage?.Invoke(this, _client.IsConnected ? "Already connected." : "Connecting already in progress.");
            return;
        }

        try
        {
            _connecting = true; // Simplified locking for brevity
            _currentOptions = BuildMqttOptions();
            LogMessage?.Invoke(this, $"Attempting to connect to {_currentOptions.ChannelOptions} with ClientId '{_currentOptions.ClientId ?? "<generated>"}'. CleanSession={_currentOptions.CleanSession}, SessionExpiry={_currentOptions.SessionExpiryInterval}");
            var connectionResult = await _client.ConnectAsync(_currentOptions, cancellationToken);
            LogMessage?.Invoke(this, $"Connection result: {connectionResult.ReasonString}:{connectionResult.ResultCode}");
            // Subscription handled by OnClientConnected
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Connection attempt failed: {ex.Message}");
            _currentOptions = null;
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, ex));
        }
        finally
        {
            _connecting = false;
        }
    }

     public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
             LogMessage?.Invoke(this, "Already disconnected.");
             return;
        }
        await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
        LogMessage?.Invoke(this, "Disconnect requested by user.");
        _currentOptions = null;
        // State change handled by OnClientDisconnected
    }

    public async Task PublishAsync(string topic, string payload, bool retain = false, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Cannot publish: Client is not connected.");
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
            }
            else
            {
                LogMessage?.Invoke(this, $"Successfully published to '{topic}'.");
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Error publishing to '{topic}': {ex.Message}");
            throw;
        }
    }

    public async Task<MqttClientSubscribeResult> SubscribeAsync(string topicFilter, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Cannot subscribe: Client is not connected.");
            throw new InvalidOperationException("Client is not connected.");
        }

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f.WithTopic(topicFilter).WithQualityOfServiceLevel(qos))
            .Build();

        try
        {
            var result = await _client.SubscribeAsync(subscribeOptions, cancellationToken);
            LogMessage?.Invoke(this, $"Subscription request sent for '{topicFilter}'.");
            return result;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Error subscribing to '{topicFilter}': {ex.Message}");
            throw;
        }
    }

    public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(string topicFilter, CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
        {
            LogMessage?.Invoke(this, "Cannot unsubscribe: Client is not connected.");
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
    public IEnumerable<MqttApplicationMessage>? GetMessagesForTopic(string topic)
    {
        if (_topicBuffers.TryGetValue(topic, out var buffer))
        {
            return buffer.GetMessages().ToList(); // Returns snapshot
        }
        return null;
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
        _topicBuffers.Clear();
        LogMessage?.Invoke(this, "All topic buffers cleared.");
    }

    // --- New Method ---
    /// <summary>
    /// Attempts to retrieve a specific message by its topic and unique identifier.
    /// </summary>
    /// <param name="topic">The topic of the message.</param>
    /// <param name="messageId">The unique identifier of the message.</param>
    /// <param name="message">The retrieved message, or null if not found.</param>
    /// <returns>True if the message was found in the corresponding topic buffer, false otherwise.</returns>
    public bool TryGetMessage(string topic, Guid messageId, out MqttApplicationMessage? message)
    {
        message = null;
        if (_topicBuffers.TryGetValue(topic, out var buffer))
        {
            return buffer.TryGetMessage(messageId, out message);
        }
        return false;
    }
    // ---------------

    private async Task ReconnectAsync()
    {
        LogMessage?.Invoke(this, "Starting reconnection attempts...");
        int reconnectCount = 1;
        while (!_isDisposing && !_client.IsConnected)
        {
            try
            {
                LogMessage?.Invoke(this, $"Attempting to reconnect ({reconnectCount})...");
                await ConnectAsync(CancellationToken.None); // ConnectAsync handles its own logging/state

                // Add delay even after attempt, before checking IsConnected again
                await Task.Delay(TimeSpan.FromMilliseconds(250));

                if (_client.IsConnected) break; // Exit if connected

            }
            catch (Exception ex) // Fallback catch
            {
                LogMessage?.Invoke(this, $"Reconnect attempt {reconnectCount} failed in outer loop: {ex.Message}");
            }

            // Wait before the next attempt
            try
            {
                int delaySeconds = Math.Min(30, 5 * reconnectCount);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), CancellationToken.None); // Loop condition handles disposal
                reconnectCount++;
            } catch (TaskCanceledException)
            {
                 LogMessage?.Invoke(this, "Reconnect delay cancelled.");
                 break;
            }
        }
        LogMessage?.Invoke(this, _client.IsConnected ? "Reconnection successful." : "Reconnection attempts stopped.");
    }


    // Modified message handling logic
    private Task HandleIncomingMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var messageId = Guid.NewGuid(); // Generate unique ID
        var topic = e.ApplicationMessage.Topic;

        // Store message in buffer with the new ID
        var buffer = _topicBuffers.GetOrAdd(topic, _ => new TopicRingBuffer(DefaultMaxTopicBufferSize));
        buffer.AddMessage(e.ApplicationMessage, messageId); // Pass ID to buffer

        // Notify external subscribers with the new event args
        var identifiedArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(
            messageId,
            e.ApplicationMessage,
            e.ClientId
            // Map other properties from 'e' if needed
        );
        MessageReceived?.Invoke(this, identifiedArgs);

        return Task.CompletedTask;
    }

    // --- IDisposable Implementation ---
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposing) return;

        if (disposing)
        {
            LogMessage?.Invoke(this, "Disposing MqttEngine...");
            _isDisposing = true;

            _client.ApplicationMessageReceivedAsync -= HandleIncomingMessageAsync;
            _client.DisconnectedAsync -= OnClientDisconnected;
            _client.ConnectedAsync -= OnClientConnected;

            if (_client.IsConnected)
            {
                try
                {
                    LogMessage?.Invoke(this, "Attempting final disconnect...");
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

            _client?.Dispose();
            LogMessage?.Invoke(this, "MqttEngine disposed.");
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private Task OnClientConnected(MqttClientConnectedEventArgs args)
    {
        LogMessage?.Invoke(this, "Connected successfully.");
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(true, null));
        _ = Task.Run(() => SubscribeToTopicsAsync(CancellationToken.None), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task OnClientDisconnected(MqttClientDisconnectedEventArgs e)
    {
        LogMessage?.Invoke(this, $"Disconnected: {e.ReasonString}. Client Was Connected: {e.ClientWasConnected}");
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, e.Exception));

        // Only attempt reconnect if not disposing and it wasn't a deliberate disconnect by us
        if (!_isDisposing && e.Reason != MqttClientDisconnectReason.NormalDisconnection && e.ClientWasConnected)
        {
             LogMessage?.Invoke(this, "Will attempt reconnection shortly...");
             await Task.Delay(TimeSpan.FromSeconds(5)); // Initial delay before starting reconnect loop
             if (!_isDisposing) // Check again after delay
             {
                 _ = Task.Run(ReconnectAsync);
             }
        }
         else if (!e.ClientWasConnected && !_isDisposing) // Handle case where initial connection might have failed implicitly
        {
             LogMessage?.Invoke(this, "Initial connection likely failed or disconnected immediately. Attempting connection loop.");
             if (!_isDisposing)
             {
                 _ = Task.Run(ReconnectAsync); // Start reconnect attempts
             }
        }
    }
} // End of MqttEngine class
