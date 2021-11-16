using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.Resources.Spells
{
    public interface ITricksterSpells
    {
        Spell GetInfectiousBlast();
        Spell GetNoxiousCure();
        Spell GetTranquilizerDart();
    }
}