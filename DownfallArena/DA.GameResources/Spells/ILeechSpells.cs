using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ILeechSpells
    {
        Spell GetHatefulSacrifice();
        Spell GetParasiteJab();
        Spell GetSoulDevourer();
    }
}