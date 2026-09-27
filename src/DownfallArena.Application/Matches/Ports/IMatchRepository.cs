using DownfallArena.Domain.Matches;
using DownfallArena.SharedKernel.Identifiers;

namespace DownfallArena.Application.Matches.Ports;

/// <summary>
/// Where matches live between two commands. Owned by Application, implemented by Infrastructure.
/// </summary>
public interface IMatchRepository
{
    Task<Match?> FindAsync(MatchId id, CancellationToken cancellationToken = default);

    Task SaveAsync(Match match, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets go of a match the host is done with. A process that plays one match keeps it for its lifetime;
    /// a host that plays many for days would otherwise keep every one of them (ADR 0081). Forgetting a match
    /// nobody saved is nothing.
    /// </summary>
    Task ForgetAsync(MatchId id, CancellationToken cancellationToken = default);
}
