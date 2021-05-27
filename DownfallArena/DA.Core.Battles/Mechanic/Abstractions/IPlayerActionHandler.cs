using System.Collections.Generic;
using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles.Enum;

namespace DA.Core.Battles.Mechanic.Abstractions
{
    public interface IPlayerActionHandler
    {
        void PlaySpell(Character source, Spell spell, List<Character> targets, Speed init);
    }
}