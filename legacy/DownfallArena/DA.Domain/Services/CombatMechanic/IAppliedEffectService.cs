using System.Collections.Generic;
using DA.Game.Domain.Models;
using DA.Game.Domain.Models.CombatMechanic;

namespace DA.Game.Domain.Services.CombatMechanic
{
    public interface IAppliedEffectService
    {
        AppliedEffectResult ApplyEffect(AppliedEffect effect, Character source, List<Character> targets);
    }
}