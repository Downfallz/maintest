using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface INecromancerSpells
    {
        Spell GetCrazedSpecters();
        Spell GetRevenantGuards();
        Spell GetSummonMinions();
    }
}