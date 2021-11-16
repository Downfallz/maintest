using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IScoundrelSpells
    {
        Spell GetPoisonSlash();
        Spell GetThrowingStar();
    }
}