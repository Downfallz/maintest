using System;
using DA.Game.Domain.Models.CombatMechanic.Enum;

namespace DA.Game.Domain.Models.CombatMechanic
{
    [Serializable]
    public record SpeedChoice
    {
        public Guid CharacterId { get; set; }
        public Speed Speed { get; set; }
    }
}
