using System.Linq;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells.Enum;

namespace DA.AI
{
    public static class CharacterHelper
    {
        public static bool IsAHealer(this Character character)
        {
            return character.CharacterTalentStats.UnlockedSpells.Any(x =>
                x.SpellType == SpellType.Defensive && x.Effects.Any(x => x.Stats == Stats.Health));
        }
    }
}
