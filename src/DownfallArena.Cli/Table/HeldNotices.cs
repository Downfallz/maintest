using DownfallArena.Application.Accounts;
using DownfallArena.Cli.Hosting;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The notices given while the lobby's claim gate is held, kept until the work that holds it has let go of it (ADR 0203):
/// a bell's write and a device read under the gate would keep every other claim waiting on them. A lobby route, an
/// operator's change under <see cref="LobbyApi.ExclusivelyAsync" /> and a release of an account's seats each open one
/// (#1133). Outside one, a notice is given at once, as the tending job's are.
/// </summary>
internal sealed class HeldNotices
{
    private static readonly AsyncLocal<HeldNotices?> Current = new();

    private readonly List<(AccountNotifier Notifier, AccountId To, Notice Notice)> _held = [];
    private bool _closed;

    /// <summary>Gives the notice now, or keeps it for the end of the work in progress when one holds the gate.</summary>
    public static async Task NotifyAsync(AccountNotifier notifier, AccountId to, Notice notice)
    {
        if (Current.Value?.Hold(notifier, to, notice) != true)
        {
            await notifier.NotifyAsync(to, notice);
        }
    }

    /// <summary>
    /// Runs the work, then gives the notices it held, once it has answered and so let go of the gate. A notice that
    /// cannot go is a line on <paramref name="said" />: what the work did is done. Inside work that already holds
    /// notices, the outer one gives them.
    /// </summary>
    public static async Task<T> AfterAsync<T>(Func<Task<T>> work, Action<string> said)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(said);
        if (Current.Value is not null)
        {
            return await work();
        }

        var held = new HeldNotices();
        Current.Value = held;
        T answer;
        try
        {
            answer = await work();
        }
        finally
        {
            Current.Value = null;
        }

        foreach (var (notifier, to, notice) in held.Close())
        {
            try
            {
                await notifier.NotifyAsync(to, notice);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                said($"  Account {to}: a notice could not be given: {HttpHost.OneLine(failure)}");
            }
        }

        return answer;
    }

    private bool Hold(AccountNotifier notifier, AccountId to, Notice notice)
    {
        lock (_held)
        {
            if (_closed)
            {
                return false;
            }

            _held.Add((notifier, to, notice));
            return true;
        }
    }

    private List<(AccountNotifier Notifier, AccountId To, Notice Notice)> Close()
    {
        lock (_held)
        {
            _closed = true;
            return [.. _held];
        }
    }
}
