using DA.Game.Domain2.Match.Entities;
using DA.Game.Domain2.Players.Ids;

namespace DA.Game.Application.Players.Ports;
public interface IPlayerRepository
{
    Task<ICollection<Player>> GetAllAsync(CancellationToken ct = default);
    Task<Player?> GetAsync(PlayerId id, CancellationToken ct = default);
    Task SaveAsync(Player player, CancellationToken ct = default);
}