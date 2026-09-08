using System;
using System.Collections.Generic;
using System.Text;
using DA.Core.Domain.Base.Talents.Enum;

namespace DA.Core.Domain.Base
{
    [Serializable]
    public class StatModifier
    {
        public Stats StatType { get; set; }
        public int Modifier { get; set; }
    }
}
