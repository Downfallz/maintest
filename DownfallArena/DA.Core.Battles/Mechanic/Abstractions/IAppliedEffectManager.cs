using System.Collections.Generic;
using DA.Core.Domain.Base.Teams;
using DA.Core.Domain.Battles;

namespace DA.Core.Battles.Mechanic.Abstractions
{
    public interface IAppliedEffectManager
    {
        void ApplyEffect(AppliedEffect effect, Character source, List<Character> targets);
    }
}