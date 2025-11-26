using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Policies.Speed;

public interface IInitiativePolicy
{
    bool CanEvolve(CreatureId id, IReadOnlyCollection<CharacterSnapshot> statuses);
}
