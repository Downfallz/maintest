using DA.Game.Domain2.Match.Enums;
using DA.Game.Domain2.Matches.ValueObjects;
using DA.Game.Domain2.Shared.Ids;

namespace DA.Game.Domain2.Shared.Policies.Evolution;

public sealed class EvolutionPolicyV1 : IEvolutionPolicy
{
    public bool CanEvolve(CharacterId id, IReadOnlyCollection<CharacterStatus> statuses)
    {
        var c = statuses.FirstOrDefault(x => x.CharacterId == id);
        return !c.IsStunned && c.IsAlive;
    }
}
