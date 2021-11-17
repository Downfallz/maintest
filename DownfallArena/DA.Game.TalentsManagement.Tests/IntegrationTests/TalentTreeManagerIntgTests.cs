using DA.Game.Domain.Models.TalentsManagement;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
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
            ServiceCollection services = new ServiceCollection();

            services.AddGameResources();
            services.AddGameTalentsManagement();

            ServiceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void GivenTalents_InitializeNewTree_IsSuccess()
        {
            ITalentTreeManager sut = (ITalentTreeManager)ServiceProvider.GetService(typeof(ITalentTreeManager));

            TalentTreeStructure tree = sut.InitializeNewTalentTree();
            System.Collections.Generic.IReadOnlyList<Spell> all = sut.GetAllSpells(tree);
            System.Collections.Generic.IReadOnlyList<Spell> unlocked = sut.GetUnlockedSpells(tree);
            System.Collections.Generic.IReadOnlyList<Spell> unlockable = sut.GetUnlockableSpells(tree);

            Assert.Equal(3, unlocked.Count);
            Assert.Equal(6, unlockable.Count);

            sut.UnlockSpell(tree, unlockable[0]);
            unlockable = sut.GetUnlockableSpells(tree);
            Assert.Equal(8, unlockable.Count);
        }
    }
}
