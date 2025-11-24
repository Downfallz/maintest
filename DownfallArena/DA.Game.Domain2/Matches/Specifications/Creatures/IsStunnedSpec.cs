using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Specifications.Creatures;

public sealed class IsStunnedSpec : ISpecification<CombatCreature>
{
    public bool IsSatisfiedBy(CombatCreature candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.IsStunned;
    }
}
