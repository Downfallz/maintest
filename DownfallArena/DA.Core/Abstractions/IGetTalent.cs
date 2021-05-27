using DA.Core.Abilities.Talents.Models;

namespace DA.Core.Abilities.Talents.Abstractions
{
    public interface IGetTalent
    {
        Talent FromEnum(Enum.TalentList colorBand);
    }
}