using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace DownfallArena.Cli.Table;

/// <summary>
/// What a process hears instead of looking at its tables every second (ADR 0165): the relay's hosts group, where every
/// process says which table it moved. A table another process moved is looked at once; each connection that opens
/// is a look at every table held, for what moved while it was down; and a connection that drops is opened again, a
/// little later each time. Nothing is read on a clock. A look that could not be made -- the store out for a moment --
/// drops the connection, so that its opening looks at everything again once the store is back.
/// </summary>
/// <param name="opened">The look at every table held, as each connection opens; it fails when a look could not be made.</param>
/// <param name="moved">The look at one table another process moved; it fails when it could not be made.</param>
internal sealed class RelayListener(IChangeRelay relay, Func<CancellationToken, Task> opened, Func<string, CancellationToken, Task> moved, TimeProvider clock)
{
    public RelayListener(IChangeRelay relay, TableWatch watch, TimeProvider clock)
        : this(relay, watch.ReconcileAsync, watch.MovedAsync, clock)
    {
    }

    /// <summary>The messages a connection to <paramref name="address" /> receives, until it closes. A socket by default.</summary>
    public Func<Uri, CancellationToken, IAsyncEnumerable<string>> Receive { get; init; } = ReceiveAsync;

    /// <summary>Hears until stopped.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pause = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (await HearAsync(await relay.HostsAsync(cancellationToken), cancellationToken))
                {
                    pause = TimeSpan.FromSeconds(1);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception failure) when (failure is WebSocketException or Azure.RequestFailedException or HttpRequestException or IOException or Azure.Identity.AuthenticationFailedException)
            {
                Console.WriteLine($"  Relay: the hosts' connection dropped, opened again in {pause.TotalSeconds:0} s: {failure.Message}");
            }

            try
            {
                await Task.Delay(pause, clock, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            pause = TimeSpan.FromSeconds(Math.Min(30, pause.TotalSeconds * 2));
        }
    }

    // One connection, until it closes: whether it opened and its look at everything was made.
    private async Task<bool> HearAsync(Uri address, CancellationToken cancellationToken)
    {
        var open = false;
        await foreach (var message in Receive(address, cancellationToken))
        {
            if (!open)
            {
                // Open: every table held is looked at once, for what moved while no connection was.
                await opened(cancellationToken);
                open = true;
            }
            else if (WebPubSubRelay.Moved(message) is { } said && said.Origin != relay.Origin)
            {
                await moved(said.Table, cancellationToken);
            }
        }

        return open;
    }

    // The connection opening is said as an empty message: it is the look at every table held, for what moved while
    // no connection was open.
    private static async IAsyncEnumerable<string> ReceiveAsync(Uri address, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(address, cancellationToken);
        yield return string.Empty;
        var buffer = new byte[4096];
        using var text = new MemoryStream();
        while (socket.State == WebSocketState.Open)
        {
            var received = await socket.ReceiveAsync(buffer, cancellationToken);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            await text.WriteAsync(buffer.AsMemory(0, received.Count), cancellationToken);
            if (received.EndOfMessage)
            {
                yield return Encoding.UTF8.GetString(text.GetBuffer(), 0, (int)text.Length);
                text.SetLength(0);
            }
        }
    }
}
