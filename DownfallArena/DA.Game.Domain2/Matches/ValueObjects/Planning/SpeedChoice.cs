using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Planning;

public sealed record SpeedChoice : ValueObject
{
    public CreatureId CreatureId { get; }
    public SkillSpeed Speed { get; }

    private SpeedChoice(CreatureId creatureId, SkillSpeed speed)
    {
        if (creatureId == default)
            throw new ArgumentException("CreatureId cannot be default.", nameof(creatureId));

        CreatureId = creatureId;
        Speed = speed;
    }

    public static SpeedChoice Of(CreatureId creatureId, SkillSpeed speed)
        => new(creatureId, speed);

    public override string ToString()
        => $"SpeedChoice[{CreatureId}, {Speed}]";
}
