using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Specifications.Creatures;

public sealed class IsDeadSpec : ISpecification<CombatCharacter>
{
    public bool IsSatisfiedBy(CombatCharacter candidate)
        => !candidate.IsAlive;
}
