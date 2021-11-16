using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ICreatureSpells
    {
        Spell GetAttack();
        Spell GetSuperAttack();
        Spell GetWait();
    }
}