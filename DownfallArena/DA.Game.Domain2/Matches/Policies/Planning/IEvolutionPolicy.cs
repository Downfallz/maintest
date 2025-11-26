using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Policies.Planning;

public interface IEvolutionPolicy
{
    bool CanEvolve(CreatureId id, CreaturePerspective evolutionContext);
}
