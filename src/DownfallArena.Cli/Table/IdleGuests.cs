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

    /// <summary>
    /// Removes every guest last used <see cref="KeptFor" /> or more before <paramref name="now" />, and says how many went;
    /// then forgets the settings, Tutorials and activity left by an account that is gone.
    /// </summary>
    public async Task<int> RemoveAsync(DateTimeOffset now)
    {
        List<Account> guests;
        HashSet<SharedKernel.Identifiers.AccountId> listed;
        Dictionary<SharedKernel.Identifiers.AccountId, AccountActivity> used;
        try
        {
            var all = await accounts.ListAsync();
            guests = [.. all.Where(account => account.IsGuest)];
            listed = [.. all.Select(account => account.Id)];
            used = (await activity.ListAsync()).ToDictionary(one => one.Account);
        }
        catch (Exception failure) when (Unreadable(failure))
        {
            // The pass goes on without this stage: the next one removes them.
            Said($"  Guests: the accounts could not be listed, so none was removed: {HttpHost.OneLine(failure)}");
            return 0;
        }

        var registry = new AccountRegistry(accounts, clock);
        var removed = 0;
        foreach (var idle in guests.Where(guest => Idle(guest, used.GetValueOrDefault(guest.Id), now)))
        {
            try
            {
                // Read again just before it goes: a guest used since is kept.
                var kept = await activity.FindAsync(idle.Id);
                if (await accounts.FindAsync(idle.Id) is not { IsGuest: true } guest || !Idle(guest, kept, now))
                {
                    continue;
                }

                // The account goes first, and only while it is still a guest: the API may upgrade it meanwhile, under no
                // lock this process holds, and an upgraded guest keeps its tutorials and activity (#1253). What is kept for
                // it goes after; a pass that stops between them leaves it to the next pass, which finds it below.
                if (!await registry.DeleteAsync(guest.Id, account => account.IsGuest))
                {
                    continue;
                }

                removed++;
            }
            catch (Exception failure) when (Unreadable(failure))
            {
                Said($"  Account {idle.Id}: a guest unused for {KeptFor.TotalDays:0} days could not be removed: {HttpHost.OneLine(failure)}");
                continue;
            }

            try
            {
                await ForgetAsync(idle.Id);
            }
            catch (Exception failure) when (Unreadable(failure))
            {
                Said($"  Account {idle.Id}: a removed guest's settings or activity could not be forgotten, the next pass will: {HttpHost.OneLine(failure)}");
            }
        }

        // What a removed account left behind, by a pass that stopped between the account and the rest. Listed after the
        // removals, so a listing that fails costs only this sweep, never them. Read again: an account made since the
        // listing is not gone.
        IReadOnlyList<SharedKernel.Identifiers.AccountId> chose;
        try
        {
            chose = preferences is null ? [] : await preferences.ListAccountsAsync();
        }
        catch (Exception failure) when (Unreadable(failure))
        {
            Said($"  Guests: the settings could not be listed, so only the activity left by removed accounts is forgotten: {HttpHost.OneLine(failure)}");
            chose = [];
        }

        foreach (var left in chose.Concat(used.Keys).Where(id => !listed.Contains(id)).Distinct())
        {
            try
            {
                if (await accounts.FindAsync(left) is null)
                {
                    await ForgetAsync(left);
                }
            }
            catch (Exception failure) when (Unreadable(failure))
            {
                Said($"  Account {left}: what a removed account left behind could not be forgotten: {HttpHost.OneLine(failure)}");
            }
        }

        return removed;
    }

    // Its settings and Tutorials first and its activity last, so a forget that stops partway leaves the activity the
    // next pass finds it by, when the listing of the settings cannot.
    private async Task ForgetAsync(SharedKernel.Identifiers.AccountId account)
    {
        if (preferences is not null)
        {
            await preferences.ForgetAsync(account);
        }

        await activity.ForgetAsync(account);
    }

    // A store out of service, or a document that does not read: what stops this stage and never the pass.
    private static bool Unreadable(Exception failure) =>
        TableRegistry.StoreFails(failure) || failure is System.Text.Json.JsonException or InvalidDataException or FormatException or ArgumentException;

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
