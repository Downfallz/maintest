using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement;

namespace DA.Game.TalentsManagement.Tools
{
    public interface ITalentTreeBuilder
    {
        TalentTreeStructure GenerateNewTree();
    }
}