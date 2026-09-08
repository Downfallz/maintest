using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Services.Combat.Resolution;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Matches.ValueObjects.Combat;

namespace DA.Game.Domain2.Matches.Services.Combat;

public interface ICritComputationService
{
    CritComputationResult ApplyCrit(CreaturePerspective ctx, CombatActionChoice choice);
}
