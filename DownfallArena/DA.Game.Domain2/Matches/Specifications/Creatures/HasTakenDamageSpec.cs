using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Specifications.Creatures;

public sealed class HasTakenDamageSpec : ISpecification<CombatCharacter>
{
    public bool IsSatisfiedBy(CombatCharacter candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Health < candidate.BaseHealth;
    }
}
