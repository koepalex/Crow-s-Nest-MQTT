using System.Runtime.CompilerServices; // Added for InternalsVisibleTo

[assembly: InternalsVisibleTo("UnitTests")] // Added for testing internal methods

namespace CrowsNestMqtt.BusinessLogic;

using System.Collections.Concurrent;
using CrowsNestMqtt.BusinessLogic.Configuration; // Required for AuthenticationMode
using CrowsNestMqtt.BusinessLogic.Services;
using MQTTnet;
using MQTTnet.Protocol;
using CrowsNestMqtt.Utils;
using System.Collections.Generic;
using System.Text;

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
    private CancellationTokenSource? _connectionCts; // To control the entire connection/reconnection cycle
    private readonly object _reconnectLock = new object();
    private bool _isReconnectLoopRunning = false;
    private readonly ConcurrentDictionary<string, TopicRingBuffer> _topicBuffers;
    private IList<TopicBufferLimit> _topicSpecificBufferLimits = new List<TopicBufferLimit>();
    internal const long DefaultMaxTopicBufferSize = 1 * 1024 * 1024; // Changed to internal const

    // Batch processing for high-volume message scenarios  
    private readonly ConcurrentQueue<MqttApplicationMessageReceivedEventArgs> _pendingMessages = new();
    private readonly Timer _messageProcessingTimer;
    private readonly Dictionary<string, long> _topicBufferSizeCache = new(); // Cache buffer sizes to avoid repeated calculations

    // Modified event signature
    public event EventHandler<IdentifiedMqttApplicationMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler<string>? LogMessage;

    public MqttEngine(MqttConnectionSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _topicSpecificBufferLimits = settings.TopicSpecificBufferLimits;
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
        _topicBuffers = new ConcurrentDictionary<string, TopicRingBuffer>();

        _client.ApplicationMessageReceivedAsync += HandleIncomingMessageAsync;
        _client.ConnectedAsync += OnClientConnected;
        _client.DisconnectedAsync += OnClientDisconnected;

        _messageProcessingTimer = new Timer(ProcessMessageBatch, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
    }

    // ... (UpdateSettings, SubscribeToTopicsAsync, BuildMqttOptions, ConnectAsync, DisconnectAsync, PublishAsync, SubscribeAsync, UnsubscribeAsync remain largely the same) ...
    // UpdateSettings method
    public void UpdateSettings(MqttConnectionSettings newSettings)
    {
        _settings = newSettings ?? throw new ArgumentNullException(nameof(newSettings));
        _topicSpecificBufferLimits = newSettings.TopicSpecificBufferLimits;
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
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, ex, ConnectionStatusState.Disconnected));
        }
    }

    // Builds MqttClientOptions based on current settings
    private MqttClientOptions BuildMqttOptions()
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.Hostname, _settings.Port)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithKeepAlivePeriod(_settings.KeepAliveInterval)
            .WithCleanSession(_settings.CleanSession)
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);

        if (_settings.SessionExpiryInterval.HasValue)
        {
                builder.WithSessionExpiryInterval(_settings.SessionExpiryInterval.Value);
        }

        if (!string.IsNullOrWhiteSpace(_settings.ClientId))
        {
            builder.WithClientId(_settings.ClientId);
        }

        // TLS support
        if (_settings.UseTls)
        {
            var tlsOptions = new MqttClientTlsOptions
            {
                UseTls = true,
                AllowUntrustedCertificates = true,
                IgnoreCertificateChainErrors = true,
                IgnoreCertificateRevocationErrors = true,
                SslProtocol = System.Security.Authentication.SslProtocols.None, //let OS choose
                CertificateValidationHandler = _ => true,
            };
            builder.WithTlsOptions(tlsOptions);
        }

        // Add credentials based on AuthMode
        switch (_settings.AuthMode)
        {
            case UsernamePasswordAuthenticationMode upa:
                if (!string.IsNullOrWhiteSpace(upa.Username)) // Optional: only set if username is not blank
                {
                    builder.WithCredentials(upa.Username, upa.Password);
                }
                break;
            case EnhancedAuthenticationMode enhancedAuth:
                if (!string.IsNullOrEmpty(enhancedAuth.AuthenticationMethod) &&
                    !string.IsNullOrEmpty(enhancedAuth.AuthenticationData))
                {
                    builder.WithEnhancedAuthentication(
                        enhancedAuth.AuthenticationMethod,
                        Encoding.UTF8.GetBytes(enhancedAuth.AuthenticationData));
                }
                break;
            case AnonymousAuthenticationMode:
                // No credentials to add for anonymous mode
                break;
            // Default case can be omitted if all AuthenticationMode types are handled
        }

        return builder.Build();
    }

