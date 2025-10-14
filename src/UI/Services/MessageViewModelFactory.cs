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
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (mqttService == null)
            throw new ArgumentNullException(nameof(mqttService));
        if (statusBarService == null)
            throw new ArgumentNullException(nameof(statusBarService));

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

        // Register request messages with response-topic
        if (!string.IsNullOrEmpty(message.ResponseTopic) &&
            message.CorrelationData != null &&
            message.CorrelationData.Length > 0)
        {
            var correlationHex = BitConverter.ToString(message.CorrelationData).Replace("-", "");
            Log.Information(
                "Registering REQUEST message {MessageId} on topic {Topic} with response-topic {ResponseTopic} and correlation-data {CorrelationData}",
                messageId, topic, message.ResponseTopic, correlationHex);

            var registered = await _correlationService.RegisterRequestAsync(
                messageId.ToString(),
                message.CorrelationData,
                message.ResponseTopic,
                ttlMinutes: 30);

            Log.Information(
                "Request registration {Result} for message {MessageId}",
                registered ? "SUCCEEDED" : "FAILED", messageId);

            return registered;
        }
        // Link response messages with correlation-data
        else if (message.CorrelationData != null && message.CorrelationData.Length > 0)
        {
            var correlationHex = BitConverter.ToString(message.CorrelationData).Replace("-", "");
            Log.Information(
                "Linking RESPONSE message {MessageId} on topic {Topic} with correlation-data {CorrelationData}",
                messageId, topic, correlationHex);

            var linked = await _correlationService.LinkResponseAsync(
                messageId.ToString(),
                message.CorrelationData,
                topic);

            Log.Information(
                "Response linking {Result} for message {MessageId}",
                linked ? "SUCCEEDED" : "FAILED", messageId);

            return linked;
        }

        return false;
    }
}
