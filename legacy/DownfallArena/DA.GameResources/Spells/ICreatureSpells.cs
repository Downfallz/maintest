using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ICreatureSpells
    {
        Spell GetAttack();
        Spell GetSuperAttack();
        Spell GetWait();
    }
}