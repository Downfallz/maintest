using System;
using DA.Game.Domain.Models.TalentsManagement.Enum;
using DA.Game.Domain.Models.TalentsManagement.Spells;
using DA.Game.Resources.IoC;
using DA.Game.TalentsManagement.Tools;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DA.Game.Resources.Tests
{
    public class IntegratedTests
    {
        public IGetSpell getSpell { get; private set; }

        public IntegratedTests()
        {
            ServiceCollection services = new ServiceCollection();

            services.AddGameResources();
            var serviceProvider = services.BuildServiceProvider();

            getSpell = serviceProvider.GetService<IGetSpell>();
        }

        [Fact]
        public void GetEverySpells_ReturnSpell()
        {
            foreach (TalentList tal in (TalentList[])Enum.GetValues(typeof(TalentList)))
            {
                var spell = getSpell.FromEnum(tal);
                spell.Should().NotBeNull();
            }
        }
    }
}