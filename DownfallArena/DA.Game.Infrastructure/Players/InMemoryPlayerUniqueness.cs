using DA.Game.Application.Players.Ports;

namespace DA.Game.Infrastructure.Players;

public sealed class InMemoryPlayerUniqueness(IPlayerRepository playerRepository) : IPlayerUniqueness
{
    public async Task<bool> ExistsNameAsync(string name, CancellationToken ct = default)
    {
        var players = await playerRepository.GetAllAsync(ct);
        return players.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
