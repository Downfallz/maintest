using DA.Game.Domain2.Matches.Enums;
using DA.Game.Domain2.Shared.Primitives;

namespace DA.Game.Domain2.Matches.ValueObjects
{
    public record SpeedChoice : ValueObject
    {
        protected SpeedChoice(Guid CharacterId, Speed Speed) { }
        
        public static SpeedChoice Create(Guid characterId, Speed speed)
        {
            return new SpeedChoice(characterId, speed);
        }
    }
}
