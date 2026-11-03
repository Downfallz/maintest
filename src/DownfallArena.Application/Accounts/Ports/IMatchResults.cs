namespace DownfallArena.Application.Accounts.Ports;

/// <summary>
/// Where finished matches with an account in them are kept, one record each (ADR 0122, stage 5), beside the
/// accounts. Recording the same match twice keeps one record. Owned by Application, implemented by
/// Infrastructure.
/// </summary>
public interface IMatchResults
{
    Task RecordAsync(MatchResult result, CancellationToken cancellationToken = default);

    /// <summary>Every result kept, oldest first.</summary>
    Task<IReadOnlyList<MatchResult>> ListAsync(CancellationToken cancellationToken = default);
}
