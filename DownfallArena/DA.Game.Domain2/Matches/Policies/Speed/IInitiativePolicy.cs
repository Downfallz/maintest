using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Policies.Speed;

public interface IInitiativePolicy
{
    bool CanEvolve(CreatureId id, IReadOnlyCollection<CharacterStatus> statuses);
}
