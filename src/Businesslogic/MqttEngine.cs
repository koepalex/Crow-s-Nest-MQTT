using MQTTnet;
// using MQTTnet.Client; // Removed incorrect namespace
using MQTTnet.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrowsNestMqtt.Utils; // Added for TopicRingBuffer

namespace CrowsNestMqtt.BusinessLogic;

public class MqttEngine
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly ConcurrentDictionary<string, TopicRingBuffer> _topicBuffers; // Added buffer storage
    private const long DefaultMaxTopicBufferSize = 10 * 1024 * 1024; // 10 MB - Added default size

    public event EventHandler<MqttApplicationMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<MqttConnectionStateChangedEventArgs>? ConnectionStateChanged; // Renamed/Refined event
    public event EventHandler<string>? LogMessage; // Added for logging internal info/errors

    public MqttEngine(string brokerHost, int brokerPort)
    {
        var factory = new MqttClientFactory();
        _options = new MqttClientOptionsBuilder() // Use new MqttClientOptionsBuilder()
            .WithTcpServer(brokerHost, brokerPort)
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            // Add other options as needed (e.g., credentials, KeepAlive)
            .WithCleanSession(true) // Example: Configure CleanSession
            .Build();

        _client = factory.CreateMqttClient();
        _topicBuffers = new ConcurrentDictionary<string, TopicRingBuffer>(); // Initialize buffer dictionary

        _client.ApplicationMessageReceivedAsync += (e =>
        {
            // Store message in buffer
            var topic = e.ApplicationMessage.Topic;
            var buffer = _topicBuffers.GetOrAdd(topic, _ => new TopicRingBuffer(DefaultMaxTopicBufferSize));
            buffer.AddMessage(e.ApplicationMessage);

            // Notify subscribers (e.g., UI)
            MessageReceived?.Invoke(this, e);

            // No need to await Task.CompletedTask, just return it.
            return Task.CompletedTask;
        });

        // Removed ConnectingFailedAsync handler. Initial connection failures
        // are caught in the ConnectAsync method's try-catch block.

        _client.DisconnectedAsync += (async e =>
        {
            LogMessage?.Invoke(this, $"Disconnected: {e.ReasonString}. Reconnecting...");
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, e.Exception));

            // Optional: Wait a bit before starting reconnect attempts
            await Task.Delay(TimeSpan.FromSeconds(5));
            // Start reconnect attempts (fire and forget or manage the task)
            _ = Task.Run(ReconnectAsync); // Use Task.Run to avoid blocking the handler
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

    public async Task ConnectAsync()
    {
        try
        {
            LogMessage?.Invoke(this, $"Attempting to connect to {_options.ChannelOptions}...");
            await _client.ConnectAsync(_options, CancellationToken.None);
            // Subscription is now handled by the ConnectedAsync handler
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"Connection attempt failed: {ex.Message}");
            ConnectionStateChanged?.Invoke(this, new MqttConnectionStateChangedEventArgs(false, ex));
            // Reconnect logic will be triggered by DisconnectedAsync if the connection drops later
            // Or by ConnectingFailedAsync if the initial connection fails.
        }
    }

    public async Task DisconnectAsync()
    {
        // Use default disconnect options (ReasonCode=NormalDisconnection, no UserProperties)
        await _client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build());
        LogMessage?.Invoke(this, "Disconnect requested by user.");
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