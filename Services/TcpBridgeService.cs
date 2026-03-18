using Scoreboard.Models;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Scoreboard.Services;

/// <summary>
/// TCP server on localhost:45678 that the Stream Deck plugin connects to.
/// Receives action commands from the plugin and broadcasts game state updates.
/// Protocol: newline-delimited JSON.
/// </summary>
public class TcpBridgeService : IDisposable
{
    private readonly TcpListener _listener;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private NetworkStream? _stream;
    private bool _disposed;

    public event EventHandler<GameAction>? CommandReceived;
    public event EventHandler? ClientConnected;

    public TcpBridgeService(int port = 52800)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            _listener.Start();
            _ = AcceptLoopAsync(_cts.Token);
        }
        catch
        {
            // Port already in use — bridge unavailable but app still runs
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                client.NoDelay = true;
                _stream = client.GetStream();
                ClientConnected?.Invoke(this, EventArgs.Empty);
                await ReadLoopAsync(_stream, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* client disconnected — wait for the next one */ }
        }
    }

    private async Task ReadLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break; // client disconnected

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("action", out var actionEl)
                    && Enum.TryParse<GameAction>(actionEl.GetString(), out var action))
                {
                    CommandReceived?.Invoke(this, action);
                }
            }
            catch { /* ignore malformed messages */ }
        }
    }

    public async Task SendStateAsync(
        string homeTeam, string awayTeam,
        int homeScore, int awayScore,
        string clock, bool isRunning, bool gameDone,
        string nextMatchTime = "--:--")
    {
        if (_stream == null) return;

        var payload = JsonSerializer.Serialize(new
        {
            homeTeam,
            awayTeam,
            homeScore,
            awayScore,
            clock,
            isRunning,
            gameDone,
            nextMatchTime
        }) + "\n";

        var bytes = Encoding.UTF8.GetBytes(payload);

        await _sendLock.WaitAsync();
        try { await _stream.WriteAsync(bytes); }
        catch { _stream = null; }
        finally { _sendLock.Release(); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _stream?.Dispose();
        _listener.Stop();
    }
}
