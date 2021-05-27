using System;
using System.Collections.Generic;
using System.Linq;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Enum;

namespace DA.Core.Domain.Base.Teams
{
    [Serializable]
    public class CharCondition
    {
        public bool IsPermanent { get; set; }
        public StatModifier StatModifier { get; set; }
        public int RoundsLeft { get; set; }
    }
}
