var builder = DistributedApplication.CreateBuilder(args);

// Add EMQX MQTT v5 broker as a Docker container with dynamic ports
var mqttBroker = builder.AddContainer("mqtt", "emqx/emqx", "latest")
    .WithEndpoint(targetPort: 1883, name: "mqtt", scheme: "mqtt")
    .WithHttpEndpoint(targetPort: 18083, name: "dashboard")
    .WithEnvironment("EMQX_ALLOW_ANONYMOUS", "true")
    .WithEnvironment("EMQX_AUTHORIZATION__NO_MATCH", "allow")
    .WithEnvironment("EMQX_AUTHORIZATION__SOURCES", "[]") // Disable file-based ACL to allow subscribe to #
    .WithEnvironment("EMQX_LISTENER__TCP__EXTERNAL", "1883")
    .WithEnvironment("EMQX_MQTT__MAX_PACKET_SIZE", "10485760"); // 10MB max packet size

// Get the MQTT endpoint for passing to the client application
var mqttEndpoint = mqttBroker.GetEndpoint("mqtt");

// Add CrowsNestMqtt as a project that auto-connects to the MQTT broker
builder.AddProject<Projects.CrowsNestMqtt_App>("crows-nest-mqtt")
    .WithReference(mqttEndpoint)
    .WaitFor(mqttBroker)
    .WithEnvironment("CROWSNEST__USE_TLS", "false")
    .WithEnvironment("CROWSNEST__AUTH_MODE", "anonymous")
    .WithEnvironment("CROWSNEST__CLIENT_ID", "")
    .WithEnvironment("CROWSNEST__KEEP_ALIVE_SECONDS", "0")
    .WithEnvironment("CROWSNEST__CLEAN_SESSION", "true")
    .WithEnvironment("CROWSNEST__SESSION_EXPIRY_SECONDS", "0")
    .WithEnvironment("CROWSNEST__SUBSCRIPTION_QOS", "1")
    .WithEnvironment("CROWSNEST__TOPIC_BUFFER_LIMITS", """[{"TopicFilter":"#","MaxSizeBytes":11048576}]""");

builder.Build().Run();
