using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IScoundrelSpells
    {
        Spell GetPoisonSlash();
        Spell GetThrowingStar();
    }
}