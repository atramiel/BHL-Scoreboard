using System.Net.WebSockets;
using System.Text;

namespace Scoreboard.Services;

/// <summary>
/// Connects to the public relay server as a publisher (at /source) and pushes
/// game state JSON on every update.  Browsers subscribe to the relay and receive
/// the same payload without needing to be on the local network.
/// </summary>
public class RelayPublisherService : IDisposable
{
    private readonly Uri _uri;
    private ClientWebSocket? _ws;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public RelayPublisherService(string relayUrl)
    {
        var trimmed = relayUrl.Trim().TrimEnd('/');
        // Add https:// if no scheme provided
        if (!trimmed.StartsWith("http://") && !trimmed.StartsWith("https://")
            && !trimmed.StartsWith("ws://") && !trimmed.StartsWith("wss://"))
            trimmed = "https://" + trimmed;
        var normalized = trimmed
            .Replace("https://", "wss://")
            .Replace("http://", "ws://");
        _uri = new Uri(normalized + "/source");
        _ = ConnectLoopAsync(_cts.Token);
    }

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        var delay = 2000;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                _ws = ws;
                await ws.ConnectAsync(_uri, ct);
                delay = 2000;

                var buf = new byte[64];
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    try { await ws.ReceiveAsync(buf, ct); }
                    catch { break; }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { }
            finally { _ws = null; }

            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { break; }
            delay = Math.Min(delay * 2, 30_000);
        }
    }

    public async Task SendAsync(string json)
    {
        var ws = _ws;
        if (ws?.State != WebSocketState.Open) return;
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _ws?.Dispose();
    }
}
