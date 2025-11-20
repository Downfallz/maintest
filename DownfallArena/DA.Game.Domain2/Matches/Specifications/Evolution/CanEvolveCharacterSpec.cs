using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Specifications.Evolution;

public sealed class CanEvolveCharacterSpec : ISpecification<EvolutionContext>
{
    public bool IsSatisfiedBy(EvolutionContext c)
        => true;
}
