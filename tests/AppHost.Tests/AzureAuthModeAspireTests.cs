namespace CrowsNestMqtt.AppHostTests;

using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MQTTnet;
using Xunit;

/// <summary>
/// End-to-end tests that verify Azure Event Grid namespace connectivity by
/// launching the bundled <c>MockAzureEventGridBroker</c> as a child process and
/// connecting a real MQTTnet v5 client with <c>OAUTH2-JWT</c> enhanced auth.
///
/// The broker is the same one wired into the production AppHost as the fourth
/// Crow's Nest instance, so a green run here is strong evidence that the Aspire
/// configuration will succeed too.
///
/// The tests intentionally do NOT depend on Docker (EMQX) or a display server —
/// they only need the mock broker to be buildable and a free TCP port.
/// </summary>
public sealed class AzureAuthModeAspireTests : IAsyncLifetime
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

    private Process? _brokerProcess;
    private string _brokerHost = "127.0.0.1";
    private int _brokerPort;
    private readonly List<string> _brokerStdout = new();
    private readonly List<string> _brokerStderr = new();

    public async ValueTask InitializeAsync()
    {
        _brokerPort = GetFreeTcpPort();
        _brokerProcess = StartMockBroker(_brokerHost, _brokerPort);

        // Wait until the broker accepts TCP connections (i.e. is listening).
        using var startCts = new CancellationTokenSource(StartTimeout);
        var ct = startCts.Token;
        Exception? lastError = null;
        while (!ct.IsCancellationRequested)
        {
            if (_brokerProcess.HasExited)
            {
                throw new InvalidOperationException(
                    $"Mock broker exited unexpectedly with code {_brokerProcess.ExitCode} before binding to {_brokerHost}:{_brokerPort}.\n"
                    + $"stdout:\n{string.Join('\n', _brokerStdout)}\n"
                    + $"stderr:\n{string.Join('\n', _brokerStderr)}");
            }

            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(_brokerHost, _brokerPort);
                var winner = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromMilliseconds(300), ct));
                if (winner == connectTask && tcp.Connected)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
            await Task.Delay(200, ct);
        }

        throw new TimeoutException(
            $"Mock broker did not accept connections on {_brokerHost}:{_brokerPort} within {StartTimeout}. Last error: {lastError?.Message ?? "(none)"}\n"
            + $"stdout:\n{string.Join('\n', _brokerStdout)}\n"
            + $"stderr:\n{string.Join('\n', _brokerStderr)}");
    }

    public async ValueTask DisposeAsync()
    {
        if (_brokerProcess is null)
        {
            return;
        }

        try
        {
            if (!_brokerProcess.HasExited)
            {
                _brokerProcess.Kill(entireProcessTree: true);
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _brokerProcess.WaitForExitAsync(timeoutCts.Token);
            }
        }
        catch
        {
            // best-effort cleanup
        }
        finally
        {
            _brokerProcess.Dispose();
            _brokerProcess = null;
        }
    }

    [Fact]
    public async Task MockBroker_AcceptsOauth2JwtConnect_And_LogsAcceptance()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var ct = cts.Token;

        var jwt = BuildDevJwt();
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithTcpServer(_brokerHost, _brokerPort)
            .WithTlsOptions(o =>
            {
                o.UseTls();
                o.WithAllowUntrustedCertificates(true);
                o.WithIgnoreCertificateChainErrors(true);
                o.WithCertificateValidationHandler(_ => true);
            })
            .WithClientId("aspire-test-client")
            .WithEnhancedAuthentication("OAUTH2-JWT", Encoding.UTF8.GetBytes(jwt))
            .Build();

        var connectResult = await client.ConnectAsync(options, ct);
        Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);

        // Assert on the broker's captured stdout so we know the reason for
        // acceptance was actually the OAUTH2-JWT check (not a fallback).
        var accepted = await WaitForStdoutAsync(
            log => log.Contains("Accepted CONNECT (method=OAUTH2-JWT", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            ct);
        Assert.True(
            accepted,
            "Mock broker did not log OAUTH2-JWT acceptance. Captured stdout:\n"
                + string.Join('\n', _brokerStdout));

        await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
    }

    [Fact]
    public async Task MockBroker_RejectsAnonymousConnect_WithBadAuthMethod()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var ct = cts.Token;

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithTcpServer(_brokerHost, _brokerPort)
            .WithTlsOptions(o =>
            {
                o.UseTls();
                o.WithAllowUntrustedCertificates(true);
                o.WithIgnoreCertificateChainErrors(true);
                o.WithCertificateValidationHandler(_ => true);
            })
            .WithClientId("aspire-test-client-anon")
            .Build(); // no enhanced auth

        // The mock broker must reject CONNECT without OAUTH2-JWT. Different
        // MQTTnet builds surface this as either a broker-side reason code in the
        // CONNACK or a raw connection failure, so accept both shapes.
        try
        {
            var result = await client.ConnectAsync(options, ct);
            Assert.NotEqual(MqttClientConnectResultCode.Success, result.ResultCode);
        }
        catch (Exception ex) when (ex is MQTTnet.Adapter.MqttConnectingFailedException or MQTTnet.Exceptions.MqttCommunicationException)
        {
            // Expected: broker rejected the connection.
        }

        var rejected = await WaitForStdoutAsync(
            log => log.Contains("Mock broker rejects CONNECT: expected AuthenticationMethod 'OAUTH2-JWT'", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            ct);
        Assert.True(
            rejected,
            "Mock broker did not log the expected rejection reason. Captured stdout:\n"
                + string.Join('\n', _brokerStdout));
    }

    [Fact]
    public async Task MockBroker_AcceptsClientInitiatedReauth()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var ct = cts.Token;

        var jwt = BuildDevJwt();
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
            .WithTcpServer(_brokerHost, _brokerPort)
            .WithTlsOptions(o =>
            {
                o.UseTls();
                o.WithAllowUntrustedCertificates(true);
                o.WithIgnoreCertificateChainErrors(true);
                o.WithCertificateValidationHandler(_ => true);
            })
            .WithClientId("aspire-test-client-reauth")
            .WithEnhancedAuthentication("OAUTH2-JWT", Encoding.UTF8.GetBytes(jwt))
            .Build();

        var connectResult = await client.ConnectAsync(options, ct);
        Assert.Equal(MqttClientConnectResultCode.Success, connectResult.ResultCode);

        // Send a client-initiated AUTH packet with a fresh token — this is the
        // same wire flow MqttEngine's proactive token refresh timer triggers.
        var refreshedJwt = BuildDevJwt();
        await client.SendEnhancedAuthenticationExchangeDataAsync(
            new MqttEnhancedAuthenticationExchangeData
            {
                AuthenticationData = Encoding.UTF8.GetBytes(refreshedJwt),
                ReasonCode = MQTTnet.Protocol.MqttAuthenticateReasonCode.ReAuthenticate,
            },
            ct);

        var reauthLogged = await WaitForStdoutAsync(
            log => log.Contains("Received AUTH (method=OAUTH2-JWT", StringComparison.Ordinal)
                && log.Contains("reason=ReAuthenticate", StringComparison.Ordinal),
            TimeSpan.FromSeconds(10),
            ct);
        Assert.True(
            reauthLogged,
            "Mock broker did not log the client-initiated AUTH re-authentication. Captured stdout:\n"
                + string.Join('\n', _brokerStdout));

        await client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build(), ct);
    }

    private Process StartMockBroker(string host, int port)
    {
        var dllPath = LocateMockBrokerDll();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(dllPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(dllPath);
        psi.EnvironmentVariables["MOCK_EG_HOST"] = host;
        psi.EnvironmentVariables["MOCK_EG_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        psi.EnvironmentVariables["MOCK_EG_USE_TLS"] = "true";
        // Ensure ASP.NET Core / dotnet host uses server GC etc. — not strictly
        // required, but keeps output stable across environments.
        psi.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development";

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                lock (_brokerStdout)
                {
                    _brokerStdout.Add(args.Data);
                }
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                lock (_brokerStderr)
                {
                    _brokerStderr.Add(args.Data);
                }
            }
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Failed to start mock broker process.");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static string LocateMockBrokerDll()
    {
        // Walk up from the test's output directory to the repo root, then into
        // tools/MockAzureEventGridBroker/bin/{Configuration}/net10.0/.
        var testDir = AppContext.BaseDirectory;
        var configName = Path.GetFileName(Path.GetDirectoryName(testDir.TrimEnd(Path.DirectorySeparatorChar))!);
        // testDir looks like ...\tests\AppHost.Tests\bin\Release\net10.0\
        var directory = new DirectoryInfo(testDir);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrowsNestMQTT.slnx")))
        {
            directory = directory.Parent;
        }
        if (directory is null)
        {
            throw new InvalidOperationException($"Could not locate repository root from {testDir}.");
        }

        var candidates = new[]
        {
            Path.Combine(directory.FullName, "tools", "MockAzureEventGridBroker", "bin", "Release", "net10.0", "CrowsNestMqtt.MockAzureEventGridBroker.dll"),
            Path.Combine(directory.FullName, "tools", "MockAzureEventGridBroker", "bin", "Debug", "net10.0", "CrowsNestMqtt.MockAzureEventGridBroker.dll"),
        };
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new FileNotFoundException(
            $"Mock broker DLL not found (config={configName}). Build the solution first. Searched:\n"
                + string.Join('\n', candidates));
    }

    private async Task<bool> WaitForStdoutAsync(Func<string, bool> predicate, TimeSpan timeout, CancellationToken outerToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        int lastCount = 0;
        while (DateTime.UtcNow < deadline)
        {
            outerToken.ThrowIfCancellationRequested();
            string[] snapshot;
            lock (_brokerStdout)
            {
                snapshot = _brokerStdout.Skip(lastCount).ToArray();
                lastCount = _brokerStdout.Count;
            }
            foreach (var line in snapshot)
            {
                if (predicate(line))
                {
                    return true;
                }
            }
            await Task.Delay(100, outerToken);
        }
        return false;
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string BuildDevJwt()
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes("test-key-that-is-long-enough-for-hmac-signing");
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim("sub", "aspire-test-user") }),
            Issuer = "https://sts.local",
            Audience = "https://eventgrid.azure.net",
            Expires = DateTime.UtcNow.AddHours(1),
            NotBefore = DateTime.UtcNow.AddMinutes(-5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature),
        };
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
