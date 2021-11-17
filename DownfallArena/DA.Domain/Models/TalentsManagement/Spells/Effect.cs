using System;
using DA.Game.Domain.Models.TalentsManagement.Spells.Enum;

namespace DA.Game.Domain.Models.TalentsManagement.Spells
{
    [Serializable]
    public class Effect
    {
        public int? Length { get; set; }
        public EffectType EffectType { get; set; }
        public Stats Stats { get; set; }
        public int Modifier { get; set; }
    }
}
