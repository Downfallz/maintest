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
    /// Runs the work, then gives the notices it held, once it has let go of the gate, whether it finished or failed: a
    /// release that fails at its third table has told the players at the first two. A notice that cannot go is a line on
    /// <paramref name="said" />. Work begun inside another scope holds its notices in a scope of its own. At its end it hands
    /// them to the outer scope if that one is still open, or gives them itself if it has closed. A loop a route started
    /// runs on after the route answered, and its notices still wait for its own work (#1178).
    /// </summary>
    public static async Task<T> AfterAsync<T>(Func<Task<T>> work, Action<string> said)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(said);
        var outer = Current.Value;
        var held = new HeldNotices();
        Current.Value = held;
        try
        {
            return await work();
        }
        finally
        {
            Current.Value = outer;
            var notices = held.Close();
            if (outer?.HoldAll(notices) != true)
            {
                await GiveAsync(notices, said);
            }
        }
    }

    private static async Task GiveAsync(List<(AccountNotifier Notifier, AccountId To, Notice Notice)> held, Action<string> said)
    {
        foreach (var (notifier, to, notice) in held)
        {
            try
            {
                await notifier.NotifyAsync(to, notice);
            }
            catch (Exception failure)
            {
                // Given in a finally: a cancellation here would hide the work's own failure and drop the notices after it.
                said($"  Account {to}: a notice could not be given: {HttpHost.OneLine(failure)}");
            }
        }
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

    /// <summary>Takes an inner scope's notices, all of them or none: none once this scope has closed.</summary>
    private bool HoldAll(List<(AccountNotifier Notifier, AccountId To, Notice Notice)> notices)
    {
        lock (_held)
        {
            if (_closed)
            {
                return false;
            }

            _held.AddRange(notices);
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
