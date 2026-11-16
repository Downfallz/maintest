namespace DownfallArena.Cli.Table;

/// <summary>
/// Sends one message a key at a time, and the ones asked for while it is on its way as one more (ADR 0165): a bot's
/// turn moves its table several times in a breath, and a page told twice reads once anyway, so the burst costs the
/// relay's daily allowance two messages, not ten. A send that fails is a line on the console and a message that did
/// not go; the next move of the same key sends again.
/// </summary>
internal sealed class CoalescedSends(Func<string, Task> send)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, bool> _sending = new(StringComparer.Ordinal);

    /// <summary>What is still on its way, for a test or a host stopping to wait on.</summary>
    public Task Idle { get; private set; } = Task.CompletedTask;

    public void Post(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (_sending.ContainsKey(key))
            {
                _sending[key] = true;
                return;
            }

            _sending[key] = false;
            var running = RunAsync(key);
            Idle = Task.WhenAll(Idle, running);
        }
    }

    private async Task RunAsync(string key)
    {
        await Task.Yield();
        while (true)
        {
            try
            {
                await send(key);
            }
            catch (Exception failure) when (failure is Azure.RequestFailedException or HttpRequestException or IOException or TaskCanceledException or Azure.Identity.AuthenticationFailedException)
            {
                Console.WriteLine($"  Relay: '{key}' could not be sent: {failure.Message}");
            }

            lock (_gate)
            {
                if (!_sending[key])
                {
                    _sending.Remove(key);
                    return;
                }

                _sending[key] = false;
            }
        }
    }
}
