using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Shared.Policies.Speed;

public interface IInitiativePolicy
{
    bool CanEvolve(CharacterId id, IReadOnlyCollection<CharacterStatus> statuses);
}
