using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IMercenarySpells
    {
        Spell GetChainSlash();
        Spell GetProtectiveSlam();
        Spell GetThunderingSeal();
    }
}