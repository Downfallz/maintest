using DA.Core.Domain.Base.Talents.Talents;

namespace DA.Core.Teams.TalentTree
{
    public interface ITalentTreeBuilder
    {
        TalentTreeStructure GenerateNewTree();
    }
}