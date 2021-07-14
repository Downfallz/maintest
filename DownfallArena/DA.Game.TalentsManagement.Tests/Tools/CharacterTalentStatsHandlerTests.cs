using AutoFixture.Xunit2;
using DA.Game.Domain.Models.GameFlowEngine.CombatMechanic;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement;
using DA.Game.Domain.Models.GameFlowEngine.TalentsManagement.Spells;
using DA.Game.TalentsManagement.Tools;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DA.Game.TalentsManagement.Tests.Tools
{
    public class CharacterTalentStatsHandlerTests
    {
        [AutoMoqData]
        [Theory]
        public void InitializeCharacterTalentTree_CallsTalentTreeManagerInitializer([Frozen] Mock<ITalentTreeManager> manager, CharacterTalentStatsHandler sut)
        {
            TalentTreeStructure ts = sut.InitializeCharacterTalentTree();
            Assert.NotNull(ts);
            manager.Verify(x => x.InitializeNewTalentTree(), Times.Once);
        }

        [AutoMoqData]
        [Theory]
        public void GiveNullArgs_UnlockSpell_ThrowsExceptions(CharacterTalentStatsHandler sut, TalentTreeStructure talentTreeStructure)
        {
            Assert.Throws<ArgumentNullException>(() => sut.UnlockSpell(null, null));
            Assert.Throws<ArgumentNullException>(() => sut.UnlockSpell(null, new Spell()));
            Assert.Throws<ArgumentException>(() => sut.UnlockSpell(new TalentTreeStructure(), new Spell()));
            Assert.Throws<ArgumentNullException>(() => sut.UnlockSpell(talentTreeStructure, null));
        }

        [AutoMoqData]
        [Theory]
        public void UnlockSpell_CallsTalentTreeManagerUnlocker([Frozen] Mock<ITalentTreeManager> manager,
            CharacterTalentStatsHandler sut,
            TalentTreeStructure talentTreeStructure,
            Spell spell)
        {
            CharacterTalentStats ts = sut.UnlockSpell(talentTreeStructure, spell);
            Assert.NotNull(ts);
            manager.Verify(x => x.UnlockSpell(
                It.Is<TalentTreeStructure>(z => object.ReferenceEquals(z, talentTreeStructure)),
                It.Is<Spell>(z => object.ReferenceEquals(z, spell))), Times.Once);
        }

        [AutoMoqData]
        [Theory]
        public void GiveNullArgs_UpdateCharTalentTree_ThrowsExceptions(CharacterTalentStatsHandler sut, TalentTreeStructure talentTreeStructure)
        {
            Assert.Throws<ArgumentNullException>(() => sut.UpdateCharTalentTree(null));
            Assert.Throws<ArgumentException>(() => sut.UpdateCharTalentTree(new TalentTreeStructure()));
        }

        [AutoMoqData]
        [Theory]
        public void GivenValidTalentTree_UpdateCharTalentTree_CallsUnderlyingDependenceAndSetOnStats([Frozen] Mock<ITalentTreeManager> manager,
            CharacterTalentStatsHandler sut,
            TalentTreeStructure talentTreeStructure,
            IReadOnlyList<Spell> unlockedSpells,
            IReadOnlyList<Spell> unlockableSpells)
        {
            manager.Setup(x => x.GetUnlockedSpells(It.IsAny<TalentTreeStructure>())).Returns(unlockedSpells);
            manager.Setup(x => x.GetUnlockableSpells(It.IsAny<TalentTreeStructure>())).Returns(unlockableSpells);

            CharacterTalentStats ts = sut.UpdateCharTalentTree(talentTreeStructure);

            manager.Verify(x => x.GetUnlockedSpells(
                It.Is<TalentTreeStructure>(z => object.ReferenceEquals(z, talentTreeStructure))), Times.Once);
            manager.Verify(x => x.GetUnlockableSpells(
                It.Is<TalentTreeStructure>(z => object.ReferenceEquals(z, talentTreeStructure))), Times.Once);

            Assert.Same(unlockedSpells, ts.UnlockedSpells);
            Assert.Same(unlockableSpells, ts.UnlockableSpells);
        }

        [AutoMoqData]
        [Theory]
        public void GivenValidTalentTree_UpdateCharTalentTree_SetsAllPassiveEffects([Frozen] Mock<ITalentTreeManager> manager,
            CharacterTalentStatsHandler sut,
            TalentTreeStructure talentTreeStructure,
            IReadOnlyList<Spell> unlockedSpells,
            IReadOnlyList<Spell> unlockableSpells)
        {
            manager.Setup(x => x.GetUnlockedSpells(It.IsAny<TalentTreeStructure>())).Returns(unlockedSpells);
            manager.Setup(x => x.GetUnlockableSpells(It.IsAny<TalentTreeStructure>())).Returns(unlockableSpells);

            CharacterTalentStats ts = sut.UpdateCharTalentTree(talentTreeStructure);

            Assert.Equal(unlockedSpells.SelectMany(x => x.PassiveEffects).ToList().Count, ts.PassiveEffects.Count);
        }

        [AutoMoqData]
        [Theory]
        public void GivenValidTalentTree_UpdateCharTalentTree_SetsInitiativeToBeTheSumOfAllSpellss([Frozen] Mock<ITalentTreeManager> manager,
            CharacterTalentStatsHandler sut,
            TalentTreeStructure talentTreeStructure,
            IReadOnlyList<Spell> unlockedSpells,
            IReadOnlyList<Spell> unlockableSpells)
        {
            manager.Setup(x => x.GetUnlockedSpells(It.IsAny<TalentTreeStructure>())).Returns(unlockedSpells);
            manager.Setup(x => x.GetUnlockableSpells(It.IsAny<TalentTreeStructure>())).Returns(unlockableSpells);

            CharacterTalentStats ts = sut.UpdateCharTalentTree(talentTreeStructure);

            Assert.Equal(unlockedSpells.Sum(x => x.Initiative), ts.Initiative);
        }
    }
}