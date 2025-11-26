using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.Planning
{
    public record SpeedChoice(CreatureId CreatureId, SkillSpeed Speed) : ValueObject
    {
        public static SpeedChoice Create(CreatureId creatureId, SkillSpeed speed)
        {
            return new SpeedChoice(creatureId, speed);
        }
    }
}
