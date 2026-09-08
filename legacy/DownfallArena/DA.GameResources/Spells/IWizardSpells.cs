using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IWizardSpells
    {
        Spell GetEngulfingFlames();
        Spell GetIceSpear();
        Spell GetMeteor();
    }
}