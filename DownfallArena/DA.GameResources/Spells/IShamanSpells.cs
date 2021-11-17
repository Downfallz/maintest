using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IShamanSpells
    {
        Spell GetHealingScreech();
        Spell GetRestoringBurst();
        Spell GetToxicWaves();
    }
}