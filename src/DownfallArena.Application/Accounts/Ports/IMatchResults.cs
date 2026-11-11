using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where finished matches with an account in them are kept, one record each (ADR 0122, stage 5): in Table Storage
/// by match and by account on Azure (ADR 0168), as files on a laptop. Recording the same match twice keeps one
/// record. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IMatchResults
{
    Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default);

    /// <summary>Every result kept, oldest first.</summary>
    Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The results of the matches an account held a seat in, oldest first: what a player's own pages read. A store
    /// that keeps them by account reads them alone (ADR 0168); any other picks them out of every result.
    /// </summary>
    async Task<IReadOnlyList<MatchResult>> ListForAsync(AccountId account, CancellationToken cancellationToken = default) =>
        [.. (await ListAsync(cancellationToken)).Where(result => result.Seats.Any(seat => seat.Account == account))];
}
