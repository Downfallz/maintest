using DA.Game.Application.Players.Ports;
using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Infrastructure.Matches;

public sealed class InMemoryPlayerUniqueness(IPlayerRepository playerRepository) : IPlayerUniqueness
{
    public async Task<bool> ExistsNameAsync(string name, CancellationToken ct = default)
    {
        var players = await playerRepository.GetAllAsync(ct);
        return players.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
