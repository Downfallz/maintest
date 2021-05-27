using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Enum;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public interface IGetSpell
    {
        Spell FromEnum(TalentList colorBand);
    }
}