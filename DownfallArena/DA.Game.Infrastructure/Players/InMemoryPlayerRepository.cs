using DA.Game.Application.Players.Ports;
using DA.Game.Domain2.Players.Entities;
using DA.Game.Shared.Contracts.Players.Ids;

namespace DA.Game.Infrastructure.Players;

public sealed class InMemoryPlayerRepository : IPlayerRepository
{
    private readonly Dictionary<PlayerId, Player> _store = new();

    public Task<ICollection<Player>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult((ICollection<Player>)_store.Values.ToList());
    }

    public Task<Player?> GetAsync(PlayerId id, CancellationToken ct = default)
    {
        return Task.FromResult(_store.TryGetValue(id, out var m) ? m : null);
    }

    public Task SaveAsync(Player player, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        _store[player.Id] = player;
        return Task.CompletedTask;
    }
}
