using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Domain2.Matches.Specifications.Evolution;
using DA.Game.Domain2.Shared.Primitives;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Shared.Policies.Evolution;

public sealed class EvolutionPolicy : IEvolutionPolicy
{
    private readonly CanEvolveCharacterSpec _canEvolve;

    public EvolutionPolicy()
    {
        _canEvolve = new CanEvolveCharacterSpec();
    }

    public bool CanEvolve(CharacterId id, EvolutionContext evolutionContext)
    {
        return _canEvolve.IsSatisfiedBy(evolutionContext);
    }
}
