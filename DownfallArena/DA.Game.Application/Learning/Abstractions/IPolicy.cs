using DA.Game.Domain2.Match.ReadModels;
using DA.Game.Domain2.Match.ValueObjects;

namespace DA.Game.Application.Learning.Abstractions;

// Abstractions/IPolicy.cs
public interface IPolicy
{
    Task<PlayerAction> DecideAsync(GameView view, CancellationToken ct = default);
}
