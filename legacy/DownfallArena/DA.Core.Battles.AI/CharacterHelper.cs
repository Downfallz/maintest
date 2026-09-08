using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DA.Core.Domain.Base.Talents.Enum;
using DA.Core.Domain.Base.Teams;

namespace DA.Core.Game.AI
{
    public static class CharacterHelper
    {
        public static bool IsAHealer(this Character character)
        {
            return character.UnlockedSpells.Any(x =>
                x.SpellType == SpellType.Defensive && x.Effects.Any(x => x.Stats == Stats.Health));
        }
    }
}
