using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ISorcererSpells
    {
        Spell GetLightningBolt();
        Spell GetRejuvenate();
    }
}