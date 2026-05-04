using CrowsNestMqtt.BusinessLogic;
using CrowsNestMqtt.BusinessLogic.Contracts;
using CrowsNestMqtt.BusinessLogic.Models;
using CrowsNestMqtt.BusinessLogic.Services;
using CrowsNestMqtt.UI.ViewModels;
using MQTTnet;
using Serilog;
using System.Buffers;
using System.Text;

namespace CrowsNestMqtt.UI.Services;

/// <summary>
/// Factory for creating MessageViewModel instances from MQTT messages.
/// </summary>
public class MessageViewModelFactory : IMessageViewModelFactory
{
    private readonly IMessageCorrelationService? _correlationService;

    public MessageViewModelFactory(IMessageCorrelationService? correlationService = null)
    {
        _correlationService = correlationService;
    }

    public MessageViewModel CreateMessageViewModel(
        IdentifiedMqttApplicationMessageReceivedEventArgs message,
        IMqttService mqttService,
        IStatusBarService statusBarService)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(mqttService);
        ArgumentNullException.ThrowIfNull(statusBarService);

        var payloadBytes = message.ApplicationMessage.Payload.ToArray();
        var preview = GeneratePayloadPreview(payloadBytes);

        // Remove all newline characters (both \r and \n)
        preview = preview.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

        return new MessageViewModel(
            message.MessageId,
            message.Topic,
            DateTime.Now,
            preview,
            payloadBytes.Length,
            mqttService,
            statusBarService,
            message.ApplicationMessage,
            enableFallbackFullMessage: true,
            message.IsEffectivelyRetained);
    }

    public string GeneratePayloadPreview(byte[] payload, int maxLength = 100)
    {
        if (payload == null || payload.Length == 0)
            return "[No Payload]";

        string preview;
        try
        {
            // Use a decoder with throw-on-invalid to detect binary data
            var decoder = Encoding.UTF8.GetDecoder();
            decoder.Fallback = DecoderFallback.ExceptionFallback;
            var chars = new char[payload.Length * 2]; // Worst case
            decoder.GetChars(payload, 0, payload.Length, chars, 0, flush: true);
            preview = new string(chars, 0, decoder.GetCharCount(payload, 0, payload.Length, flush: true));
        }
        catch (DecoderFallbackException)
        {
            return $"[Binary Data: {payload.Length} bytes]";
        }

        if (preview.Length > maxLength)
        {
            return preview.Substring(0, maxLength) + "...";
        }

        return preview;
    }

    public async Task<bool> RegisterCorrelationAsync(Guid messageId, MqttApplicationMessage message, string topic)
    {
        if (_correlationService == null || message == null)
            return false;

        if (message.CorrelationData == null || message.CorrelationData.Length == 0)
            return false;

        var correlationHex = BitConverter.ToString(message.CorrelationData).Replace("-", "");

        // Try to link as a response first (handles responses that echo ResponseTopic)
        bool linked = false;
        try
        {
            linked = await _correlationService.LinkResponseAsync(
                messageId.ToString(),
                message.CorrelationData,
                topic).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // LinkResponseAsync throws if responseTopic is null/empty — not a valid response
        }

        if (linked)
        {
            Log.Information(
                "Linked RESPONSE message {MessageId} on topic {Topic} with correlation-data {CorrelationData}",
                messageId, topic, correlationHex);
            return true;
        }

        // Not a response (or no matching request yet) — register as a new request if it has ResponseTopic
        if (!string.IsNullOrEmpty(message.ResponseTopic))
        {
            Log.Information(
                "Registering REQUEST message {MessageId} on topic {Topic} with response-topic {ResponseTopic} and correlation-data {CorrelationData}",
                messageId, topic, message.ResponseTopic, correlationHex);

            var registered = await _correlationService.RegisterRequestAsync(
                messageId.ToString(),
                message.CorrelationData,
                message.ResponseTopic,
                ttlMinutes: 30).ConfigureAwait(false);

            Log.Information(
                "Request registration {Result} for message {MessageId}",
                registered ? "SUCCEEDED" : "FAILED", messageId);

            return registered;
        }

        Log.Information(
            "Unlinked message {MessageId} on topic {Topic} with correlation-data {CorrelationData} (no matching request and no response-topic)",
            messageId, topic, correlationHex);

        return false;
    }
}
