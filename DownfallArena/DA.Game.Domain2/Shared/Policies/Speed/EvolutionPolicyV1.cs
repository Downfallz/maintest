using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Shared.Policies.Speed;

public sealed class InitiativePolicyV1 : IInitiativePolicy
{
    public bool CanEvolve(CreatureId id, IReadOnlyCollection<CharacterStatus> statuses)
    {
        var c = statuses.FirstOrDefault(x => x.CharacterId == id);
        return c != null && !c.IsStunned && c.IsAlive;
    }
}
