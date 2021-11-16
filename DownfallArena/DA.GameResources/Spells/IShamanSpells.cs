using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IShamanSpells
    {
        Spell GetHealingScreech();
        Spell GetRestoringBurst();
        Spell GetToxicWaves();
    }
}