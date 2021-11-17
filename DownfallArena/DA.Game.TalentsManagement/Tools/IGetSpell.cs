using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;

namespace DA.Game.TalentsManagement.Tools
{
    public interface IGetSpell
    {
        Spell FromEnum(TalentList colorBand);
    }
}