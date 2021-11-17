using DA.Game.Domain.Models.TalentsManagement;

namespace DA.Game.TalentsManagement.Tools
{
    public interface ITalentTreeBuilder
    {
        TalentTreeStructure GenerateNewTree();
    }
}