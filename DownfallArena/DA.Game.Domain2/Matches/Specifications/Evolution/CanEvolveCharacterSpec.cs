using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Entities;
using DA.Game.Domain2.Matches.Specifications.Creatures;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.Specifications.Evolution;

public sealed class CanEvolveCharacterSpec : ISpecification<EvolutionContext>
{
    private readonly ISpecification<EvolutionContext> _inner;

    public CanEvolveCharacterSpec(int maxEvolutionsPerRound)
    {
        //_inner =
        //    new IsAliveSpec()
        //        .And(new NotStunnedSpec());
    }

    public bool IsSatisfiedBy(EvolutionContext c)
        => _inner.IsSatisfiedBy(c);
}
