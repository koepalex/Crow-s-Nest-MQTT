# Test Configuration Guide

## MQTT Broker Configuration for Tests

The MqttEngineTests use configuration settings from `appsettings.test.json` to connect to an MQTT broker during integration tests.

### Configuration File: `appsettings.test.json`

```json
{
  "TestMqttBroker": {
    "Hostname": "localhost",
    "Port": 1883,
    "Description": "MQTT broker settings for integration tests. Change these values to point to your test MQTT broker."
  }
}
```

### Changing Test MQTT Broker Settings

To change the MQTT broker used for testing:

1. Open `tests/UnitTests/appsettings.test.json`
2. Modify the `Hostname` and `Port` values to point to your MQTT broker
3. The changes will be automatically picked up by the test configuration system

### Test Categories

- **Unit Tests**: Tests without `[Trait("Category", "LocalOnly")]` run without requiring an MQTT broker
- **Integration Tests**: Tests marked with `[Trait("Category", "LocalOnly")]` require a running MQTT broker at the configured hostname and port

### Running Tests

```bash
# Run all tests (requires MQTT broker)
dotnet test tests/UnitTests/

# Run only unit tests (no MQTT broker needed)
dotnet test tests/UnitTests/ --filter "Category!=LocalOnly"

# Run only integration tests (requires MQTT broker)
dotnet test tests/UnitTests/ --filter "Category=LocalOnly"
```

### Important Notes about MQTT Engine Testing

The `MqttEngine` automatically subscribes to the `#` wildcard topic when it connects, which means it receives **all messages** published to the broker. This is important to understand when writing tests:

1. **Test Isolation**: Tests may receive messages from previous test runs or other clients connected to the same broker
2. **Message Filtering**: Integration tests should filter received messages to only count the specific topics they're testing
3. **Timing**: Allow sufficient time after connecting for the automatic `#` subscription to become active before publishing test messages

### Example Test Pattern

```csharp
engine.MessageReceived += (sender, args) =>
{
    // Only count messages for our specific test topics
    if (args.ApplicationMessage.Topic == "test/my/topic1" || 
        args.ApplicationMessage.Topic == "test/my/topic2")
    {
        // Handle test-specific messages
    }
};
```

### Setting up a Local MQTT Broker for Testing
```

For local development, you can use:

1. **Mosquitto** (lightweight MQTT broker)
   ```bash
   # Windows (with chocolatey)
   choco install mosquitto
   
   # Ubuntu/Debian
   sudo apt-get install mosquitto
   
   # Start broker
   mosquitto -v
   ```

2. **Docker**
   ```bash
   docker run -it -p 1883:1883 eclipse-mosquitto
   ```

3. **EMQX** (feature-rich MQTT broker)
   ```bash
   docker run -d --name emqx -p 1883:1883 -p 8083:8083 -p 8084:8084 -p 8883:8883 -p 18083:18083 emqx/emqx
   ```

The default configuration assumes a local MQTT broker running on `localhost:1883`.
