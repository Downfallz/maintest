using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;
using DownfallArena.Cli.Hosting;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The guests no browser can reach any more (ADR 0207), removed by the hourly job with what was kept for them: the
/// account, its identity's binding and its name, its game settings and tutorials, its activity. A guest's session
/// lives <see cref="AccountSessions.Lifetime" /> from its last renewal, which follows its last use within the hour the
/// last-seen time lags by (<see cref="AccountActivityLog.SeenEvery" />); a guest unused for <see cref="KeptFor" /> holds no
/// session a browser still keeps, so no request of the API process can be writing it while this one removes it.
/// A guest holds no seat, friend, device, notice or feedback: the routes that make them refuse it.
/// </summary>
internal sealed class IdleGuests(IAccountStore accounts, IAccountActivityStore activity, IPreferenceStore? preferences, TimeProvider clock)
{
    /// <summary>How long after its last use a guest is removed: its session's lifetime, and a day.</summary>
    public static readonly TimeSpan KeptFor = AccountSessions.Lifetime + TimeSpan.FromDays(1);

    /// <summary>Where a guest that could not be removed is said: the console unless a test reads it.</summary>
    public Action<string> Said { get; init; } = Console.WriteLine;

    /// <summary>Removes every guest last used <see cref="KeptFor" /> or more before <paramref name="now" />, and says how many went.</summary>
    public async Task<int> RemoveAsync(DateTimeOffset now)
    {
        var guests = (await accounts.ListAsync()).Where(account => account.IsGuest).ToList();
        if (guests.Count == 0)
        {
            return 0;
        }

        var used = (await activity.ListAsync()).ToDictionary(one => one.Account);
        var registry = new AccountRegistry(accounts, clock);
        var removed = 0;
        foreach (var listed in guests.Where(guest => Idle(guest, used.GetValueOrDefault(guest.Id), now)))
        {
            try
            {
                // Read again just before it goes: a guest upgraded since the listing is a player's account, and one used
                // since is kept.
                var kept = await activity.FindAsync(listed.Id);
                if (await accounts.FindAsync(listed.Id) is not { IsGuest: true } guest || !Idle(guest, kept, now))
                {
                    continue;
                }

                // What is kept for it goes first and the account last: a pass that stops between them finds the guest
                // again, its activity gone, and its making time still says it is idle.
                if (preferences is not null)
                {
                    await preferences.ForgetAsync(guest.Id);
                }

                if (kept is not null)
                {
                    await activity.ForgetAsync(guest.Id);
                }

                if (await registry.DeleteAsync(guest.Id))
                {
                    removed++;
                }
            }
            catch (Exception failure) when (TableRegistry.StoreFails(failure))
            {
                Said($"  Account {listed.Id}: a guest unused for {KeptFor.TotalDays:0} days could not be removed: {HttpHost.OneLine(failure)}");
            }
        }

        return removed;
    }

    // Last used: its last sign-in or request, or its making when neither was kept.
    private static bool Idle(Account guest, AccountActivity? kept, DateTimeOffset now)
    {
        var last = guest.CreatedAt;
        if (kept?.LastSignIn is { } signedIn && signedIn > last)
        {
            last = signedIn;
        }

        if (kept?.LastSeen is { } seen && seen > last)
        {
            last = seen;
        }

        return now - last >= KeptFor;
    }
}
