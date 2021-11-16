using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IAssassinSpells
    {
        public Spell GetDeathSquad();
        public Spell GetMomentum();
        public Spell GetMortalWound();
    }
}