public async Task ConnectAsync(CancellationToken cancellationToken = default)
{
    if (_client.IsConnected)
    {
        LogMessage?.Invoke(this, "Already connected.");
        return;
    }

    // Cancel any previous attempts before starting a new one.
    _connectionCts?.Cancel();
    _connectionCts?.Dispose();
    _connectionCts = new CancellationTokenSource();
    var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCts.Token).Token;

    try
    {
        // Announce that we are starting the connection process.
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, null, ConnectionStatusState.Connecting));
        
        _currentOptions = BuildMqttOptions();
        LogMessage?.Invoke(this, $"Attempting to connect to {_currentOptions.ChannelOptions} with ClientId '{_currentOptions.ClientId ?? "<generated>"}'.");
        
        // This call will either succeed and trigger OnClientConnected,
        // fail and trigger OnClientDisconnected, or be cancelled.
        await _client.ConnectAsync(_currentOptions, combinedToken);
    }
    catch (OperationCanceledException)
    {
        // This is expected if the user cancels. The OnClientDisconnected handler will set the final state.
        LogMessage?.Invoke(this, "Connection attempt was cancelled.");
    }
    catch (Exception ex)
    {
        // For any other exception, the OnClientDisconnected handler will be triggered by the library,
        // where it will log the error and set the state.
        LogMessage?.Invoke(this, $"Connection attempt failed: {ex.Message}");
    }
}

