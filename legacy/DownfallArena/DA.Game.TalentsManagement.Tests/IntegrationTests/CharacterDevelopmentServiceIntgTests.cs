using DA.Game.Domain.Models;
using DA.Game.Domain.Services.TalentsManagement;
using DA.Game.Resources.Generator;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.IoC;
using DA.Game.TalentsManagement.Tools;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.IntegrationTests
{
    public class CharacterDevelopmentServiceIntgTests
    {

        public ServiceProvider ServiceProvider { get; }

        public CharacterDevelopmentServiceIntgTests()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddGameResources();
            services.AddGameTalentsManagement();

            ServiceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void BasicUnlockSpellTest()
        {
            ICharacterDevelopmentService sut = (ICharacterDevelopmentService)ServiceProvider.GetService(typeof(ICharacterDevelopmentService));

            Character newChar = sut.InitializeNewCharacter();

            sut.UnlockSpell(newChar, newChar.CharacterTalentStats.UnlockableSpells[0]);
            Assert.NotNull(newChar);
        }
    }
}
