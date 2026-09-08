using DA.Core.Domain.Base.Talents.Enum;
using System;

namespace DA.Core.Domain.Base.Talents
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