public async Task DisconnectAsync(CancellationToken cancellationToken = default)
{
    // This method now has a single responsibility: to signal that the user wants to stop.
    // It cancels the master token for this connection cycle.
    LogMessage?.Invoke(this, "Disconnect/Cancel requested by user.");
    _connectionCts?.Cancel();

    // If the client is already connected, we can also initiate a graceful disconnect.
    // The OnClientDisconnected handler will still fire, but this can speed up the process.
    if (_client.IsConnected)
    {
        await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), cancellationToken);
    }
    else
    {
        // If not connected (i.e., in Connecting state), immediately set state to Disconnected
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, null, ConnectionStatusState.Disconnected));
    }
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

    internal static int MatchTopic(string topic, string filter) // Changed to internal static
    {
        if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(filter))
        {
            return -1;
        }

        if (filter == topic)
        {
            return 1000; // Exact match score
        }

        var topicSegments = topic.Split('/');
        var filterSegments = filter.Split('/');

        int score = 0;
        int i = 0; // topic segment index
        int j = 0; // filter segment index

        while (i < topicSegments.Length && j < filterSegments.Length)
        {
            if (filterSegments[j] == "#")
            {
                if (j == filterSegments.Length - 1) // '#' must be the last segment in the filter.
                {
                    // The '#' matches the rest of the topic segments.
                    // Score for '#' (1) will be added post-loop if this condition leads to a match.
                    i = topicSegments.Length; // Mark all remaining topic segments as "matched" by '#'.
                    break; // Exit loop; post-loop logic will determine final score.
                }
                else
                {
                    return -1; // '#' is not the last segment, invalid filter for this context.
                }
            }

            if (filterSegments[j] == topicSegments[i])
            {
                score += 10;
            }
            else if (filterSegments[j] == "+")
            {
                score += 5;
            }
            else
            {
                return -1; // Segments do not match.
            }
            i++;
            j++;
        }

        // After loop, check conditions for a valid match.

        // Case 1: All segments in both topic and filter have been processed and matched.
        if (i == topicSegments.Length && j == filterSegments.Length)
        {
            return score;
        }

        // Case 2: Filter ended with '#' (so j is at the '#' segment) and all topic segments were covered.
        // This covers both "topic/sub" vs "topic/#" (where '#' matches "sub")
        // and "topic" vs "topic/#" (where '#' matches zero levels).
        if (j == filterSegments.Length - 1 && filterSegments[j] == "#" && i == topicSegments.Length)
        {
            return score + 1; // Add score for the '#' wildcard itself.
        }
        
        // Case 3: Topic has more segments, but filter ended before '#'. (e.g. "a/b/c" vs "a/b")
        // This is implicitly handled as not a match by falling through if not covered above.

        // Case 4: Filter has more segments, but topic ended. (e.g. "a/b" vs "a/b/c")
        // This is also implicitly handled as not a match.

        return -1; // No match based on the rules.
    }

    internal virtual long GetMaxBufferSizeForTopic(string topic) // Changed to internal virtual
    {
        long bestMatchSize = DefaultMaxTopicBufferSize;
        int bestMatchScore = -1;

        if (_topicSpecificBufferLimits == null) return bestMatchSize;

        foreach (var rule in _topicSpecificBufferLimits)
        {
            if (string.IsNullOrEmpty(rule.TopicFilter)) continue;

            int currentScore = MatchTopic(topic, rule.TopicFilter);
            if (currentScore > bestMatchScore)
            {
                bestMatchScore = currentScore;
                bestMatchSize = rule.MaxSizeBytes;
            }
        }
        return bestMatchSize;
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogMessage?.Invoke(this, "Starting reconnection attempts...");
            int reconnectCount = 1;
            int delaySeconds = 5;

            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(
                false, null, ConnectionStatusState.Connecting,
                $"Connection lost. Attempting to reconnect... (Attempt {reconnectCount}, next in {delaySeconds}s)"
            ));

            // Initial delay before the first retry in the loop
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

            while (!cancellationToken.IsCancellationRequested && !_isDisposing && !_client.IsConnected)
            {
                try
                {
                    var attemptMessage = $"Reconnect attempt {reconnectCount}...";
                    LogMessage?.Invoke(this, attemptMessage);
                    ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(
                        false, null, ConnectionStatusState.Connecting,
                        $"Attempting to reconnect (Attempt {reconnectCount})..."
                    ));

                    // Use the existing options, don't call the public ConnectAsync
                    if (_currentOptions != null)
                    {
                        await _client.ConnectAsync(_currentOptions, cancellationToken);
                    }
                    else
                    {
                        LogMessage?.Invoke(this, "Cannot reconnect, options are not set. Aborting.");
                        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(
                            false, null, ConnectionStatusState.Disconnected,
                            "Cannot reconnect, options not set."
                        ));
                        break;
                    }

                    if (_client.IsConnected) break; // Exit if connected
                }
                catch (OperationCanceledException)
                {
                    LogMessage?.Invoke(this, "Reconnection was cancelled.");
                    break; // Exit the loop if cancellation is requested
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke(this, $"Reconnect attempt {reconnectCount} failed: {ex.Message}");
                    ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(
                        false, ex, ConnectionStatusState.Connecting,
                        $"Reconnect attempt {reconnectCount} failed: {ex.Message}"
                    ));
                }
                reconnectCount++;

                if (cancellationToken.IsCancellationRequested) break;

                // Wait before the next attempt
                delaySeconds = Math.Min(30, 5 * reconnectCount);
                var backoffMessage = $"Next reconnect attempt in {delaySeconds} seconds (Attempt {reconnectCount})...";
                LogMessage?.Invoke(this, backoffMessage);
                ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(
                    false, null, ConnectionStatusState.Connecting,
                    $"Waiting {delaySeconds}s before next reconnect (Attempt {reconnectCount})"
                ));
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }

            // The loop has exited. Now, determine the final state.
            if (cancellationToken.IsCancellationRequested)
            {
                LogMessage?.Invoke(this, "Reconnection attempts stopped due to user cancellation.");
                // The state has already been set to Disconnected by the DisconnectAsync method.
            }
            else if (!_client.IsConnected)
            {
                LogMessage?.Invoke(this, "Reconnection attempts failed after multiple retries.");
                // The loop finished without success and without being cancelled. We are now officially disconnected.
                ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(
                    false, null, ConnectionStatusState.Disconnected,
                    "Reconnection attempts failed. Please check your network or broker settings."
                ));
            }
        }
        finally
        {
            lock (_reconnectLock)
            {
                _isReconnectLoopRunning = false;
            }
        }
    }

    // Modified message handling logic with batching for performance
    private Task HandleIncomingMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        e.AutoAcknowledge = true;
        // Add message to batch queue for processing
        _pendingMessages.Enqueue(e);
        
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// Processes batched MQTT messages for improved performance during high-volume scenarios
    /// </summary>
    private void ProcessMessageBatch(object? state)
    {
        if (_pendingMessages.Count == 0) return;

        var messagesToProcess = new List<MqttApplicationMessageReceivedEventArgs>();
        
        // Adaptive batch size - larger batches for high volume
        int maxBatchSize = 75;
        int count = 0;

        while (_pendingMessages.TryDequeue(out var message) && count < maxBatchSize)
        {
            messagesToProcess.Add(message);
            count++;
        }
        
        // Process the batch
        if (messagesToProcess.Count > 0)
        {
            ProcessMessageBatchInternal(messagesToProcess);
        }
    }
    
    /// <summary>
    /// Internal method to process a batch of messages efficiently
    /// </summary>
    private void ProcessMessageBatchInternal(List<MqttApplicationMessageReceivedEventArgs> messages)
    {
        var eventArgsToFire = new List<IdentifiedMqttApplicationMessageReceivedEventArgs>();
        var topicStats = new Dictionary<string, int>(); // For logging
        
        foreach (var e in messages)
        {
            var messageId = Guid.NewGuid(); // Generate unique ID
            var topic = e.ApplicationMessage.Topic;
            
            // Use cached buffer size or calculate once per topic
            if (!_topicBufferSizeCache.TryGetValue(topic, out var bufferSize))
            {
                bufferSize = GetMaxBufferSizeForTopic(topic);
                _topicBufferSizeCache[topic] = bufferSize;
            }
            
            // Store message in buffer with the new ID
            var buffer = _topicBuffers.GetOrAdd(topic, _ => new TopicRingBuffer(bufferSize));
            buffer.AddMessage(e.ApplicationMessage, messageId);
            
            // Prepare event args for batch firing
            var identifiedArgs = new IdentifiedMqttApplicationMessageReceivedEventArgs(
                messageId,
                e.ApplicationMessage,
                e.ClientId
            );
            eventArgsToFire.Add(identifiedArgs);
            
            // Track topic stats for efficient logging
            topicStats.TryGetValue(topic, out var count);
            topicStats[topic] = count + 1;
        }
        
        // Batch fire events
        foreach (var args in eventArgsToFire)
        {
            MessageReceived?.Invoke(this, args);
        }
        
        // Efficient logging - log per topic instead of per message
        // if (topicStats.Count <= 10) // Only log details for reasonable number of topics
        // {
        //     foreach (var kvp in topicStats)
        //     {
        //         var topic = kvp.Key;
        //         var messageCount = kvp.Value;
        //         var bufferSize = _topicBufferSizeCache[topic];
                
        //         LogMessage?.Invoke(this, $"Processed {messageCount} messages for topic '{topic}': Using buffer size {bufferSize} bytes");
        //     }
        // }
        // else
        // {
        //     // High topic diversity - log summary only
        //     var totalMessages = messages.Count;
        //     var uniqueTopics = topicStats.Count;
        //     LogMessage?.Invoke(this, $"Processed batch: {totalMessages} messages across {uniqueTopics} topics");
        // }
    }

   // --- IDisposable Implementation ---
   // Made protected internal virtual for testability with NSubstitute
   protected internal virtual void Dispose(bool disposing)
   {
       if (_isDisposing) return;

        if (disposing)
        {
            LogMessage?.Invoke(this, "Disposing MqttEngine...");
            _isDisposing = true;

            _messageProcessingTimer.Dispose();
            _pendingMessages.Clear();

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
    // Reset reconnect loop flag on successful connection
    lock (_reconnectLock)
    {
        _isReconnectLoopRunning = false;
    }

    LogMessage?.Invoke(this, "Connected successfully.");
    ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(true, null, ConnectionStatusState.Connected));
    
    var token = _connectionCts?.Token ?? CancellationToken.None;
    if (!token.IsCancellationRequested)
    {
        _ = Task.Run(() => SubscribeToTopicsAsync(token), token);
    }
    
    return Task.CompletedTask;
}

    private Task OnClientDisconnected(MqttClientDisconnectedEventArgs e)
    {
        LogMessage?.Invoke(this, $"Disconnected: {e.ReasonString}. Client Was Connected: {e.ClientWasConnected}");

        // If the disconnect was intentional (user clicked Disconnect/Cancel) or we are disposing,
        // then set the state to Disconnected and do not attempt to reconnect.
        if (_isDisposing || (_connectionCts?.IsCancellationRequested ?? true))
        {
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, e.Exception, ConnectionStatusState.Disconnected));
            return Task.CompletedTask;
        }

        // If the disconnect was unintentional, start the reconnect process.
        lock (_reconnectLock)
        {
            if (_isReconnectLoopRunning)
            {
                LogMessage?.Invoke(this, "Disconnected, but reconnect loop is already active.");
                return Task.CompletedTask;
            }
            _isReconnectLoopRunning = true;
        }

        LogMessage?.Invoke(this, "Will attempt reconnection shortly...");
        ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, e.Exception, ConnectionStatusState.Connecting));

        // Run the reconnect logic in a background task.
        _ = Task.Run(() => ReconnectAsync(_connectionCts.Token), _connectionCts.Token);
        
        return Task.CompletedTask;
    }
} // End of MqttEngine class
