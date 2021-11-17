using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface INecromancerSpells
    {
        Spell GetCrazedSpecters();
        Spell GetRevenantGuards();
        Spell GetSummonMinions();
    }
}