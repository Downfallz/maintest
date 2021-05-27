using DA.Core.Domain.Base.Talents;
using DA.Core.Domain.Base.Talents.Talents.Enum;

namespace DA.Core.Teams.Abstractions
{
    public interface IGetSpell
    {
        Spell FromEnum(TalentList colorBand);
    }
}