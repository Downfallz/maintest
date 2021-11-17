using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Resources.Generator;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.IntegrationTests
{
    public class TalentTreeBuilderIntgTests
    {

        public ServiceProvider ServiceProvider { get; }

        public TalentTreeBuilderIntgTests()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddGameResources();
            services.AddGameTalentsManagement();

            ServiceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void GivenTalents_GenerateNewTree_IsSuccess()
        {
            ITalentTreeBuilder sut = (ITalentTreeBuilder)ServiceProvider.GetService(typeof(ITalentTreeBuilder));
            TalentTreeStructure newTree = sut.GenerateNewTree();
            Assert.NotNull(newTree);
        }
    }
}
