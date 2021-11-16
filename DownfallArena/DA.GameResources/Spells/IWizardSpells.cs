using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IWizardSpells
    {
        Spell GetEngulfingFlames();
        Spell GetIceSpear();
        Spell GetMeteor();
    }
}