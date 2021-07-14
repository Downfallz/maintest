using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic.Enum;
using System;

namespace DA.Game.Domain.Models.GameFlowEngine.CombatMechanic
{
    [Serializable]
    public class SpeedChoice
    {
        public Guid CharacterId { get; set; }
        public Speed Speed { get; set; }
    }
}
