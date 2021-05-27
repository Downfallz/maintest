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
            var getSpell = new GetSpell();
            var talentTreeInitializer = new TalentTreeBuilder(getSpell);
            var newTree = talentTreeInitializer.GenerateNewTree();
            Assert.NotNull(newTree);
        }
    }
}
