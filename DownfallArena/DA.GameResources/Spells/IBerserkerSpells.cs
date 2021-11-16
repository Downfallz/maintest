using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface IBerserkerSpells
    {
        public Spell GetEnragedCharge();

        public Spell GetTornado();

        public Spell GetPsychoRush();
    }
}
