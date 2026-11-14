using DownfallArena.Application.Accounts;
using DownfallArena.Application.Accounts.Ports;

namespace DownfallArena.Cli.Table;

/// <summary>
/// The seats of the accounts the operator suspended (ADR 0178): kept a day for the player, in case the account is
/// restored, then offered to other players as a deleted account's are, once, and noted on the suspension.
/// </summary>
internal sealed class SuspendedSeats(IAccountStore accounts, AccountRegistry registry, LobbyApi lobby)
{
    /// <summary>The accounts whose seats this pass offered: those suspended a day or more, not yet offered.</summary>
    public async Task<IReadOnlyList<string>> ReleaseAsync(DateTimeOffset now)
    {
        var released = new List<string>();
        foreach (var account in (await accounts.ListAsync()).Where(account => account.Suspension?.SeatsDue(now) == true))
        {
            await lobby.ReleaseAsync(account.Id);
            await registry.SeatsReleasedAsync(account.Id, now);
            released.Add(account.Id.Value);
        }

        return released;
    }
}
