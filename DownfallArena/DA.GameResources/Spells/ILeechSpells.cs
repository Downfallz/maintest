using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ILeechSpells
    {
        Spell GetHatefulSacrifice();
        Spell GetParasiteJab();
        Spell GetSoulDevourer();
    }
}