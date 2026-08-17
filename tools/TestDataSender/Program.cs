using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Protocol;

var host = Environment.GetEnvironmentVariable("MQTT_HOST");
var portText = Environment.GetEnvironmentVariable("MQTT_PORT");
var useTlsText = Environment.GetEnvironmentVariable("MQTT_USE_TLS");
var azureHost = Environment.GetEnvironmentVariable("AZURE_MQTT_HOST");
var azurePortText = Environment.GetEnvironmentVariable("AZURE_MQTT_PORT");
var azureToken = Environment.GetEnvironmentVariable("AZURE_MQTT_TOKEN");

if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portText, out var port))
{
    Console.Error.WriteLine("MQTT_HOST and MQTT_PORT environment variables are required.");
    return 1;
}

var useTls = bool.TryParse(useTlsText, out var parsedUseTls) && parsedUseTls;
var delaySeconds = args.Length > 0 && int.TryParse(args[0], out var parsedDelay) ? parsedDelay : 30;

Console.WriteLine($"Waiting {delaySeconds} seconds for broker and clients to be ready...");
await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

var optionsBuilder = new MqttClientOptionsBuilder()
    .WithTcpServer(host, port)
    .WithClientId($"crows-nest-test-data-{Guid.NewGuid():N}");

if (useTls)
{
    optionsBuilder.WithTlsOptions(new MqttClientTlsOptions
    {
        UseTls = true,
        AllowUntrustedCertificates = true,
        IgnoreCertificateChainErrors = true,
        IgnoreCertificateRevocationErrors = true,
        CertificateValidationHandler = _ => true,
    });
}

var client = new MqttClientFactory().CreateMqttClient();
await client.ConnectAsync(optionsBuilder.Build());

var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var testDataDirectory = Path.Combine(repositoryRoot, "tests", "TestData");
var treasureResponseTopic = "test/pirate/ship/response/treasure-map";
var crewStatusResponseTopic = "test/pirate/ship/response/crew-status";
var treasureCorrelationData = Guid.NewGuid().ToByteArray();
var crewStatusCorrelationData = Guid.NewGuid().ToByteArray();

await PublishTestSuiteAsync();

await client.DisconnectAsync(new MqttClientDisconnectOptions());
client.Dispose();

if (!string.IsNullOrWhiteSpace(azureHost)
    && int.TryParse(azurePortText, out var azurePort)
    && !string.IsNullOrWhiteSpace(azureToken))
{
    Console.WriteLine($"Publishing test data to Azure Event Grid mock at {azureHost}:{azurePort}...");
    var azureOptions = new MqttClientOptionsBuilder()
        .WithTcpServer(azureHost, azurePort)
        .WithClientId($"crows-nest-azure-test-data-{Guid.NewGuid():N}")
        .WithEnhancedAuthentication("OAUTH2-JWT", Encoding.UTF8.GetBytes(azureToken))
        .WithTlsOptions(new MqttClientTlsOptions
        {
            UseTls = true,
            AllowUntrustedCertificates = true,
            IgnoreCertificateChainErrors = true,
            IgnoreCertificateRevocationErrors = true,
            CertificateValidationHandler = _ => true,
        })
        .Build();

    client = new MqttClientFactory().CreateMqttClient();
    await client.ConnectAsync(azureOptions);
    await PublishTestSuiteAsync();
    await client.DisconnectAsync(new MqttClientDisconnectOptions());
    client.Dispose();
}

async Task PublishTestSuiteAsync()
{
await PublishFileAsync("test/viewer/image", "image/png", "test-image.png");
await PublishFileAsync("test/viewer/video", "video/mp4", "test-video.mp4");
await PublishFileAsync("test/viewer/json", "application/json", "test-struct.json");
await PublishFileAsync("test/viewer/hex", "application/octet-stream", "story.7z");
await PublishAsync("test/viewer/raw", "text/plain", Encoding.UTF8.GetBytes("Crow's NestMQTT test message"));
await PublishAsync("test/retain", "application/json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { messageType = "retained", timestamp = DateTimeOffset.UtcNow })), retain: true);

await PublishAsync(
    "test/pirate/ship/request/treasure-map",
    "application/json",
    JsonSerializer.SerializeToUtf8Bytes(new
    {
        messageType = "treasure_map_request",
        shipName = "The Crow's Nest",
        requestId = Guid.NewGuid(),
        timestamp = DateTimeOffset.UtcNow,
    }),
    responseTopic: treasureResponseTopic,
    correlationData: treasureCorrelationData);

await PublishAsync(
    "test/pirate/ship/request/crew-status",
    "application/json",
    JsonSerializer.SerializeToUtf8Bytes(new
    {
        messageType = "crew_status_request",
        shipName = "The Crow's Nest",
        requestId = Guid.NewGuid(),
        timestamp = DateTimeOffset.UtcNow,
    }),
    responseTopic: crewStatusResponseTopic,
    correlationData: crewStatusCorrelationData);

await PublishAsync(
    treasureResponseTopic,
    "application/json",
    JsonSerializer.SerializeToUtf8Bytes(new
    {
        messageType = "treasure_map_response",
        status = "success",
        timestamp = DateTimeOffset.UtcNow,
    }),
    correlationData: treasureCorrelationData);

await PublishAsync(
    "test/userprops/demo",
    "application/json",
    JsonSerializer.SerializeToUtf8Bytes(new
    {
        messageType = "user_properties_test",
        description = "Testing MQTT 5 user properties display",
        timestamp = DateTimeOffset.UtcNow,
        data = new { testValue = 42, testString = "Hello, World!" },
    }),
    userProperties: new Dictionary<string, string>
    {
        ["sent-at"] = DateTimeOffset.UtcNow.ToString("O"),
        ["sender"] = "TestDataSender",
        ["version"] = "1.0.0",
    });

foreach (var expirySeconds in new uint[] { 5, 30, 90 })
{
    await PublishAsync(
        $"test/expiry/{expirySeconds}s",
        "application/octet-stream",
        [],
        messageExpiryInterval: expirySeconds);
}
}

Console.WriteLine("Test data sent successfully.");
return 0;

async Task PublishFileAsync(string topic, string contentType, string filename)
{
    await PublishAsync(topic, contentType, await File.ReadAllBytesAsync(Path.Combine(testDataDirectory, filename)));
}

async Task PublishAsync(
    string topic,
    string contentType,
    byte[] payload,
    bool retain = false,
    string? responseTopic = null,
    byte[]? correlationData = null,
    IReadOnlyDictionary<string, string>? userProperties = null,
    uint? messageExpiryInterval = null)
{
    var messageBuilder = new MqttApplicationMessageBuilder()
        .WithTopic(topic)
        .WithPayload(payload)
        .WithContentType(contentType)
        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
        .WithRetainFlag(retain);

    if (responseTopic is not null)
    {
        messageBuilder.WithResponseTopic(responseTopic);
    }

    if (correlationData is not null)
    {
        messageBuilder.WithCorrelationData(correlationData);
    }

    if (userProperties is not null)
    {
        foreach (var (name, value) in userProperties)
        {
            messageBuilder.WithUserProperty(name, Encoding.UTF8.GetBytes(value));
        }
    }

    if (messageExpiryInterval.HasValue)
    {
        messageBuilder.WithMessageExpiryInterval(messageExpiryInterval.Value);
    }

    await client.PublishAsync(messageBuilder.Build());
    Console.WriteLine($"Sent {topic} ({payload.Length} bytes).");
}