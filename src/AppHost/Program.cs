var builder = DistributedApplication.CreateBuilder(args);

// Add EMQX MQTT v5 broker as a Docker container with dynamic ports
var mqttBroker = builder.AddContainer("mqtt", "emqx/emqx", "latest")
    .WithEndpoint(targetPort: 1883, name: "default", scheme: "mqtt")
    .WithHttpEndpoint(targetPort: 18083, name: "dashboard")
    .WithEnvironment("EMQX_ALLOW_ANONYMOUS", "true")
    .WithEnvironment("EMQX_AUTHORIZATION__NO_MATCH", "allow")
    .WithEnvironment("EMQX_AUTHORIZATION__SOURCES", "[]") // Disable file-based ACL to allow subscribe to #
    .WithEnvironment("EMQX_LISTENER__TCP__EXTERNAL", "1883")
    .WithEnvironment("EMQX_MQTT__MAX_PACKET_SIZE", "10485760"); // 10MB max packet size

// Get the MQTT endpoint for passing to the client application
var mqttEndpoint = mqttBroker.GetEndpoint("default");

// Add CrowsNestMqtt as a project that auto-connects to the MQTT broker
builder.AddProject<Projects.CrowsNestMqtt_App>("crows-nest-mqtt")
    .WithReference(mqttEndpoint)
    .WaitFor(mqttBroker);

builder.Build().Run();
