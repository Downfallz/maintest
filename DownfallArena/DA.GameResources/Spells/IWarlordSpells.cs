using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IWarlordSpells
    {
        Spell GetCrushingStomp();
        Spell GetFullPlate();
        Spell GetRestorativeGush();
    }
}