using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet.AspNetCore;
using MQTTnet.Server;
using Xunit;

namespace CrowsNestMqtt.Integration.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Referenced via IClassFixture<WebSocketProxyBrokerFixture> from public test classes; visibility must match test-class visibility.")]
public sealed class WebSocketProxyBrokerFixture : IAsyncLifetime
{
    private readonly CancellationTokenSource _proxyCancellation = new();
    private WebApplication? _brokerApplication;
    private TcpListener? _proxyListener;
    private Task? _proxyAcceptLoop;
    private int _proxyConnectionCount;

    public static string Hostname => IPAddress.Loopback.ToString();
    public int WebSocketPort { get; private set; }
    public int ProxyPort { get; private set; }
    public int ProxyConnectionCount => Volatile.Read(ref _proxyConnectionCount);
    public string? LastProxyRequestLine { get; private set; }

    public async ValueTask InitializeAsync()
    {
        WebSocketPort = GetRandomPort();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, WebSocketPort));
        builder.Services
            .AddHostedMqttServer(options => options.WithoutDefaultEndpoint())
            .AddMqttConnectionHandler()
            .AddConnections();

        _brokerApplication = builder.Build();
        _brokerApplication.MapConnectionHandler<MqttConnectionHandler>(
            "/mqtt",
            options => options.WebSockets.SubProtocolSelector =
                protocols => protocols.FirstOrDefault() ?? string.Empty);
        _brokerApplication.UseMqttServer(_ => { });
        await _brokerApplication.StartAsync().ConfigureAwait(false);

        _proxyListener = new TcpListener(IPAddress.Loopback, 0);
        _proxyListener.Start();
        ProxyPort = ((IPEndPoint)_proxyListener.LocalEndpoint).Port;
        _proxyAcceptLoop = AcceptProxyConnectionsAsync(_proxyCancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _proxyCancellation.CancelAsync().ConfigureAwait(false);
        _proxyListener?.Stop();
        _proxyListener?.Dispose();

        if (_proxyAcceptLoop != null)
        {
            try
            {
                await _proxyAcceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_brokerApplication != null)
        {
            await _brokerApplication.StopAsync().ConfigureAwait(false);
            await _brokerApplication.DisposeAsync().ConfigureAwait(false);
        }

        _proxyCancellation.Dispose();
    }

    private async Task AcceptProxyConnectionsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _proxyListener!.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(() => HandleProxyConnectionAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    private async Task HandleProxyConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var clientStream = client.GetStream();
            var requestBytes = await ReadHttpHeadersAsync(clientStream, cancellationToken).ConfigureAwait(false);
            var requestText = Encoding.ASCII.GetString(requestBytes);
            var firstLineEnd = requestText.IndexOf("\r\n", StringComparison.Ordinal);
            if (firstLineEnd < 0)
            {
                return;
            }

            var firstLine = requestText[..firstLineEnd];
            LastProxyRequestLine = firstLine;
            Interlocked.Increment(ref _proxyConnectionCount);

            var parts = firstLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return;
            }

            using var upstream = new TcpClient();
            if (parts[0].Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                var destination = parts[1].Split(':', 2);
                if (destination.Length != 2 || !int.TryParse(destination[1], out var port))
                {
                    return;
                }

                await upstream.ConnectAsync(destination[0], port, cancellationToken).ConfigureAwait(false);
                await clientStream.WriteAsync(
                    Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (!Uri.TryCreate(parts[1], UriKind.Absolute, out var destination))
                {
                    return;
                }

                var port = destination.IsDefaultPort
                    ? destination.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80
                    : destination.Port;
                await upstream.ConnectAsync(destination.Host, port, cancellationToken).ConfigureAwait(false);

                var rewrittenRequest = Encoding.ASCII.GetBytes(
                    $"{parts[0]} {destination.PathAndQuery} {parts[2]}{requestText[firstLineEnd..]}");
                await upstream.GetStream().WriteAsync(rewrittenRequest, cancellationToken).ConfigureAwait(false);
            }

            var upstreamStream = upstream.GetStream();
            await Task.WhenAll(
                clientStream.CopyToAsync(upstreamStream, cancellationToken),
                upstreamStream.CopyToAsync(clientStream, cancellationToken)).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadHttpHeadersAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];

        while (buffer.Length < 64 * 1024)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (buffer.Length >= 4)
            {
                var bytes = buffer.GetBuffer();
                var length = (int)buffer.Length;
                if (bytes[length - 4] == '\r'
                    && bytes[length - 3] == '\n'
                    && bytes[length - 2] == '\r'
                    && bytes[length - 1] == '\n')
                {
                    return buffer.ToArray();
                }
            }
        }

        throw new InvalidDataException("Proxy request did not contain a complete HTTP header block.");
    }

    private static int GetRandomPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
