using DA.Game.Domain2.Matches.Aggregates;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.ValueObjects;

namespace DA.Game.Domain2.Matches.Services.Combat;

public interface IEffectComputationService
{
    RawEffectBundle ComputeRawEffects(CombatActionChoice intent);
}
