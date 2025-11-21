using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Shared.Policies.Evolution;

public interface IEvolutionPolicy
{
    bool CanEvolve(CreatureId id, EvolutionContext evolutionContext);
}
