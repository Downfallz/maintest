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
}
