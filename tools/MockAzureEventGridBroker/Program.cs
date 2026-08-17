namespace CrowsNestMqtt.MockAzureEventGridBroker;

/// <summary>
/// Development-only MQTT broker that emulates Azure Event Grid namespace enhanced
/// authentication (<c>OAUTH2-JWT</c>). Accepts CONNECT packets whose
/// AuthenticationMethod is <c>OAUTH2-JWT</c> and AuthenticationData contains a
/// well-formed JWT. Signature and audience are NOT verified. Do not use in
/// production or expose beyond localhost.
///
/// Configuration via env vars:
///  <list type="bullet">
///   <item><c>MOCK_EG_PORT</c> — TCP/TLS port to listen on (default: dynamic).</item>
///   <item><c>MOCK_EG_HOST</c> — host to bind (default: 127.0.0.1).</item>
///   <item><c>MOCK_EG_USE_TLS</c> — <c>true</c> to enable TLS with a self-signed cert (default: true).</item>
///  </list>
/// The chosen port is printed to stdout as <c>MOCK_EG_LISTENING_ON=&lt;host&gt;:&lt;port&gt;</c>.
/// The heavy lifting lives in <see cref="MockBroker"/> — <see cref="Program"/> is
/// a thin process-lifetime wrapper so both the CLI entry point and the in-process
/// tests share the same implementation.
/// </summary>
internal static class Program
{
    internal static async Task Main()
    {
        var options = MockBrokerOptions.FromEnvironment();
        var broker = new MockBroker(options);
        await using var brokerHandle = broker.ConfigureAwait(false);

        await broker.StartAsync().ConfigureAwait(false);

        var stopTcs = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stopTcs.TrySetResult();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => stopTcs.TrySetResult();

        await stopTcs.Task.ConfigureAwait(false);
        await broker.StopAsync().ConfigureAwait(false);
    }
}
