using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IAssassinSpells
    {
        public Spell GetDeathSquad();
        public Spell GetMomentum();
        public Spell GetMortalWound();
    }
}