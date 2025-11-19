using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Shared.Policies.Evolution;

public interface IInitiativePolicy
{
    bool CanEvolve(CharacterId id, IReadOnlyCollection<CharacterStatus> statuses);
}
