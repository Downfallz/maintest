using DA.Game.Domain2.Matches.Contexts;
using DA.Game.Shared.Contracts.Matches.Ids;

namespace DA.Game.Domain2.Matches.Policies.Planning;

public sealed class InitiativePolicyV1 : IInitiativePolicy
{
    public bool CanEvolve(CreatureId id, IReadOnlyCollection<CreatureSnapshot> statuses)
    {
        var c = statuses.FirstOrDefault(x => x.CharacterId == id);
        return c != null && !c.IsStunned && c.IsAlive;
    }
}
