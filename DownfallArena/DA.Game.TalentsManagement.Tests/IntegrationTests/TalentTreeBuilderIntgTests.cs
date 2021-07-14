using DA.Game.Resources.Generator;
using DA.Game.TalentsManagement.Tools;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.IntegrationTests
{
    public class TalentTreeBuilderIntgTests
    {
        [Fact]
        public void GivenTalents_GenerateNewTree_IsSuccess()
        {
            GetSpell getSpell = new GetSpell();
            TalentTreeBuilder talentTreeInitializer = new TalentTreeBuilder(getSpell);
            Domain.Models.GameFlowEngine.TalentsManagement.TalentTreeStructure newTree = talentTreeInitializer.GenerateNewTree();
            Assert.NotNull(newTree);
        }
    }
}
