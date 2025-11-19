using DA.Game.Domain2.Matches.Enums;
using DA.Game.Domain2.Shared.Ids;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.ValueObjects
{
    public record SpeedChoice(CharacterId CharacterId, Speed Speed) : ValueObject
    {        
        public static SpeedChoice Create(CharacterId characterId, Speed speed)
        {
            return new SpeedChoice(characterId, speed);
        }
    }
}
