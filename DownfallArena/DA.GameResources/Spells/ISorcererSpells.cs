using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ISorcererSpells
    {
        Spell GetLightningBolt();
        Spell GetRejuvenate();
    }
}