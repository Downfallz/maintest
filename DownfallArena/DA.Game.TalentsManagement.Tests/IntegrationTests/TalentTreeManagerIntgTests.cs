using DA.Game.Resources.Generator;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.IntegrationTests
{
    public class TalentTreeManagerIntgTests
    {

        public ServiceProvider ServiceProvider { get; }

        public TalentTreeManagerIntgTests()
        {
            var services = new ServiceCollection();

            services.AddScoped<IGetSpell, GetSpell>();
            services.AddScoped<ITalentTreeBuilder, TalentTreeBuilder>();
            services.AddScoped<ITalentTreeManager, TalentTreeManager>();

            ServiceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Test1()
        {
            ITalentTreeManager sut = (ITalentTreeManager)ServiceProvider.GetService(typeof(ITalentTreeManager));

            var tree = sut.InitializeNewTalentTree();
            var all = sut.GetAllSpells(tree);
            var unlocked = sut.GetUnlockedSpells(tree);
            var unlockable = sut.GetUnlockableSpells(tree);

            Assert.Equal(3, unlocked.Count);
            Assert.Equal(6, unlockable.Count);

            sut.UnlockSpell(tree, unlockable[0]);
            unlockable = sut.GetUnlockableSpells(tree);
            Assert.Equal(8, unlockable.Count);
        }
    }
}
