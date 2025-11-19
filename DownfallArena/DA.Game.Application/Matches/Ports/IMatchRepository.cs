using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Application.Matches.Ports;

public interface IMatchRepository
{
    Task<Match?> GetAsync(MatchId id, CancellationToken ct = default);
    Task<Result<Match>> SaveAsync(Match match, CancellationToken ct = default);
}
