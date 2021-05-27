using System.Collections.Generic;
using DA.Game.Domain.Models.GameFlowEngine;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Domain.Services.GameFlowEngine.CombatMechanic
{
    public interface ISpellResolverService
    {
        void PlaySpell(Character source, Spell spell, List<Character> targets, Speed init);
    }
}