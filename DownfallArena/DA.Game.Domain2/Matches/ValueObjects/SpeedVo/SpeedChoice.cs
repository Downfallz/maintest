using DA.Game.Shared.Contracts.Matches.Enums;
using DA.Game.Shared.Contracts.Matches.Ids;
using DA.Game.Shared.Utilities;

namespace DA.Game.Domain2.Matches.ValueObjects.SpeedVo
{
    public record SpeedChoice(CreatureId CharacterId, Speed Speed) : ValueObject
    {
        public static SpeedChoice Create(CreatureId characterId, Speed speed)
        {
            return new SpeedChoice(characterId, speed);
        }
    }
}
