using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IWarlordSpells
    {
        Spell GetCrushingStomp();
        Spell GetFullPlate();
        Spell GetRestorativeGush();
    }
}