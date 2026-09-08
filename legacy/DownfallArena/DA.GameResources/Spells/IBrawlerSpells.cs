using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IBrawlerSpells
    {
        Spell GetGuard();
        Spell GetPummel();
    }
}