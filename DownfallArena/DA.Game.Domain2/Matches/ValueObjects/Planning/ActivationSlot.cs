using DA.Game.Domain2.Matches.Entities;
using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Contracts.Resources.Stats;

namespace DA.Game.Domain2.Matches.ValueObjects.Planning;

public sealed record ActivationSlot(
    PlayerSlot Owner,
    CreatureId CreatureId,
    SkillSpeed Speed,
    Initiative Initiative)
{
    public override string ToString()
        => $"{Owner} - {CreatureId} [{Speed}] Init={Initiative.Value}";
}