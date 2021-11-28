using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;
using DA.Game.Domain.Models.CombatMechanic.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Domain.Services.CombatMechanic
{
    public interface ISpellResolverService
    {
        SpellResolverResult PlaySpell(Character source, Spell spell, List<Character> targets, Speed init);
    }
}