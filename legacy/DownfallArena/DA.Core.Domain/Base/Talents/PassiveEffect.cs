using DA.Core.Domain.Base.Talents.Enum;
using System;

namespace DA.Core.Domain.Base.Talents
{
    [Serializable]
    public class PassiveEffect
    {
        public StatModifier StatModifier { get; set; }
    }
}
