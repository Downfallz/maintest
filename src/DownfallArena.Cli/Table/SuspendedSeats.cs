using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The seats of the accounts the operator suspended (ADR 0178): kept a day for the player, in case the account is
/// restored, then offered to other players as a deleted account's are. The pass writes no account: it runs in a
/// process of its own (ADR 0162), and the operator's restores, renames and deletions are the API's to write. Offering
/// is idempotent instead, since a seat offered is no longer the account's, so a later pass finds nothing to offer.
/// </summary>
internal sealed class SuspendedSeats(IAccountStore accounts, LobbyApi lobby)
{
    /// <summary>The accounts whose seats this pass offered: those suspended a day or more that still held a seat.</summary>
    public async Task<IReadOnlyList<string>> ReleaseAsync(DateTimeOffset now)
    {
        var released = new List<string>();
        foreach (var listed in (await accounts.ListAsync()).Where(account => account.Suspension?.SeatsDue(now) == true))
        {
            // Read again just before the seats are offered: an account the operator restored since the listing keeps
            // them, and one suspended again since is kept its own day.
            if (await accounts.FindAsync(listed.Id) is not { Suspension: { } current } || current.At != listed.Suspension!.At || !current.SeatsDue(now))
            {
                continue;
            }

            if (await lobby.ReleaseAsync(listed.Id) > 0)
            {
                released.Add(listed.Id.Value);
            }
        }

        return released;
    }
}
